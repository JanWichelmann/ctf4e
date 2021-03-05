using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using Ctf4e.Server.Models;
using Ctf4e.Server.ViewModels;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;

namespace Ctf4e.Server.Services
{
    public interface IScoreboardService
    {
        Task<AdminScoreboard> GetAdminScoreboardAsync(int lessonId, int slotId, CancellationToken cancellationToken = default);
        Task<Scoreboard> GetFullScoreboardAsync(CancellationToken cancellationToken = default, bool forceUncached = false);
        Task<Scoreboard> GetLessonScoreboardAsync(int lessonId, CancellationToken cancellationToken = default, bool forceUncached = false);
        Task<UserScoreboard> GetUserScoreboardAsync(int userId, int groupId, int lessonId, CancellationToken cancellationToken = default);
    }

    public class ScoreboardService : IScoreboardService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly IConfigurationService _configurationService;
        private readonly IMemoryCache _cache;

        /// <summary>
        ///     Database connection for Dapper queries.
        /// </summary>
        private readonly IDbConnection _dbConn;

        private double _minPointsMultiplier;
        private int _halfPointsCount;

        public ScoreboardService(CtfDbContext dbContext, IMapper mapper, IConfigurationService configurationService, IMemoryCache cache)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            // Profile Dapper queries
            _dbConn = new ProfiledDbConnection(_dbContext.Database.GetDbConnection(), MiniProfiler.Current);
        }

        public async Task<AdminScoreboard> GetAdminScoreboardAsync(int lessonId, int slotId, CancellationToken cancellationToken = default)
        {
            // Load flag point parameters
            await InitFlagPointParametersAsync(cancellationToken);

            // Consistent time
            var now = DateTime.Now;

            bool passAsGroup = await _configurationService.GetPassAsGroupAsync(cancellationToken);

            var lessons = await _dbContext.Lessons.AsNoTracking()
                .OrderBy(l => l.Name)
                .ProjectTo<Lesson>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var slots = await _dbContext.Slots.AsNoTracking()
                .OrderBy(s => s.Name)
                .ProjectTo<Slot>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var lessonExecutions = await _dbContext.LessonExecutions.AsNoTracking()
                .Where(l => l.LessonId == lessonId && l.Group.SlotId == slotId)
                .ToDictionaryAsync(l => l.GroupId, cancellationToken); // Each group ID can only appear once, since it is part of the primary key

            var users = await _dbContext.Users.AsNoTracking()
                .Where(u => u.Group.SlotId == slotId)
                .OrderBy(u => u.DisplayName)
                .ToListAsync(cancellationToken);
            var groupIdLookup = users.ToDictionary(u => u.Id, u => u.GroupId);
            var userNameLookup = users.ToDictionary(u => u.Id, u => u.DisplayName);

            var exercises = await _dbContext.Exercises.AsNoTracking()
                .Where(e => e.LessonId == lessonId)
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var exerciseSubmissionsUngrouped = await _dbContext.ExerciseSubmissions.AsNoTracking()
                .Where(e => e.Exercise.LessonId == lessonId && e.User.Group.SlotId == slotId)
                .OrderBy(e => e.ExerciseId)
                .ThenBy(e => e.SubmissionTime)
                .ProjectTo<ExerciseSubmission>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
            var exerciseSubmissions = exerciseSubmissionsUngrouped
                .GroupBy(e => passAsGroup ? groupIdLookup[e.UserId] : e.UserId) // This needs to be done in memory (no aggregation)
                .ToDictionary(
                    e => e.Key,
                    e => e.GroupBy(es => es.ExerciseId)
                        .ToDictionary(es => es.Key, es => es.ToList()));

            var flags = await _dbContext.Flags.AsNoTracking()
                .Where(f => f.LessonId == lessonId)
                .OrderBy(f => f.IsBounty)
                .ThenBy(f => f.Description)
                .ProjectTo<Flag>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var flagSubmissionsUngrouped = await _dbContext.FlagSubmissions.AsNoTracking()
                .Where(f => f.Flag.LessonId == lessonId && f.User.Group.SlotId == slotId)
                .ProjectTo<FlagSubmission>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
            var flagSubmissions = flagSubmissionsUngrouped
                .GroupBy(f => f.UserId) // This needs to be done in memory (no aggregation)
                .ToDictionary(
                    f => f.Key,
                    f => f.GroupBy(fs => fs.FlagId)
                        .ToDictionary(fs => fs.Key, fs => fs.Single())); // There can only be one submission per flag and user

            // Compute submission counts and current points of all flags over all slots
            var scoreboardFlagStatus = (await _dbConn.QueryAsync<FlagEntity, long, AdminScoreboardFlagEntry>(@"
                        SELECT f.*, COUNT(DISTINCT g.`Id`) AS 'SubmissionCount'
                        FROM `Flags` f
                        LEFT JOIN(
                          `FlagSubmissions` s
                          INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                          INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                        ) ON s.`FlagId` = f.`Id`
                          AND g.`ShowInScoreboard` = 1
                          AND EXISTS(
                            SELECT 1
                            FROM `LessonExecutions` le
                            WHERE le.`GroupId` = g.`Id`
                              AND le.`LessonId` = @lessonId
                              AND le.`PreStart` <= s.`SubmissionTime`
                              AND s.`SubmissionTime` < le.`End`
                          )
                        WHERE f.`LessonId` = @lessonId
                        GROUP BY f.`Id`",
                    (flag, submissionCount) => new AdminScoreboardFlagEntry
                    {
                        Flag = _mapper.Map<Flag>(flag),
                        SubmissionCount = (int)submissionCount
                    },
                    new { lessonId },
                    splitOn: "SubmissionCount"))
                .ToDictionary(f => f.Flag.Id);
            foreach(var f in scoreboardFlagStatus)
                f.Value.CurrentPoints = CalculateFlagPoints(f.Value.Flag, f.Value.SubmissionCount);

            int mandatoryExercisesCount = exercises.Count(e => e.IsMandatory);
            var adminScoreboard = new AdminScoreboard
            {
                LessonId = lessonId,
                Lessons = lessons,
                SlotId = slotId,
                Slots = slots,
                MandatoryExercisesCount = mandatoryExercisesCount,
                OptionalExercisesCount = exercises.Count - mandatoryExercisesCount,
                FlagCount = flags.Count,
                Flags = scoreboardFlagStatus.Select(f => f.Value).ToList(),
                UserEntries = new List<AdminScoreboardUserEntry>(),
                PassAsGroup = passAsGroup,
                UserNames = userNameLookup
            };

            // For each user, collect exercise and flag data
            foreach(var user in users)
            {
                lessonExecutions.TryGetValue(user.GroupId ?? -1, out var groupLessonExecution);

                var userEntry = new AdminScoreboardUserEntry
                {
                    UserId = user.Id,
                    UserName = user.DisplayName,
                    Status = LessonExecutionToStatus(now, groupLessonExecution),
                    Exercises = new List<ScoreboardUserExerciseEntry>(),
                    Flags = new List<AdminScoreboardUserFlagEntry>()
                };

                // Exercises
                exerciseSubmissions.TryGetValue(passAsGroup ? user.GroupId : user.Id, out var userExerciseSubmissions);
                int passedMandatoryExercisesCount = 0;
                int passedOptionalExercisesCount = 0;
                foreach(var exercise in exercises)
                {
                    if(userExerciseSubmissions != null && userExerciseSubmissions.ContainsKey(exercise.Id))
                    {
                        var submissions = userExerciseSubmissions[exercise.Id];

                        var (passed, points, validTries) = CalculateExerciseStatus(exercise, submissions, groupLessonExecution);

                        userEntry.Exercises.Add(new ScoreboardUserExerciseEntry
                        {
                            Exercise = exercise,
                            Tries = submissions.Count,
                            ValidTries = validTries,
                            Passed = passed,
                            Points = points,
                            Submissions = submissions
                        });

                        if(passed)
                        {
                            if(exercise.IsMandatory)
                                ++passedMandatoryExercisesCount;
                            else
                                ++passedOptionalExercisesCount;
                        }
                    }
                    else
                    {
                        userEntry.Exercises.Add(new ScoreboardUserExerciseEntry
                        {
                            Exercise = exercise,
                            Tries = 0,
                            ValidTries = 0,
                            Passed = false,
                            Points = 0,
                            Submissions = new List<ExerciseSubmission>()
                        });
                    }
                }

                // Flags
                flagSubmissions.TryGetValue(user.Id, out var userFlagSubmissions);
                int foundFlagsCount = 0;
                foreach(var flag in flags)
                {
                    if(userFlagSubmissions != null && userFlagSubmissions.ContainsKey(flag.Id))
                    {
                        var submission = userFlagSubmissions[flag.Id];

                        var valid = CalculateFlagStatus(submission, groupLessonExecution);

                        userEntry.Flags.Add(new AdminScoreboardUserFlagEntry
                        {
                            Flag = flag,
                            Submitted = true,
                            Valid = valid,
                            CurrentPoints = scoreboardFlagStatus[flag.Id].CurrentPoints,
                            SubmissionTime = submission.SubmissionTime
                        });

                        ++foundFlagsCount;
                    }
                    else
                    {
                        userEntry.Flags.Add(new AdminScoreboardUserFlagEntry
                        {
                            Flag = flag,
                            Submitted = false,
                            Valid = false,
                            SubmissionTime = DateTime.MinValue
                        });
                    }
                }

                // General statistics
                userEntry.PassedMandatoryExercisesCount = passedMandatoryExercisesCount;
                userEntry.HasPassed = passedMandatoryExercisesCount == mandatoryExercisesCount;
                userEntry.PassedOptionalExercisesCount = passedOptionalExercisesCount;
                userEntry.FoundFlagsCount = foundFlagsCount;
                adminScoreboard.UserEntries.Add(userEntry);
            }

            return adminScoreboard;
        }

        /// <summary>
        ///     Derives the status for the given timestamp, considering the given lesson execution constraints.
        /// </summary>
        /// <param name="time">Timestamp to check.</param>
        /// <param name="lessonExecution">Lesson execution data.</param>
        /// <returns></returns>
        private static ScoreboardGroupStatus LessonExecutionToStatus(DateTime time, LessonExecutionEntity lessonExecution)
        {
            if(lessonExecution == null)
                return ScoreboardGroupStatus.Undefined;

            if(time < lessonExecution.PreStart)
                return ScoreboardGroupStatus.BeforePreStart;
            if(lessonExecution.PreStart <= time && time < lessonExecution.Start)
                return ScoreboardGroupStatus.PreStart;
            if(lessonExecution.Start <= time && time < lessonExecution.End)
                return ScoreboardGroupStatus.Start;
            if(lessonExecution.End <= time)
                return ScoreboardGroupStatus.End;

            return ScoreboardGroupStatus.Undefined;
        }

        /// <summary>
        ///     Calculates the points for the given exercise from the given submission list. Ignores tries that are outside of the lesson execution constraints.
        /// </summary>
        /// <param name="exercise">Exercise being evaluated.</param>
        /// <param name="submissions">All submissions for this exercise.</param>
        /// <param name="lessonExecution">Lesson execution data.</param>
        /// <returns></returns>
        private static (bool passed, int points, int validTries) CalculateExerciseStatus(Exercise exercise, IEnumerable<ExerciseSubmission> submissions, LessonExecutionEntity lessonExecution)
        {
            // If the group does not have a lesson execution, collecting points and passing is impossible
            if(lessonExecution == null)
                return (false, 0, 0);

            // Point calculation:
            //     Subtract points for every failed try
            //     If the exercise was passed, add base points
            //     If the number is negative, return 0

            int tries = 0;
            int points = 0;
            bool passed = false;
            foreach(var submission in submissions)
            {
                // Check submission validity
                if(submission.SubmissionTime < lessonExecution.PreStart || lessonExecution.End <= submission.SubmissionTime)
                    continue;
                ++tries;

                if(submission.ExercisePassed)
                {
                    points += exercise.BasePoints;
                    passed = true;
                    break;
                }

                points -= submission.Weight * exercise.PenaltyPoints;
            }

            if(points < 0)
                points = 0;

            return (passed, points, tries);
        }

        /// <summary>
        ///     Calculates whether the given flag submission is valid, by checking the lesson execution constraints.
        /// </summary>
        /// <param name="flagSubmission">Flag submission.</param>
        /// <param name="lessonExecution">Lesson execution data.</param>
        /// <returns></returns>
        private static bool CalculateFlagStatus(FlagSubmission flagSubmission, LessonExecutionEntity lessonExecution)
        {
            // If the group does not have a lesson execution, submitting flags is impossible
            if(lessonExecution == null)
                return false;

            return lessonExecution.PreStart <= flagSubmission.SubmissionTime && flagSubmission.SubmissionTime < lessonExecution.End;
        }

        /// <summary>
        ///     Retrieves the flag point parameters from the configuration.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        private async Task InitFlagPointParametersAsync(CancellationToken cancellationToken = default)
        {
            // Retrieve constants
            _minPointsMultiplier = 1.0 / await _configurationService.GetFlagMinimumPointsDivisorAsync(cancellationToken);
            _halfPointsCount = await _configurationService.GetFlagHalfPointsSubmissionCountAsync(cancellationToken);
        }

        /// <summary>
        ///     Returns the points the flag yields for the give submission count.
        /// </summary>
        /// <param name="flag">Flag.</param>
        /// <param name="submissionCount">Number of valid submissions.</param>
        /// <returns></returns>
        private int CalculateFlagPoints(Flag flag, int submissionCount)
        {
            // Bounties are not scaled
            if(flag.IsBounty)
                return flag.BasePoints;

            // Retrieve necessary constants

            // a: Base points
            // b: Min points = multiplier*a
            // c: 50% points y = (a+b)/2
            // d: 50% points x

            // Flag points depending on submission count x:
            // (a-b)*((a-b)/(c-b))^(1/(d-1)*(-x+1))+b
            // (base is solution of (a-b)*y^(-d+1)+b=c)

            // (a-b)
            double amb = flag.BasePoints - _minPointsMultiplier * flag.BasePoints;

            // (c-b)=(a+b)/2-b=(a-b)/2
            // -> (a-b)/(c-b)=2
            double points = (amb * Math.Pow(2, (-submissionCount + 1.0) / (_halfPointsCount - 1))) + (_minPointsMultiplier * flag.BasePoints);
            return points > flag.BasePoints ? flag.BasePoints : (int)Math.Round(points);
        }

        public async Task<Scoreboard> GetFullScoreboardAsync(CancellationToken cancellationToken = default, bool forceUncached = false)
        {
            // Is there a cached scoreboard?
            const string fullScoreboardCacheKey = "scoreboard-full";
            if(!forceUncached && _cache.TryGetValue(fullScoreboardCacheKey, out Scoreboard scoreboard))
                return scoreboard;

            // Load flag point parameters
            await InitFlagPointParametersAsync(cancellationToken);

            // Get current time to avoid overestimating scoreboard validity time
            DateTime now = DateTime.Now;

            // Get flag point limits
            var flagPointLimits = await _dbContext.Lessons.AsNoTracking()
                .Select(l => new { l.Id, l.MaxFlagPoints })
                .ToDictionaryAsync(l => l.Id, l => l.MaxFlagPoints, cancellationToken);

            // Get list of exercises
            var exercises = await _dbContext.Exercises.AsNoTracking()
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToDictionaryAsync(e => e.Id, cancellationToken);

            // Initialize scoreboard entries with group data and latest submission timestamps
            var scoreboardEntries = (await _dbConn.QueryAsync<ScoreboardEntry>(@"
                    SELECT
                      g.`Id` AS 'GroupId',
                      g.`DisplayName` AS 'GroupName',
                      g.`SlotId`, (
                        SELECT MAX((
                          SELECT MAX(es.`SubmissionTime`)
                          FROM `ExerciseSubmissions` es
                          INNER JOIN `Exercises` e ON es.`ExerciseId` = e.`Id`
                          WHERE u1.`Id` = es.`UserId`
                            AND es.`ExercisePassed`
                            AND EXISTS(
                              SELECT 1
                              FROM `LessonExecutions` le1
                              WHERE g.`Id` = le1.`GroupId`
                                AND le1.`LessonId` = e.`LessonId`
                                AND le1.`PreStart` <= es.`SubmissionTime`
                                AND es.`SubmissionTime` < le1.`End`
                            )
	                    ))
                        FROM `Users` u1
                        WHERE g.`Id` = u1.`GroupId`
                      ) AS 'LastExerciseSubmissionTime', (
                        SELECT MAX((
                          SELECT MAX(fs.`SubmissionTime`)
                          FROM `FlagSubmissions` fs
                          INNER JOIN `Flags` f ON fs.`FlagId` = f.`Id`
                          WHERE u2.`Id` = fs.`UserId`
                            AND EXISTS(
                              SELECT 1
                              FROM `LessonExecutions` le2
                              WHERE g.`Id` = le2.`GroupId`
                                AND le2.`LessonId` = f.`LessonId`
                                AND le2.`PreStart` <= fs.`SubmissionTime`
                                AND fs.`SubmissionTime` < le2.`End`
                            )
                        ))
                        FROM `Users` u2
                        WHERE g.`Id` = u2.`GroupId`
                      ) AS 'LastFlagSubmissionTime'
                    FROM `Groups` g
                    WHERE g.`ShowInScoreboard`"))
                .ToList();

            // Get valid submission counts for passed exercises
            // A passed exercise always has Weight = 1
            var validExerciseSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int ExerciseId, int WeightSum)>(@"
                    CREATE TEMPORARY TABLE MinPassedSubmissionTimes
                      (PRIMARY KEY primary_key (ExerciseId, GroupId))
                      SELECT s.ExerciseId, u.GroupId, MIN(s.`SubmissionTime`) AS 'MinSubmissionTime'
	                  FROM `ExerciseSubmissions` s
                      INNER JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
	                  INNER JOIN `Users` u ON u.`Id` = s.`UserId`
	                  WHERE s.`ExercisePassed` = 1
                        AND EXISTS(
                          SELECT 1
                          FROM `LessonExecutions` le
                          WHERE u.`GroupId` = le.`GroupId`
                            AND le.`LessonId` = e.`LessonId`
                            AND le.`PreStart` <= s.`SubmissionTime`
                            AND s.`SubmissionTime` < le.`End`
                        )
                      GROUP BY s.ExerciseId, u.GroupId
                    ;

                    SELECT g.`Id` AS `GroupId`, e.`Id` AS `ExerciseId`, SUM(s.`Weight`) AS `WeightSum`
                    FROM `ExerciseSubmissions` s
                    INNER JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
                    INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND s.`SubmissionTime` <= (
	                    SELECT st.`MinSubmissionTime`
	                    FROM `MinPassedSubmissionTimes` st
	                    WHERE st.`ExerciseId` = s.ExerciseId
                    	  AND st.`GroupId` = u.`GroupId`
                      )
                      AND EXISTS(
                        SELECT 1
                        FROM `LessonExecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LessonId` = e.`LessonId`
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY g.`Id`, e.`Id`"))
                .ToList();
            var validExerciseSubmissions = validExerciseSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            // Compute submission counts and current points of all flags over all slots
            var flags = (await _dbConn.QueryAsync<FlagEntity, long, ScoreboardFlagEntry>(@"
                        SELECT f.*, COUNT(DISTINCT g.`Id`) AS 'SubmissionCount'
                        FROM `Flags` f
                        LEFT JOIN(
                          `FlagSubmissions` s
                          INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                          INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                        ) ON s.`FlagId` = f.`Id`
                          AND g.`ShowInScoreboard` = 1
                          AND EXISTS(
                            SELECT 1
                            FROM `LessonExecutions` le
                            WHERE le.`GroupId` = g.`Id`
                              AND le.`LessonId` = f.`LessonId`
                              AND le.`PreStart` <= s.`SubmissionTime`
                              AND s.`SubmissionTime` < le.`End`
                          )
                        GROUP BY f.`Id`",
                    (flag, submissionCount) => new ScoreboardFlagEntry
                    {
                        Flag = _mapper.Map<Flag>(flag),
                        SubmissionCount = (int)submissionCount
                    },
                    splitOn: "SubmissionCount"))
                .ToDictionary(f => f.Flag.Id);
            foreach(var f in flags)
                f.Value.CurrentPoints = CalculateFlagPoints(f.Value.Flag, f.Value.SubmissionCount);

            // Get valid submissions for flags
            var validFlagSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int FlagId, int LessonId)>(@"
                    SELECT g.`Id` AS 'GroupId', f.Id AS 'FlagId', f.LessonId
                    FROM `FlagSubmissions` s
                    INNER JOIN `Flags` f ON f.`Id` = s.`FlagId`
                    INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND EXISTS(
                        SELECT 1
                        FROM `LessonExecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LessonId` = f.`LessonId`
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY g.`Id`, f.`Id`"))
                .ToList();
            var validFlagSubmissions = validFlagSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            using(MiniProfiler.Current.Step("Loop scoreboard entries and compute points"))
            {
                // Compute group points
                foreach(var entry in scoreboardEntries)
                {
                    // Exercise points
                    validExerciseSubmissions.TryGetValue(entry.GroupId, out var groupExerciseSubmissions);
                    if(groupExerciseSubmissions != null)
                    {
                        foreach(var s in groupExerciseSubmissions)
                            entry.ExercisePoints += Math.Max(0, exercises[s.ExerciseId].BasePoints - ((s.WeightSum - 1) * exercises[s.ExerciseId].PenaltyPoints));
                    }

                    // Flag points
                    validFlagSubmissions.TryGetValue(entry.GroupId, out var groupFlagSubmissions);
                    if(groupFlagSubmissions != null)
                    {
                        var flagPointsPerLesson = new Dictionary<int, (int FlagPoints, int BugBountyPoints, int FlagCount)>();
                        foreach(var s in groupFlagSubmissions)
                        {
                            if(!flagPointsPerLesson.TryGetValue(s.LessonId, out var fl))
                                fl = (0, 0, 0);

                            // Treat bounties separately
                            if(flags[s.FlagId].Flag.IsBounty)
                                flagPointsPerLesson[s.LessonId] = (fl.FlagPoints, fl.BugBountyPoints + flags[s.FlagId].CurrentPoints, fl.FlagCount);
                            else
                                flagPointsPerLesson[s.LessonId] = (fl.FlagPoints + flags[s.FlagId].CurrentPoints, fl.BugBountyPoints, fl.FlagCount + 1);
                        }

                        foreach(var fl in flagPointsPerLesson)
                        {
                            entry.FlagPoints += Math.Min(fl.Value.FlagPoints, flagPointLimits[fl.Key]);
                            entry.BugBountyPoints += fl.Value.BugBountyPoints;
                            entry.FlagCount += fl.Value.FlagCount;
                        }
                    }

                    entry.TotalPoints = entry.ExercisePoints + entry.FlagPoints + entry.BugBountyPoints;
                    entry.LastSubmissionTime = entry.LastExerciseSubmissionTime > entry.LastFlagSubmissionTime ? entry.LastExerciseSubmissionTime : entry.LastFlagSubmissionTime;
                }
            }

            using(MiniProfiler.Current.Step("Sort scoreboard entries"))
            {
                // Sort list to get ranking
                scoreboardEntries.Sort((g1, g2) =>
                {
                    if(g1.TotalPoints > g2.TotalPoints)
                        return -1;
                    if(g1.TotalPoints < g2.TotalPoints)
                        return 1;
                    if(g1.FlagCount > g2.FlagCount)
                        return -1;
                    if(g1.FlagCount < g2.FlagCount)
                        return 1;
                    if(g1.LastSubmissionTime < g2.LastSubmissionTime)
                        return -1;
                    if(g1.LastSubmissionTime > g2.LastSubmissionTime)
                        return 1;
                    return 0;
                });
            }

            using(MiniProfiler.Current.Step("Compute ranks"))
            {
                // Set rank variables
                int lastRank = 0;
                int lastRankPoints = 0;
                var lastRankSubmissionTime = DateTime.MaxValue;
                foreach(var entry in scoreboardEntries)
                {
                    // Differing values?
                    if(entry.TotalPoints != lastRankPoints || entry.LastSubmissionTime != lastRankSubmissionTime)
                    {
                        // Next rank
                        ++lastRank;
                        lastRankPoints = entry.TotalPoints;
                        lastRankSubmissionTime = entry.LastSubmissionTime;
                    }

                    // Set rank
                    entry.Rank = lastRank;
                }
            }

            using(MiniProfiler.Current.Step("Create final scoreboard object"))
            {
                scoreboard = new Scoreboard
                {
                    AllLessons = true,
                    MaximumEntryCount = await _configurationService.GetScoreboardEntryCountAsync(cancellationToken),
                    Entries = scoreboardEntries,
                    Flags = flags,
                    ValidUntil = now.AddSeconds(await _configurationService.GetScoreboardCachedSecondsAsync(cancellationToken))
                };
            }

            // Update cache
            var cacheDuration = TimeSpan.FromSeconds(await _configurationService.GetScoreboardCachedSecondsAsync(cancellationToken));
            if(cacheDuration > TimeSpan.Zero)
                _cache.Set(fullScoreboardCacheKey, scoreboard, cacheDuration);

            return scoreboard;
        }

        public async Task<Scoreboard> GetLessonScoreboardAsync(int lessonId, CancellationToken cancellationToken = default, bool forceUncached = false)
        {
            // Is there a cached scoreboard?
            string scoreboardCacheKey = "scoreboard-" + lessonId;
            if(!forceUncached && _cache.TryGetValue(scoreboardCacheKey, out Scoreboard scoreboard))
                return scoreboard;

            // Load flag point parameters
            await InitFlagPointParametersAsync(cancellationToken);

            // Get current time to avoid overestimating scoreboard validity time
            DateTime now = DateTime.Now;

            // Get lesson data
            var lesson = await _dbContext.Lessons.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);
            if(lesson == null)
                return null;

            // Get list of exercises
            var exercises = await _dbContext.Exercises.AsNoTracking()
                .Where(e => e.LessonId == lessonId)
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToDictionaryAsync(e => e.Id, cancellationToken);
            if(!exercises.Any())
                return null; // No scoreboard for empty lessons

            // Initialize scoreboard entries with group data and latest submission timestamps
            var scoreboardEntries = (await _dbConn.QueryAsync<ScoreboardEntry>(@"
                    SELECT
                      g.`Id` AS 'GroupId',
                      g.`DisplayName` AS 'GroupName',
                      g.`SlotId`,
                      (
                        SELECT MAX((
                          SELECT MAX(es.`SubmissionTime`)
                          FROM `ExerciseSubmissions` es
                          INNER JOIN `Exercises` e ON es.`ExerciseId` = e.`Id`
                          WHERE e.`LessonId` = @lessonId
                            AND u1.`Id` = es.`UserId`
                            AND es.`ExercisePassed`
                            AND EXISTS(
                              SELECT 1
                              FROM `LessonExecutions` le1
                              WHERE g.`Id` = le1.`GroupId`
                                AND le1.`LessonId` = @lessonId
                                AND le1.`PreStart` <= es.`SubmissionTime`
                                AND es.`SubmissionTime` < le1.`End`
                            )
	                    ))
                        FROM `Users` u1
                        WHERE g.`Id` = u1.`GroupId`
                      ) AS 'LastExerciseSubmissionTime',
                      (
                        SELECT MAX((
                          SELECT MAX(fs.`SubmissionTime`)
                          FROM `FlagSubmissions` fs
                          INNER JOIN `Flags` f ON fs.`FlagId` = f.`Id`
                          WHERE f.`LessonId` = @lessonId
                            AND u2.`Id` = fs.`UserId`
                            AND EXISTS(
                              SELECT 1
                              FROM `LessonExecutions` le2
                              WHERE g.`Id` = le2.`GroupId`
                                AND le2.`LessonId` = @lessonId
                                AND le2.`PreStart` <= fs.`SubmissionTime`
                                AND fs.`SubmissionTime` < le2.`End`
                            )
                        ))
                        FROM `Users` u2
                        WHERE g.`Id` = u2.`GroupId`
                      ) AS 'LastFlagSubmissionTime'
                    FROM `Groups` g
                    WHERE g.`ShowInScoreboard`",
                    new { lessonId }))
                .ToList();

            // Get valid submission counts for passed exercises
            // A passed exercise always has Weight = 1
            var validExerciseSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int ExerciseId, int WeightSum)>(@"
                    CREATE TEMPORARY TABLE MinPassedSubmissionTimes
                      (PRIMARY KEY primary_key (ExerciseId, GroupId))
                      SELECT s.ExerciseId, u.GroupId, MIN(s.`SubmissionTime`) AS 'MinSubmissionTime'
	                  FROM `ExerciseSubmissions` s
	                  INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                      INNER JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
	                  WHERE e.`LessonId` = @lessonId
                        AND s.`ExercisePassed` = 1
                        AND EXISTS(
                          SELECT 1
                          FROM `LessonExecutions` le
                          WHERE u.`GroupId` = le.`GroupId`
                            AND le.`LessonId` = @lessonId
                            AND le.`PreStart` <= s.`SubmissionTime`
                            AND s.`SubmissionTime` < le.`End`
                        )
                      GROUP BY s.ExerciseId, u.GroupId
                    ;

                    SELECT g.`Id` AS `GroupId`, e.`Id` AS `ExerciseId`, SUM(s.`Weight`) AS `WeightSum`
                    FROM `ExerciseSubmissions` s
                    INNER JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
                    INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND e.`LessonId` = @lessonId
                      AND s.`SubmissionTime` <= (
	                    SELECT st.`MinSubmissionTime`
	                    FROM `MinPassedSubmissionTimes` st
	                    WHERE st.`ExerciseId` = s.ExerciseId
                    	  AND st.`GroupId` = u.`GroupId`
                      )
                      AND EXISTS(
                        SELECT 1
                        FROM `LessonExecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LessonId` = @lessonId
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY g.`Id`, e.`Id`",
                    new { lessonId }))
                .ToList();
            var validExerciseSubmissions = validExerciseSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            // Get valid submissions for flags
            var validFlagSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int FlagId, int LessonId)>(@"
                    SELECT g.`Id` AS 'GroupId', f.Id AS 'FlagId', f.LessonId
                    FROM `FlagSubmissions` s
                    INNER JOIN `Flags` f ON f.`Id` = s.`FlagId`
                    INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND f.`LessonId` = @lessonId
                      AND EXISTS(
                        SELECT 1
                        FROM `LessonExecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LessonId` = @lessonId
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY g.`Id`, f.`Id`",
                    new { lessonId }))
                .ToList();
            var validFlagSubmissions = validFlagSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            // Compute submission counts and current points of all flags
            var flags = (await _dbConn.QueryAsync<FlagEntity, long, ScoreboardFlagEntry>(@"
                        SELECT f.*, COUNT(DISTINCT g.`Id`) AS 'SubmissionCount'
                        FROM `Flags` f
                        LEFT JOIN(
                          `FlagSubmissions` s
                          INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                          INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                        ) ON s.`FlagId` = f.`Id`
                          AND g.`ShowInScoreboard` = 1
                          AND EXISTS(
                            SELECT 1
                            FROM `LessonExecutions` le
                            WHERE le.`GroupId` = g.`Id`
                              AND le.`LessonId` = @lessonId
                              AND le.`PreStart` <= s.`SubmissionTime`
                              AND s.`SubmissionTime` < le.`End`
                          )
                        WHERE f.`LessonId` = @lessonId
                        GROUP BY f.`Id`",
                    (flag, submissionCount) => new ScoreboardFlagEntry
                    {
                        Flag = _mapper.Map<Flag>(flag),
                        SubmissionCount = (int)submissionCount
                    },
                    new { lessonId },
                    splitOn: "SubmissionCount"))
                .ToDictionary(f => f.Flag.Id);
            foreach(var f in flags)
                f.Value.CurrentPoints = CalculateFlagPoints(f.Value.Flag, f.Value.SubmissionCount);

            using(MiniProfiler.Current.Step("Loop scoreboard entries and compute points"))
            {
                // Compute group points
                foreach(var entry in scoreboardEntries)
                {
                    // Exercise points
                    validExerciseSubmissions.TryGetValue(entry.GroupId, out var groupExerciseSubmissions);
                    if(groupExerciseSubmissions != null)
                    {
                        foreach(var s in groupExerciseSubmissions)
                            entry.ExercisePoints += Math.Max(0, exercises[s.ExerciseId].BasePoints - ((s.WeightSum - 1) * exercises[s.ExerciseId].PenaltyPoints));
                    }

                    // Flag points
                    validFlagSubmissions.TryGetValue(entry.GroupId, out var groupFlagSubmissions);
                    if(groupFlagSubmissions != null)
                    {
                        foreach(var s in groupFlagSubmissions)
                        {
                            if(flags[s.FlagId].Flag.IsBounty)
                                entry.BugBountyPoints += flags[s.FlagId].CurrentPoints;
                            else
                            {
                                entry.FlagPoints += flags[s.FlagId].CurrentPoints;
                                ++entry.FlagCount;
                            }
                        }

                        if(entry.FlagPoints > lesson.MaxFlagPoints)
                            entry.FlagPoints = lesson.MaxFlagPoints;
                    }

                    entry.TotalPoints = entry.ExercisePoints + entry.FlagPoints + entry.BugBountyPoints;
                    entry.LastSubmissionTime = entry.LastExerciseSubmissionTime > entry.LastFlagSubmissionTime ? entry.LastExerciseSubmissionTime : entry.LastFlagSubmissionTime;
                }
            }

            using(MiniProfiler.Current.Step("Sort scoreboard entries"))
            {
                // Sort list to get ranking
                scoreboardEntries.Sort((g1, g2) =>
                {
                    if(g1.TotalPoints > g2.TotalPoints)
                        return -1;
                    if(g1.TotalPoints < g2.TotalPoints)
                        return 1;
                    if(g1.FlagCount > g2.FlagCount)
                        return -1;
                    if(g1.FlagCount < g2.FlagCount)
                        return 1;
                    if(g1.LastSubmissionTime < g2.LastSubmissionTime)
                        return -1;
                    if(g1.LastSubmissionTime > g2.LastSubmissionTime)
                        return 1;
                    return 0;
                });
            }

            using(MiniProfiler.Current.Step("Compute ranks"))
            {
                // Set rank variables
                int lastRank = 0;
                int lastRankPoints = 0;
                var lastRankSubmissionTime = DateTime.MaxValue;
                foreach(var entry in scoreboardEntries)
                {
                    // Differing values?
                    if(entry.TotalPoints != lastRankPoints || entry.LastSubmissionTime != lastRankSubmissionTime)
                    {
                        // Next rank
                        ++lastRank;
                        lastRankPoints = entry.TotalPoints;
                        lastRankSubmissionTime = entry.LastSubmissionTime;
                    }

                    // Set rank
                    entry.Rank = lastRank;
                }
            }

            string lessonName = await _dbContext.Lessons.AsNoTracking()
                .Where(l => l.Id == lessonId)
                .Select(l => l.Name)
                .FirstAsync(cancellationToken);

            using(MiniProfiler.Current.Step("Create final scoreboard object"))
            {
                scoreboard = new Scoreboard
                {
                    LessonId = lessonId,
                    CurrentLessonName = lessonName,
                    AllLessons = false,
                    MaximumEntryCount = await _configurationService.GetScoreboardEntryCountAsync(cancellationToken),
                    Entries = scoreboardEntries,
                    Flags = flags,
                    ValidUntil = now.AddSeconds(await _configurationService.GetScoreboardCachedSecondsAsync(cancellationToken))
                };
            }

            // Update cache
            var cacheDuration = TimeSpan.FromSeconds(await _configurationService.GetScoreboardCachedSecondsAsync(cancellationToken));
            if(cacheDuration > TimeSpan.Zero)
                _cache.Set(scoreboardCacheKey, scoreboard, cacheDuration);

            return scoreboard;
        }

        public async Task<UserScoreboard> GetUserScoreboardAsync(int userId, int groupId, int lessonId, CancellationToken cancellationToken = default)
        {
            // Consistent time
            var now = DateTime.Now;

            bool passAsGroup = await _configurationService.GetPassAsGroupAsync(cancellationToken);

            var currentLesson = await _dbContext.Lessons.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);
            if(currentLesson == null)
                return null;

            // Get list of lessons
            var lessons = await _dbContext.Lessons.AsNoTracking()
                .OrderBy(l => l.Name)
                .Select(l => new UserScoreboardLessonEntry
                {
                    LessonId = l.Id,
                    Name = l.Name,
                    ServerBaseUrl = l.ServerBaseUrl,
                    Active = l.Executions.Any(le => le.GroupId == groupId && le.PreStart <= now && now < le.End)
                })
                .ToListAsync(cancellationToken);

            // Find active lesson execution
            var lessonExecution = await _dbContext.LessonExecutions.AsNoTracking()
                .FirstOrDefaultAsync(le => le.GroupId == groupId && le.LessonId == lessonId, cancellationToken);

            // Get lookup of group members
            var groupMembers = await _dbContext.Users.AsNoTracking()
                .Where(u => u.GroupId == groupId)
                .OrderBy(u => u.DisplayName)
                .Select(u => new { u.Id, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

            // Get list of exercises for current lesson
            var exercises = await _dbContext.Exercises.AsNoTracking()
                .Where(e => e.LessonId == lessonId)
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            // Retrieve all exercise submissions of this user/group
            var exerciseSubmissions = (await _dbConn.QueryAsync<ExerciseSubmissionEntity>($@"
                    SELECT es.*
                    FROM `ExerciseSubmissions` es
                    INNER JOIN `Exercises` e ON e.`Id` = es.`ExerciseId`
                    INNER JOIN `Users` u ON u.`Id` = es.`UserId`
                    WHERE u.`GroupId` = @groupId
                    {(passAsGroup ? "" : "AND u.`Id` = @userId")}
                    AND e.`LessonId` = @lessonId
                    ORDER BY e.`Id`, es.`SubmissionTime`",
                    new { groupId, userId, lessonId }))
                .Select(es => _mapper.Map<ExerciseSubmission>(es))
                .GroupBy(es => es.ExerciseId)
                .ToDictionary(es => es.Key, es => es.ToList());

            // Retrieve all flag submissions of this group
            var foundFlags = await _dbContext.FlagSubmissions.AsNoTracking()
                .Where(fs => fs.User.GroupId == groupId && fs.Flag.LessonId == lessonId)
                .OrderBy(fs => fs.SubmissionTime)
                .Select(fs => new UserScoreboardFlagEntry
                {
                    Valid = fs.User.Group.LessonExecutions
                        .Any(le => le.LessonId == lessonId && le.PreStart <= fs.SubmissionTime && fs.SubmissionTime < le.End),
                    FlagId = fs.FlagId,
                    UserId = fs.UserId,
                    SubmissionTime = fs.SubmissionTime
                })
                .ToListAsync(cancellationToken);
            var foundFlagsGrouped = foundFlags
                .GroupBy(fs => fs.FlagId)
                .ToList();

            // Retrieve flag codes
            var flags = await _dbContext.Flags.AsNoTracking()
                .Where(f => f.LessonId == lessonId)
                .ProjectTo<Flag>(_mapper.ConfigurationProvider)
                .ToDictionaryAsync(f => f.Id, cancellationToken);
            foreach(var fs in foundFlags)
                fs.FlagCode = flags[fs.FlagId].Code;

            // Build scoreboard
            var scoreboard = new UserScoreboard
            {
                LessonId = lessonId,
                CurrentLesson = lessons.First(l => l.LessonId == lessonId),
                Lessons = lessons,
                LessonExecutionStatus = LessonExecutionToStatus(now, lessonExecution),
                LessonExecution = _mapper.Map<LessonExecution>(lessonExecution),
                FoundFlagsCount = foundFlagsGrouped.Count,
                ValidFoundFlagsCount = foundFlagsGrouped.Count(ff => ff.Any(ffs => ffs.Valid)),
                Exercises = new List<ScoreboardUserExerciseEntry>(),
                GroupMembers = groupMembers,
                Flags = foundFlags
            };

            // Check exercise submissions
            int mandatoryExerciseCount = 0;
            int passedMandatoryExercisesCount = 0;
            int passedOptionalExercisesCount = 0;
            foreach(var exercise in exercises)
            {
                if(exercise.IsMandatory)
                    ++mandatoryExerciseCount;

                if(exerciseSubmissions.ContainsKey(exercise.Id))
                {
                    var submissions = exerciseSubmissions[exercise.Id];

                    var (passed, points, validTries) = CalculateExerciseStatus(exercise, submissions, lessonExecution);

                    scoreboard.Exercises.Add(new ScoreboardUserExerciseEntry
                    {
                        Exercise = exercise,
                        Tries = submissions.Count,
                        ValidTries = validTries,
                        Passed = passed,
                        Points = points,
                        Submissions = submissions
                    });

                    if(passed)
                    {
                        if(exercise.IsMandatory)
                            ++passedMandatoryExercisesCount;
                        else
                            ++passedOptionalExercisesCount;
                    }
                }
                else
                {
                    scoreboard.Exercises.Add(new ScoreboardUserExerciseEntry
                    {
                        Exercise = exercise,
                        Tries = 0,
                        ValidTries = 0,
                        Passed = false,
                        Points = 0,
                        Submissions = new List<ExerciseSubmission>()
                    });
                }
            }

            scoreboard.PassedMandatoryExercisesCount = passedMandatoryExercisesCount;
            scoreboard.PassedOptionalExercisesCount = passedOptionalExercisesCount;
            scoreboard.HasPassed = passedMandatoryExercisesCount == mandatoryExerciseCount;

            return scoreboard;
        }
    }
}