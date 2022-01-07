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

namespace Ctf4e.Server.Services;

public interface IScoreboardService
{
    Task<AdminScoreboard> GetAdminScoreboardAsync(int labId, int slotId, bool groupMode, bool includeTutors, CancellationToken cancellationToken = default);
    Task<Scoreboard> GetFullScoreboardAsync(int? slotId, CancellationToken cancellationToken = default, bool forceUncached = false);
    Task<Scoreboard> GetLabScoreboardAsync(int labId, int? slotId, CancellationToken cancellationToken = default, bool forceUncached = false);
    Task<UserScoreboard> GetUserScoreboardAsync(int userId, int groupId, int labId, CancellationToken cancellationToken = default);
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

    public async Task<AdminScoreboard> GetAdminScoreboardAsync(int labId, int slotId, bool groupMode, bool includeTutors, CancellationToken cancellationToken = default)
    {
        // Load flag point parameters
        await InitFlagPointParametersAsync(cancellationToken);

        // Consistent time
        var now = DateTime.Now;

        bool passAsGroup = await _configurationService.GetPassAsGroupAsync(cancellationToken);

        var labs = await _dbContext.Labs.AsNoTracking()
            .OrderBy(l => l.Name)
            .ProjectTo<Lab>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var slots = await _dbContext.Slots.AsNoTracking()
            .OrderBy(s => s.Name)
            .ProjectTo<Slot>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var labExecutions = await _dbContext.LabExecutions.AsNoTracking()
            .Where(l => l.LabId == labId && l.Group.SlotId == slotId)
            .ToDictionaryAsync(l => l.GroupId, cancellationToken); // Each group ID can only appear once, since it is part of the primary key
            
        var users = await _dbContext.Users.AsNoTracking()
            .Where(u => u.GroupId != null && u.Group.SlotId == slotId)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);
        var groupIdLookup = users.ToDictionary(u => u.Id, u => u.GroupId!.Value);
        var userNameLookup = users.ToDictionary(u => u.Id, u => u.DisplayName);
            
        var groups = await _dbContext.Groups.AsNoTracking()
            .Where(g => g.SlotId == slotId)
            .OrderBy(g => g.DisplayName)
            .ProjectTo<Group>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        var groupNameLookup = groups.ToDictionary(g => g.Id, g => g.DisplayName);

        var exercises = await _dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var exerciseSubmissionsUngrouped = await _dbContext.ExerciseSubmissions.AsNoTracking()
            .Where(e => e.Exercise.LabId == labId && e.User.GroupId != null && e.User.Group.SlotId == slotId)
            .OrderBy(e => e.ExerciseId)
            .ThenBy(e => e.SubmissionTime)
            .ProjectTo<ExerciseSubmission>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
            
        // Aggregate exercise submissions by user and by group
        var exerciseSubmissionsByUser = exerciseSubmissionsUngrouped
            .GroupBy(e => e.UserId)
            .ToDictionary(
                e => e.Key,
                e => e.GroupBy(es => es.ExerciseId)
                    .ToDictionary(es => es.Key, es => es.ToList()));
        var exerciseSubmissionsByGroup = exerciseSubmissionsUngrouped
            .GroupBy(e => groupIdLookup[e.UserId])
            .ToDictionary(
                e => e.Key,
                e => e.GroupBy(es => es.ExerciseId)
                    .ToDictionary(es => es.Key, es => es.ToList()));
            
        // Compute exercise statistics
        var exercisePassedCounts = exercises
            .ToDictionary(e => e.Id, _ => 0);
        foreach(var groupSubmissions in exerciseSubmissionsByGroup)
        {
            foreach(var exercise in groupSubmissions.Value)
            {
                if(exercise.Value.Any(es => es.ExercisePassed))
                    ++exercisePassedCounts[exercise.Key];
            }
        }

        var exerciseStatistics = exercises
            .Select(e => new AdminScoreboardExerciseStatisticsEntry
            {
                Exercise = e,
                PassedCount = exercisePassedCounts[e.Id]
            })
            .ToList();

        var flags = await _dbContext.Flags.AsNoTracking()
            .Where(f => f.LabId == labId)
            .OrderBy(f => f.IsBounty)
            .ThenBy(f => f.Description)
            .ProjectTo<Flag>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var flagSubmissionsUngrouped = await _dbContext.FlagSubmissions.AsNoTracking()
            .Where(f => f.Flag.LabId == labId && f.User.GroupId != null && f.User.Group.SlotId == slotId)
            .ProjectTo<FlagSubmission>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        var flagSubmissions = flagSubmissionsUngrouped
            .GroupBy(f => f.UserId) // This needs to be done in memory (no aggregation)
            .ToDictionary(
                f => f.Key,
                f => f.GroupBy(fs => fs.FlagId)
                    .ToDictionary(fs => fs.Key, fs => fs.Single())); // There can only be one submission per flag and user

        // Compute submission counts and current points of all flags over all slots
        var scoreboardFlagStatus = (await _dbConn.QueryAsync<FlagEntity, long, AdminScoreboardFlagStatisticsEntry>(@"
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
                            FROM `LabExecutions` le
                            WHERE le.`GroupId` = g.`Id`
                              AND le.`LabId` = @labId
                              AND le.`PreStart` <= s.`SubmissionTime`
                              AND s.`SubmissionTime` < le.`End`
                          )
                        WHERE f.`LabId` = @labId
                        GROUP BY f.`Id`",
                (flag, submissionCount) => new AdminScoreboardFlagStatisticsEntry
                {
                    Flag = _mapper.Map<Flag>(flag),
                    SubmissionCount = (int)submissionCount
                },
                new { labId },
                splitOn: "SubmissionCount"))
            .ToDictionary(f => f.Flag.Id);
        foreach(var f in scoreboardFlagStatus)
            f.Value.CurrentPoints = CalculateFlagPoints(f.Value.Flag, f.Value.SubmissionCount);

        int mandatoryExercisesCount = exercises.Count(e => e.IsMandatory);
        var adminScoreboard = new AdminScoreboard
        {
            LabId = labId,
            Labs = labs,
            SlotId = slotId,
            Slots = slots,
            GroupMode = groupMode,
            IncludeTutors = includeTutors || groupMode, // Group mode will always include tutors, so we don't have to care about mixed groups
            MandatoryExercisesCount = mandatoryExercisesCount,
            OptionalExercisesCount = exercises.Count - mandatoryExercisesCount,
            FlagCount = flags.Count,
            ExerciseStatistics = exerciseStatistics,
            FlagStatistics = scoreboardFlagStatus.Select(f => f.Value).ToList(),
            UserEntries = new List<AdminScoreboardUserEntry>(),
            GroupEntries = new List<AdminScoreboardGroupEntry>(),
            PassAsGroup = passAsGroup,
            UserNames = userNameLookup,
            GroupNames = groupNameLookup
        };

        // User mode or group mode?
        // In group mode, we merge all submissions and show them once for each group
        // In user mode, we have a scoreboard entry for each user
        //     However, if passing as group is enabled, each user entry still shows all submissions, so the displayed "passed" status makes sense
        if(groupMode)
        {
            // For each group, collect exercise and flag data
            var flagSubmissionsByGroup = flagSubmissionsUngrouped
                .GroupBy(f => groupIdLookup[f.UserId])
                .ToDictionary(
                    f => f.Key,
                    f => f.GroupBy(fs => fs.FlagId)
                        .ToDictionary(fs => fs.Key, fs => fs.ToList()));
            foreach(var group in groups)
            {
                var groupMembers = users
                    .Where(u => u.GroupId == group.Id)
                    .Select(u => u.Id)
                    .ToList();

                labExecutions.TryGetValue(group.Id, out var groupLabExecution);

                var groupEntry = new AdminScoreboardGroupEntry
                {
                    GroupId = group.Id,
                    GroupName = group.DisplayName,
                    Status = LabExecutionToStatus(now, groupLabExecution),
                    Exercises = new List<ScoreboardUserExerciseEntry>(),
                    Flags = new List<AdminScoreboardUserFlagEntry>(),
                    GroupMembers = groupMembers
                };

                // Exercises
                // If passing as group is enabled:
                //     The exercise submissions are already accumulated per group, so just run CalculateExerciseStatus on it
                // If passing as group is *not* enabled:
                //     The exercises submissions are stored per user
                //     First check whether each group member has passed an exercise, by looking at their individual submissions
                //     Then compute the total tries and points using the accumulated submissions
                int passedMandatoryExercisesCount = 0;
                int passedOptionalExercisesCount = 0;
                if(passAsGroup)
                {
                    // Run through exercises
                    //    If there are submissions for that exercise, compute points etc.
                    //    If not, just produce an empty entry
                    exerciseSubmissionsByGroup.TryGetValue(group.Id, out var groupExerciseSubmissions);
                    foreach(var exercise in exercises)
                    {
                        if(groupExerciseSubmissions != null && groupExerciseSubmissions.ContainsKey(exercise.Id))
                        {
                            var submissions = groupExerciseSubmissions[exercise.Id];

                            var (passed, points, validTries) = CalculateExerciseStatus(exercise, submissions, groupLabExecution);

                            groupEntry.Exercises.Add(new ScoreboardUserExerciseEntry
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
                            groupEntry.Exercises.Add(new ScoreboardUserExerciseEntry
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
                }
                else // !passAsGroup
                {
                    // Run through exercises
                    var exerciseSubmissionsPerGroupMember = exerciseSubmissionsByUser.Where(es => groupIdLookup[es.Key] == group.Id).ToList();
                    exerciseSubmissionsByGroup.TryGetValue(group.Id, out var groupExerciseSubmissions);
                    foreach(var exercise in exercises)
                    {
                        // Scan submissions for each group member to find out how many have passed the exercise
                        int groupMembersPassed = 0;
                        foreach(var (_, userExerciseSubmissions) in exerciseSubmissionsPerGroupMember)
                        {
                            if(userExerciseSubmissions.ContainsKey(exercise.Id))
                            {
                                var submissions = userExerciseSubmissions[exercise.Id];

                                // Check whether user has passed
                                var (passed, _, _) = CalculateExerciseStatus(exercise, submissions, groupLabExecution);
                                if(passed)
                                    ++groupMembersPassed;
                            }
                        }

                        // The exercise is passed if all group members have passed
                        bool groupPassed = groupMembersPassed == groupMembers.Count;

                        // Compute other scoreboard statistics by looking at the entire submission list
                        if(groupExerciseSubmissions != null && groupExerciseSubmissions.ContainsKey(exercise.Id))
                        {
                            // Ensure that exercise submission
                            var submissions = groupExerciseSubmissions[exercise.Id];

                            var (_, points, validTries) = CalculateExerciseStatus(exercise, submissions, groupLabExecution);

                            groupEntry.Exercises.Add(new ScoreboardUserExerciseEntry
                            {
                                Exercise = exercise,
                                Tries = submissions.Count,
                                ValidTries = validTries,
                                Passed = groupPassed,
                                Points = points,
                                Submissions = submissions
                            });

                            if(groupPassed)
                            {
                                if(exercise.IsMandatory)
                                    ++passedMandatoryExercisesCount;
                                else
                                    ++passedOptionalExercisesCount;
                            }
                        }
                        else
                        {
                            groupEntry.Exercises.Add(new ScoreboardUserExerciseEntry
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
                }

                // Flags
                flagSubmissionsByGroup.TryGetValue(group.Id, out var groupFlagSubmissions);
                int foundFlagsCount = 0;
                foreach(var flag in flags)
                {
                    if(groupFlagSubmissions != null && groupFlagSubmissions.ContainsKey(flag.Id))
                    {
                        var validSubmission = groupFlagSubmissions[flag.Id].FirstOrDefault(fs => CalculateFlagStatus(fs, groupLabExecution));
                        groupEntry.Flags.Add(new AdminScoreboardUserFlagEntry
                        {
                            Flag = flag,
                            Submitted = true,
                            Valid = validSubmission != null,
                            CurrentPoints = scoreboardFlagStatus[flag.Id].CurrentPoints,
                            SubmissionTime = validSubmission?.SubmissionTime ?? groupFlagSubmissions[flag.Id].First().SubmissionTime
                        });

                        ++foundFlagsCount;
                    }
                    else
                    {
                        groupEntry.Flags.Add(new AdminScoreboardUserFlagEntry
                        {
                            Flag = flag,
                            Submitted = false,
                            Valid = false,
                            SubmissionTime = DateTime.MinValue
                        });
                    }
                }

                // General statistics
                groupEntry.PassedMandatoryExercisesCount = passedMandatoryExercisesCount;
                groupEntry.HasPassed = passedMandatoryExercisesCount == mandatoryExercisesCount;
                groupEntry.PassedOptionalExercisesCount = passedOptionalExercisesCount;
                groupEntry.FoundFlagsCount = foundFlagsCount;
                adminScoreboard.GroupEntries.Add(groupEntry);
            }
        }
        else // !groupMode
        {
            // For each user, collect exercise and flag data
            foreach(var user in users)
            {
                // Skip tutors and admins, if requested
                if(!includeTutors && (user.IsAdmin || user.IsTutor))
                    continue;

                labExecutions.TryGetValue(user.GroupId ?? -1, out var groupLabExecution);

                var userEntry = new AdminScoreboardUserEntry
                {
                    UserId = user.Id,
                    UserName = user.DisplayName,
                    GroupId = user.GroupId,
                    Status = LabExecutionToStatus(now, groupLabExecution),
                    Exercises = new List<ScoreboardUserExerciseEntry>(),
                    Flags = new List<AdminScoreboardUserFlagEntry>()
                };

                // Exercises
                Dictionary<int, List<ExerciseSubmission>> userExerciseSubmissions;
                if(passAsGroup)
                    exerciseSubmissionsByGroup.TryGetValue(user.GroupId ?? -1, out userExerciseSubmissions);
                else
                    exerciseSubmissionsByUser.TryGetValue(user.Id, out userExerciseSubmissions);
                int passedMandatoryExercisesCount = 0;
                int passedOptionalExercisesCount = 0;
                foreach(var exercise in exercises)
                {
                    if(userExerciseSubmissions != null && userExerciseSubmissions.ContainsKey(exercise.Id))
                    {
                        var submissions = userExerciseSubmissions[exercise.Id];

                        var (passed, points, validTries) = CalculateExerciseStatus(exercise, submissions, groupLabExecution);

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

                        var valid = CalculateFlagStatus(submission, groupLabExecution);

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
        }

        return adminScoreboard;
    }

    /// <summary>
    ///     Derives the status for the given timestamp, considering the given lab execution constraints.
    /// </summary>
    /// <param name="time">Timestamp to check.</param>
    /// <param name="labExecution">Lab execution data.</param>
    /// <returns></returns>
    private static ScoreboardGroupStatus LabExecutionToStatus(DateTime time, LabExecutionEntity labExecution)
    {
        if(labExecution == null)
            return ScoreboardGroupStatus.Undefined;

        if(time < labExecution.PreStart)
            return ScoreboardGroupStatus.BeforePreStart;
        if(labExecution.PreStart <= time && time < labExecution.Start)
            return ScoreboardGroupStatus.PreStart;
        if(labExecution.Start <= time && time < labExecution.End)
            return ScoreboardGroupStatus.Start;
        if(labExecution.End <= time)
            return ScoreboardGroupStatus.End;

        return ScoreboardGroupStatus.Undefined;
    }

    /// <summary>
    /// Calculates the points for the given exercise from the given submission list. Ignores tries that are outside of the lab execution constraints.
    /// </summary>
    /// <param name="exercise">Exercise being evaluated.</param>
    /// <param name="submissions">All submissions for this exercise.</param>
    /// <param name="labExecution">Lab execution data.</param>
    /// <returns></returns>
    private static (bool passed, int points, int validTries) CalculateExerciseStatus(Exercise exercise, IEnumerable<ExerciseSubmission> submissions, LabExecutionEntity labExecution)
    {
        // If the group does not have a lab execution, collecting points and passing is impossible
        if(labExecution == null)
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
            if(submission.SubmissionTime < labExecution.PreStart || labExecution.End <= submission.SubmissionTime)
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
    ///     Calculates whether the given flag submission is valid, by checking the lab execution constraints.
    /// </summary>
    /// <param name="flagSubmission">Flag submission.</param>
    /// <param name="labExecution">Lab execution data.</param>
    /// <returns></returns>
    private static bool CalculateFlagStatus(FlagSubmission flagSubmission, LabExecutionEntity labExecution)
    {
        // If the group does not have a lab execution, submitting flags is impossible
        if(labExecution == null)
            return false;

        return labExecution.PreStart <= flagSubmission.SubmissionTime && flagSubmission.SubmissionTime < labExecution.End;
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

    public async Task<Scoreboard> GetFullScoreboardAsync(int? slotId, CancellationToken cancellationToken = default, bool forceUncached = false)
    {
        // Is there a cached scoreboard?
        string fullScoreboardCacheKey = "scoreboard-full-" + (slotId?.ToString() ?? "all");
        if(!forceUncached && _cache.TryGetValue(fullScoreboardCacheKey, out Scoreboard scoreboard))
            return scoreboard;

        // Load flag point parameters
        await InitFlagPointParametersAsync(cancellationToken);

        // Get current time to avoid overestimating scoreboard validity time
        DateTime now = DateTime.Now;

        // Get flag point limits
        var pointLimits = await _dbContext.Labs.AsNoTracking()
            .Select(l => new { l.Id, l.MaxPoints, l.MaxFlagPoints })
            .ToDictionaryAsync(l => l.Id, cancellationToken);

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
                      g.`ScoreboardAnnotation` AS 'GroupAnnotation',
                      g.`ScoreboardAnnotationHoverText` AS 'GroupAnnotationHoverText',
                      g.`SlotId`, (
                        SELECT MAX((
                          SELECT MAX(fs.`SubmissionTime`)
                          FROM `FlagSubmissions` fs
                          INNER JOIN `Flags` f ON fs.`FlagId` = f.`Id`
                          WHERE fs.`UserId` = u2.`Id`
                            AND EXISTS(
                              SELECT 1
                              FROM `LabExecutions` le2
                              WHERE le2.`GroupId` = g.`Id`
                                AND le2.`LabId` = f.`LabId`
                                AND le2.`PreStart` <= fs.`SubmissionTime`
                                AND fs.`SubmissionTime` < le2.`End`
                            )
                        ))
                        FROM `Users` u2
                        WHERE u2.`GroupId` = g.`Id`
                      ) AS 'LastFlagSubmissionTime'
                    FROM `Groups` g
                    WHERE g.`ShowInScoreboard`"))
            .ToList();

        // Get valid submission counts for passed exercises
        // TODO This is slow once again
        var validExerciseSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int ExerciseId, int LabId, int WeightSum, DateTime MinPassedSubmissionTime)>(@"
                WITH
                  `ValidSubmissions` AS (
	                SELECT s.*,
		                   u.`GroupId` AS `GroupId`,
		                   e.`LabId` AS `LabId`
	                FROM `ExerciseSubmissions` s
	                INNER JOIN `Users` u ON u.`Id` = s.`UserId`
	                INNER JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
	                WHERE EXISTS(
		                SELECT 1
		                FROM `LabExecutions` le
		                WHERE u.`GroupId` = le.`GroupId`
		                  AND le.`LabId` = e.`LabId`
		                  AND le.`PreStart` <= s.`SubmissionTime`
		                  AND s.`SubmissionTime` < le.`End`
	                  )
                  ),
                  `MinPassedSubmissionTimes` AS (
	                SELECT s.`ExerciseId`, s.`GroupId`, s.`LabId`,
		                   MIN(s.`SubmissionTime`) AS `MinSubmissionTime`
	                FROM `ValidSubmissions` s
	                WHERE s.`ExercisePassed` = 1
	                GROUP BY s.`ExerciseId`, s.`GroupId`
                  )

                SELECT st.`GroupId` AS `GroupId`,
	                   st.`ExerciseId` AS `ExerciseId`,
	                   st.`LabId` AS `LabId`,
	                   COALESCE(SUM(s.`Weight`), 0) AS `WeightSum`,
	                   st.`MinSubmissionTime` AS `MinPassedSubmissionTime`
                FROM `MinPassedSubmissionTimes` st
                INNER JOIN `Groups` g ON g.`Id` = st.`GroupId` AND g.`ShowInScoreboard` = 1
                LEFT JOIN `ValidSubmissions` s
                  ON s.`ExerciseId` = st.`ExerciseId`
                  AND s.`GroupId` = st.`GroupId`
                  AND s.`ExercisePassed` = 0
                  AND s.`SubmissionTime` <= st.`MinSubmissionTime`
                GROUP BY st.`GroupId`, st.`ExerciseId`"))
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
                            FROM `LabExecutions` le
                            WHERE le.`GroupId` = g.`Id`
                              AND le.`LabId` = f.`LabId`
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
        var validFlagSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int FlagId, int LabId)>(@"
                    SELECT g.`Id` AS 'GroupId', f.Id AS 'FlagId', f.LabId
                    FROM `FlagSubmissions` s
                    INNER JOIN `Flags` f ON f.`Id` = s.`FlagId`
                    INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND EXISTS(
                        SELECT 1
                        FROM `LabExecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LabId` = f.`LabId`
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
                // Compute exercise points per lab
                validExerciseSubmissions.TryGetValue(entry.GroupId, out var groupExerciseSubmissions);
                var exercisePointsPerLab = new Dictionary<int, int>();
                if(groupExerciseSubmissions != null)
                {
                    foreach(var s in groupExerciseSubmissions)
                    {
                        if(s.MinPassedSubmissionTime > entry.LastExerciseSubmissionTime)
                            entry.LastExerciseSubmissionTime = s.MinPassedSubmissionTime;
                            
                        if(!exercisePointsPerLab.TryGetValue(s.LabId, out var ex))
                            ex = 0;

                        exercisePointsPerLab[s.LabId] = ex + Math.Max(0, exercises[s.ExerciseId].BasePoints - s.WeightSum * exercises[s.ExerciseId].PenaltyPoints);
                    }
                }

                // Compute flag points per lab
                validFlagSubmissions.TryGetValue(entry.GroupId, out var groupFlagSubmissions);
                var flagPointsPerLab = new Dictionary<int, (int FlagPoints, int BugBountyPoints, int FlagCount)>();
                if(groupFlagSubmissions != null)
                {
                    foreach(var s in groupFlagSubmissions)
                    {
                        if(!flagPointsPerLab.TryGetValue(s.LabId, out var fl))
                            fl = (0, 0, 0);

                        // Treat bounties separately
                        if(flags[s.FlagId].Flag.IsBounty)
                            flagPointsPerLab[s.LabId] = (fl.FlagPoints, fl.BugBountyPoints + flags[s.FlagId].CurrentPoints, fl.FlagCount);
                        else
                            flagPointsPerLab[s.LabId] = (fl.FlagPoints + flags[s.FlagId].CurrentPoints, fl.BugBountyPoints, fl.FlagCount + 1);
                    }
                }

                // Compute total points
                foreach(var lab in pointLimits)
                {
                    if(!exercisePointsPerLab.TryGetValue(lab.Key, out int exercisePoints))
                        exercisePoints = 0;

                    if(!flagPointsPerLab.TryGetValue(lab.Key, out var flagPoints))
                        flagPoints = (0, 0, 0);

                    // In breakdown, always return theoretical total of points without "maximum lab points" cut-off, just as it is shown in the lab scoreboard
                    entry.ExercisePoints += exercisePoints;
                    int cutOffFlagPoints = Math.Min(lab.Value.MaxFlagPoints, flagPoints.FlagPoints);
                    entry.FlagPoints += cutOffFlagPoints;
                    entry.BugBountyPoints += flagPoints.BugBountyPoints;
                    entry.FlagCount += flagPoints.FlagCount;

                    // Respect cut-offs in total points
                    entry.TotalPoints += Math.Min(lab.Value.MaxPoints, exercisePoints + cutOffFlagPoints) + flagPoints.BugBountyPoints;
                }

                entry.LastSubmissionTime = entry.LastExerciseSubmissionTime > entry.LastFlagSubmissionTime ? entry.LastExerciseSubmissionTime : entry.LastFlagSubmissionTime;
            }
        }

        if(slotId != null)
        {
            using(MiniProfiler.Current.Step("Filter slot"))
            {
                scoreboardEntries.RemoveAll(entry => entry.SlotId != slotId);
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
                AllLabs = true,
                SlotId = slotId,
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

    public async Task<Scoreboard> GetLabScoreboardAsync(int labId, int? slotId, CancellationToken cancellationToken = default, bool forceUncached = false)
    {
        // Is there a cached scoreboard?
        string scoreboardCacheKey = "scoreboard-" + labId + "-" + (slotId?.ToString() ?? "all");
        if(!forceUncached && _cache.TryGetValue(scoreboardCacheKey, out Scoreboard scoreboard))
            return scoreboard;

        // Load flag point parameters
        await InitFlagPointParametersAsync(cancellationToken);

        // Get current time to avoid overestimating scoreboard validity time
        DateTime now = DateTime.Now;

        // Get lab data
        var lab = await _dbContext.Labs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == labId, cancellationToken);
        if(lab == null)
            return null;

        // Get list of exercises
        var exercises = await _dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
            .ToDictionaryAsync(e => e.Id, cancellationToken);
        if(!exercises.Any())
            return null; // No scoreboard for empty labs

        // Initialize scoreboard entries with group data and latest submission timestamps
        // Exclude bug bounties from submission time computation, as points do not exceed the cap
        //     TODO Check this query anyway, the MAX statement appears to be invalid
        var scoreboardEntries = (await _dbConn.QueryAsync<ScoreboardEntry>(@"
                    SELECT
                      g.`Id` AS 'GroupId',
                      g.`DisplayName` AS 'GroupName',
                      g.`ScoreboardAnnotation` AS 'GroupAnnotation',
                      g.`ScoreboardAnnotationHoverText` AS 'GroupAnnotationHoverText',
                      g.`SlotId`,
                      (
                        SELECT MAX((
                          SELECT MAX(fs.`SubmissionTime`)
                          FROM `FlagSubmissions` fs
                          INNER JOIN `Flags` f ON fs.`FlagId` = f.`Id`
                          WHERE f.`LabId` = @labId
                            AND fs.`UserId` = u2.`Id`
                            AND f.`IsBounty` = 0
                            AND EXISTS(
                              SELECT 1
                              FROM `LabExecutions` le2
                              WHERE le2.`GroupId` = g.`Id`
                                AND le2.`LabId` = @labId
                                AND le2.`PreStart` <= fs.`SubmissionTime`
                                AND fs.`SubmissionTime` < le2.`End`
                            )
                        ))
                        FROM `Users` u2
                        WHERE u2.`GroupId` = g.`Id`
                      ) AS 'LastFlagSubmissionTime'
                    FROM `Groups` g
                    WHERE g.`ShowInScoreboard`",
                new { labId }))
            .ToList();

        // Get valid "failed" submission counts for passed exercises
        var validExerciseSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int ExerciseId, int WeightSum, DateTime MinPassedSubmissionTime)>(@"
                WITH
                  `ValidSubmissions` AS (
                    SELECT s.*,
	                       u.`GroupId` AS `GroupId`
                    FROM `ExerciseSubmissions` s
	                INNER JOIN `Users` u ON u.`Id` = s.`UserId`
	                INNER JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
                    WHERE e.`LabId` = @labId
	                  AND EXISTS(
		                SELECT 1
		                FROM `LabExecutions` le
		                WHERE u.`GroupId` = le.`GroupId`
		                  AND le.`LabId` = @labId
		                  AND le.`PreStart` <= s.`SubmissionTime`
		                  AND s.`SubmissionTime` < le.`End`
	                  )
                  ),
                  `MinPassedSubmissionTimes` AS (
	                SELECT s.`ExerciseId`, s.`GroupId`,
	                       MIN(s.`SubmissionTime`) AS `MinSubmissionTime`
	                FROM `ValidSubmissions` s
	                WHERE s.`ExercisePassed` = 1
	                GROUP BY s.`ExerciseId`, s.`GroupId`
                  )

                SELECT st.`GroupId` AS `GroupId`,
	                   st.`ExerciseId` AS `ExerciseId`,
	                   COALESCE(SUM(s.`Weight`), 0) AS `WeightSum`,
	                   st.`MinSubmissionTime` AS `MinPassedSubmissionTime`
                FROM `MinPassedSubmissionTimes` st
                INNER JOIN `Groups` g ON g.`Id` = st.`GroupId` AND g.`ShowInScoreboard` = 1
                LEFT JOIN `ValidSubmissions` s
                  ON s.`ExerciseId` = st.`ExerciseId`
                  AND s.`GroupId` = st.`GroupId`
                  AND s.`ExercisePassed` = 0
                  AND s.`SubmissionTime` <= st.`MinSubmissionTime`
                GROUP BY st.`GroupId`, st.`ExerciseId`
                ", new { labId }))
            .ToList();
        var validExerciseSubmissions = validExerciseSubmissionsUngrouped
            .GroupBy(s => s.GroupId) // This must be an in-memory operation
            .ToDictionary(s => s.Key);

        // Get valid submissions for flags
        var validFlagSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int FlagId, int LabId)>(@"
                    SELECT g.`Id` AS 'GroupId', f.Id AS 'FlagId', f.LabId
                    FROM `FlagSubmissions` s
                    INNER JOIN `Flags` f ON f.`Id` = s.`FlagId`
                    INNER JOIN `Users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND f.`LabId` = @labId
                      AND EXISTS(
                        SELECT 1
                        FROM `LabExecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LabId` = @labId
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY g.`Id`, f.`Id`",
                new { labId }))
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
                            FROM `LabExecutions` le
                            WHERE le.`GroupId` = g.`Id`
                              AND le.`LabId` = @labId
                              AND le.`PreStart` <= s.`SubmissionTime`
                              AND s.`SubmissionTime` < le.`End`
                          )
                        WHERE f.`LabId` = @labId
                        GROUP BY f.`Id`",
                (flag, submissionCount) => new ScoreboardFlagEntry
                {
                    Flag = _mapper.Map<Flag>(flag),
                    SubmissionCount = (int)submissionCount
                },
                new { labId },
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
                    {
                        if(s.MinPassedSubmissionTime > entry.LastExerciseSubmissionTime)
                            entry.LastExerciseSubmissionTime = s.MinPassedSubmissionTime;
                        entry.ExercisePoints += Math.Max(0, exercises[s.ExerciseId].BasePoints - s.WeightSum * exercises[s.ExerciseId].PenaltyPoints);
                    }
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

                    if(entry.FlagPoints > lab.MaxFlagPoints)
                        entry.FlagPoints = lab.MaxFlagPoints;
                }

                entry.TotalPoints = Math.Min(entry.ExercisePoints + entry.FlagPoints + entry.BugBountyPoints, lab.MaxPoints);
                entry.LastSubmissionTime = entry.LastExerciseSubmissionTime > entry.LastFlagSubmissionTime ? entry.LastExerciseSubmissionTime : entry.LastFlagSubmissionTime;
            }
        }

        if(slotId != null)
        {
            using(MiniProfiler.Current.Step("Filter slot"))
            {
                scoreboardEntries.RemoveAll(entry => entry.SlotId != slotId);
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
                LabId = labId,
                SlotId = slotId,
                AllLabs = false,
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

    public async Task<UserScoreboard> GetUserScoreboardAsync(int userId, int groupId, int labId, CancellationToken cancellationToken = default)
    {
        // Consistent time
        var now = DateTime.Now;

        bool passAsGroup = await _configurationService.GetPassAsGroupAsync(cancellationToken);

        var currentLab = await _dbContext.Labs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == labId, cancellationToken);
        if(currentLab == null)
            return null;

        // Get list of labs
        var labs = await _dbContext.Labs.AsNoTracking()
            .OrderBy(l => l.Name)
            .Select(l => new UserScoreboardLabEntry
            {
                LabId = l.Id,
                Name = l.Name,
                ServerBaseUrl = l.ServerBaseUrl,
                Active = l.Executions.Any(le => le.GroupId == groupId && le.PreStart <= now && now < le.End),
                Visible = l.Visible
            })
            .ToListAsync(cancellationToken);

        // Find active lab execution
        var labExecution = await _dbContext.LabExecutions.AsNoTracking()
            .FirstOrDefaultAsync(le => le.GroupId == groupId && le.LabId == labId, cancellationToken);

        // Get lookup of group members
        var groupMembers = await _dbContext.Users.AsNoTracking()
            .Where(u => u.GroupId == groupId)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        // Get list of exercises for current lab
        var exercises = await _dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
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
                    AND e.`LabId` = @labId
                    ORDER BY e.`Id`, es.`SubmissionTime`",
                new { groupId, userId, labId }))
            .Select(es => _mapper.Map<ExerciseSubmission>(es))
            .GroupBy(es => es.ExerciseId)
            .ToDictionary(es => es.Key, es => es.ToList());

        // Retrieve all flag submissions of this group
        var foundFlags = await _dbContext.FlagSubmissions.AsNoTracking()
            .Where(fs => fs.User.GroupId == groupId && fs.Flag.LabId == labId && !fs.Flag.IsBounty) // Do not show bounty flags
            .OrderBy(fs => fs.SubmissionTime)
            .Select(fs => new UserScoreboardFlagEntry
            {
                Valid = fs.User.Group.LabExecutions
                    .Any(le => le.LabId == labId && le.PreStart <= fs.SubmissionTime && fs.SubmissionTime < le.End),
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
            .Where(f => f.LabId == labId)
            .ProjectTo<Flag>(_mapper.ConfigurationProvider)
            .ToDictionaryAsync(f => f.Id, cancellationToken);
        foreach(var fs in foundFlags)
            fs.FlagCode = flags[fs.FlagId].Code;

        // Build scoreboard
        var scoreboard = new UserScoreboard
        {
            LabId = labId,
            CurrentLab = labs.First(l => l.LabId == labId),
            Labs = labs,
            LabExecutionStatus = LabExecutionToStatus(now, labExecution),
            LabExecution = _mapper.Map<LabExecution>(labExecution),
            FoundFlagsCount = foundFlagsGrouped.Count,
            ValidFoundFlagsCount = foundFlagsGrouped.Count(ff => ff.Any(ffs => ffs.Valid)),
            HasFoundAllFlags = foundFlagsGrouped.Count == flags.Count(f => !f.Value.IsBounty),
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

                var (passed, points, validTries) = CalculateExerciseStatus(exercise, submissions, labExecution);

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