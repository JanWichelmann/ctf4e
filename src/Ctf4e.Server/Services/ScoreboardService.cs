﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using Ctf4e.Server.Extensions;
using Ctf4e.Server.Models;
using Ctf4e.Server.ViewModels;
using Dapper;
using Microsoft.EntityFrameworkCore;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;

namespace Ctf4e.Server.Services
{
    public interface IScoreboardService
    {
        Task<AdminScoreboard> GetAdminScoreboardAsync(int labId, int slotId, CancellationToken cancellationToken = default);
        Task<Scoreboard> GetFullScoreboardAsync(CancellationToken cancellationToken = default);
        Task<Scoreboard> GetLabScoreboardAsync(int labId, CancellationToken cancellationToken = default);
        Task<GroupScoreboard> GetGroupScoreboardAsync(int userId, int groupId, int labId, CancellationToken cancellationToken = default);
    }

    public class ScoreboardService : IScoreboardService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly IFlagPointService _flagPointService;
        private readonly IConfigurationService _configurationService;
        private readonly IScoreboardCacheService _scoreboardCacheService;

        /// <summary>
        /// Database connection for Dapper queries.
        /// </summary>
        private readonly IDbConnection _dbConn;

        public ScoreboardService(CtfDbContext dbContext, IMapper mapper, IFlagPointService flagPointService, IConfigurationService configurationService, IScoreboardCacheService scoreboardCacheService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _flagPointService = flagPointService ?? throw new ArgumentNullException(nameof(flagPointService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _scoreboardCacheService = scoreboardCacheService ?? throw new ArgumentNullException(nameof(scoreboardCacheService));

            // Profile Dapper queries
            _dbConn = new ProfiledDbConnection(_dbContext.Database.GetDbConnection(), MiniProfiler.Current);
        }

        public async Task<AdminScoreboard> GetAdminScoreboardAsync(int labId, int slotId, CancellationToken cancellationToken = default)
        {
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
                .Where(u => u.Group.SlotId == slotId)
                .OrderBy(u => u.DisplayName)
                .ToListAsync(cancellationToken);
            var groupIdLookup = users.ToDictionary(u => u.Id, u => u.GroupId);
            var userNameLookup = users.ToDictionary(u => u.Id, u => u.DisplayName);

            var exercises = await _dbContext.Exercises.AsNoTracking()
                .Where(e => e.LabId == labId)
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var exerciseSubmissionsUngrouped = await _dbContext.ExerciseSubmissions.AsNoTracking()
                .Where(e => e.Exercise.LabId == labId && e.User.Group.SlotId == slotId)
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
                .Where(f => f.LabId == labId)
                .OrderBy(f => f.IsBounty)
                .ThenBy(f => f.Description)
                .ProjectTo<Flag>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var flagSubmissionsUngrouped = await _dbContext.FlagSubmissions.AsNoTracking()
                .Where(f => f.Flag.LabId == labId && f.User.Group.SlotId == slotId)
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
                    FROM `flags` f
                    LEFT JOIN `flagsubmissions` s ON s.`FlagId` = f.`Id`
                    INNER JOIN `users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `groups` g ON g.`Id` = u.`GroupId`
                    WHERE f.`LabId` = @labId
                      AND g.`ShowInScoreboard` = 1
                      AND EXISTS(
                        SELECT 1
                        FROM `labexecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LabId` = @labId
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY s.`FlagId`",
                    (flag, submissionCount) => new AdminScoreboardFlagEntry
                    {
                        Flag = _mapper.Map<Flag>(flag),
                        SubmissionCount = (int)submissionCount
                    },
                    new {labId},
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
                labExecutions.TryGetValue(user.GroupId ?? -1, out var groupLabExecution);

                var userEntry = new AdminScoreboardUserEntry
                {
                    UserId = user.Id,
                    UserName = user.DisplayName,
                    Status = LabExecutionToStatus(now, groupLabExecution),
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

            return adminScoreboard;
        }

        /// <summary>
        /// Derives the status for the given timestamp, considering the given lab execution constraints.
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
        /// Calculates whether the given flag submission is valid, by checking the lab execution constraints.
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
        /// Returns the points the flag yields for the give submission count.
        /// </summary>
        /// <param name="flag">Flag.</param>
        /// <param name="submissionCount">Number of valid submissions.</param>
        /// <returns></returns>
        private int CalculateFlagPoints(Flag flag, int submissionCount)
        {
            // Bounties are not scaled
            if(flag.IsBounty)
                return flag.BasePoints;

            int points = _flagPointService.GetFlagPoints(flag.BasePoints, submissionCount);
            return points > flag.BasePoints ? flag.BasePoints : points;
        }

        public async Task<Scoreboard> GetFullScoreboardAsync(CancellationToken cancellationToken = default)
        {
            // Is there a valid, cached scoreboard?
            if(_scoreboardCacheService.TryGetValidFullScoreboard(out var cachedScoreboard))
                return cachedScoreboard;

            // Get current time to avoid overestimating scoreboard validity time
            DateTime now = DateTime.Now;

            // Get flag point limits
            var flagPointLimits = await _dbContext.Labs.AsNoTracking()
                .Select(l => new {l.Id, l.MaxFlagPoints})
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
                              FROM `LabExecutions` le1
                              WHERE g.`Id` = le1.`GroupId`
                                AND le1.`LabId` = e.`LabId`
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
                              FROM `LabExecutions` le2
                              WHERE g.`Id` = le2.`GroupId`
                                AND le2.`LabId` = f.`LabId`
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
	                  FROM `exercisesubmissions` s
	                  INNER JOIN `users` u ON u.`Id` = s.`UserId`
	                  WHERE s.`ExercisePassed` = 1
                      GROUP BY s.ExerciseId, u.GroupId
                    ;

                    SELECT g.`Id` AS `GroupId`, e.`Id` AS `ExerciseId`, SUM(s.`Weight`) AS `WeightSum`
                    FROM `exercisesubmissions` s
                    INNER JOIN `exercises` e ON e.`Id` = s.`ExerciseId`
                    INNER JOIN `users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND s.`SubmissionTime` <= (
	                    SELECT st.`MinSubmissionTime`
	                    FROM `MinPassedSubmissionTimes` st
	                    WHERE st.`ExerciseId` = s.ExerciseId
                    	  AND st.`GroupId` = u.`GroupId`
                      )
                      AND EXISTS(
                        SELECT 1
                        FROM `labexecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LabId` = e.`LabId`
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
                    FROM `flags` f
                    LEFT JOIN `flagsubmissions` s ON s.`FlagId` = f.`Id`
                    INNER JOIN `users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND EXISTS(
                        SELECT 1
                        FROM `labexecutions` le
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
                    FROM `flagsubmissions` s
                    INNER JOIN `flags` f ON f.`Id` = s.`FlagId`
                    INNER JOIN `users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND EXISTS(
                        SELECT 1
                        FROM `labexecutions` le
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
                        var flagPointsPerLab = new Dictionary<int, (int FlagPoints, int BugBountyPoints, int FlagCount)>();
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

                        foreach(var fl in flagPointsPerLab)
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

            Scoreboard scoreboard;
            using(MiniProfiler.Current.Step("Create final scoreboard object"))
            {
                scoreboard = new Scoreboard
                {
                    AllLabs = true,
                    MaximumEntryCount = await _configurationService.GetScoreboardEntryCountAsync(cancellationToken),
                    Entries = scoreboardEntries,
                    Flags = flags,
                    ValidUntil = now.AddSeconds(await _configurationService.GetScoreboardCachedSecondsAsync(cancellationToken))
                };
            }

            _scoreboardCacheService.SetFullScoreboard(scoreboard);
            return scoreboard;
        }

        public async Task<Scoreboard> GetLabScoreboardAsync(int labId, CancellationToken cancellationToken = default)
        {
            // Is there a valid, cached scoreboard?
            if(_scoreboardCacheService.TryGetValidLabScoreboard(labId, out var cachedScoreboard))
                return cachedScoreboard;

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
                          WHERE e.`LabId` = @labId
                            AND u1.`Id` = es.`UserId`
                            AND es.`ExercisePassed`
                            AND EXISTS(
                              SELECT 1
                              FROM `LabExecutions` le1
                              WHERE g.`Id` = le1.`GroupId`
                                AND le1.`LabId` = @labId
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
                          WHERE f.`LabId` = @labId
                            AND u2.`Id` = fs.`UserId`
                            AND EXISTS(
                              SELECT 1
                              FROM `LabExecutions` le2
                              WHERE g.`Id` = le2.`GroupId`
                                AND le2.`LabId` = @labId
                                AND le2.`PreStart` <= fs.`SubmissionTime`
                                AND fs.`SubmissionTime` < le2.`End`
                            )
                        ))
                        FROM `Users` u2
                        WHERE g.`Id` = u2.`GroupId`
                      ) AS 'LastFlagSubmissionTime'
                    FROM `Groups` g
                    WHERE g.`ShowInScoreboard`",
                    new {labId}))
                .ToList();

            // Get valid submission counts for passed exercises
            // A passed exercise always has Weight = 1
            var validExerciseSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int ExerciseId, int WeightSum)>(@"
                    CREATE TEMPORARY TABLE MinPassedSubmissionTimes
                      (PRIMARY KEY primary_key (ExerciseId, GroupId))
                      SELECT s.ExerciseId, u.GroupId, MIN(s.`SubmissionTime`) AS 'MinSubmissionTime'
	                  FROM `exercisesubmissions` s
	                  INNER JOIN `users` u ON u.`Id` = s.`UserId`
                      INNER JOIN `exercises` e ON e.`Id` = s.`ExerciseId`
	                  WHERE e.`LabId` = @labId
                        AND s.`ExercisePassed` = 1
                      GROUP BY s.ExerciseId, u.GroupId
                    ;

                    SELECT g.`Id` AS `GroupId`, e.`Id` AS `ExerciseId`, SUM(s.`Weight`) AS `WeightSum`
                    FROM `exercisesubmissions` s
                    INNER JOIN `exercises` e ON e.`Id` = s.`ExerciseId`
                    INNER JOIN `users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND e.`LabId` = @labId
                      AND s.`SubmissionTime` <= (
	                    SELECT st.`MinSubmissionTime`
	                    FROM `MinPassedSubmissionTimes` st
	                    WHERE st.`ExerciseId` = s.ExerciseId
                    	  AND st.`GroupId` = u.`GroupId`
                      )
                      AND EXISTS(
                        SELECT 1
                        FROM `labexecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LabId` = @labId
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY g.`Id`, e.`Id`",
                    new {labId}))
                .ToList();
            var validExerciseSubmissions = validExerciseSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            // Get valid submissions for flags
            var validFlagSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int FlagId, int LabId)>(@"
                    SELECT g.`Id` AS 'GroupId', f.Id AS 'FlagId', f.LabId
                    FROM `flagsubmissions` s
                    INNER JOIN `flags` f ON f.`Id` = s.`FlagId`
                    INNER JOIN `users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `groups` g ON g.`Id` = u.`GroupId`
                    WHERE g.`ShowInScoreboard` = 1
                      AND f.`LabId` = @labId
                      AND EXISTS(
                        SELECT 1
                        FROM `labexecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LabId` = @labId
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY g.`Id`, f.`Id`",
                    new {labId}))
                .ToList();
            var validFlagSubmissions = validFlagSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            // Compute submission counts and current points of all flags
            var flags = (await _dbConn.QueryAsync<FlagEntity, long, ScoreboardFlagEntry>(@"
                    SELECT f.*, COUNT(DISTINCT g.`Id`) AS 'SubmissionCount'
                    FROM `flags` f
                    LEFT JOIN `flagsubmissions` s ON s.`FlagId` = f.`Id`
                    INNER JOIN `users` u ON u.`Id` = s.`UserId`
                    INNER JOIN `groups` g ON g.`Id` = u.`GroupId`
                    WHERE f.`LabId` = @labId
                      AND g.`ShowInScoreboard` = 1
                      AND EXISTS(
                        SELECT 1
                        FROM `labexecutions` le
                        WHERE le.`GroupId` = g.`Id`
                          AND le.`LabId` = @labId
                          AND le.`PreStart` <= s.`SubmissionTime`
                          AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY f.`Id`",
                    (flag, submissionCount) => new ScoreboardFlagEntry
                    {
                        Flag = _mapper.Map<Flag>(flag),
                        SubmissionCount = (int)submissionCount
                    },
                    new {labId},
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

                        if(entry.FlagPoints > lab.MaxFlagPoints)
                            entry.FlagPoints = lab.MaxFlagPoints;
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

            string labName = await _dbContext.Labs.AsNoTracking()
                .Where(l => l.Id == labId)
                .Select(l => l.Name)
                .FirstAsync(cancellationToken);
            
            Scoreboard scoreboard;
            using(MiniProfiler.Current.Step("Create final scoreboard object"))
            {
                scoreboard = new Scoreboard
                {
                    LabId = labId,
                    CurrentLabName = labName,
                    AllLabs = false,
                    MaximumEntryCount = await _configurationService.GetScoreboardEntryCountAsync(cancellationToken),
                    Entries = scoreboardEntries,
                    Flags = flags,
                    ValidUntil = now.AddSeconds(await _configurationService.GetScoreboardCachedSecondsAsync(cancellationToken))
                };
            }

            _scoreboardCacheService.SetLabScoreboard(labId, scoreboard);
            return scoreboard;
        }

        public async Task<GroupScoreboard> GetGroupScoreboardAsync(int userId, int groupId, int labId, CancellationToken cancellationToken = default)
        {
            // Consistent time
            var now = DateTime.Now;

            bool passAsGroup = await _configurationService.GetPassAsGroupAsync(cancellationToken);

            var currentLab = await _dbContext.Labs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == labId, cancellationToken);
            if(currentLab == null)
                return null;

            var labs = await _dbContext.Labs.AsNoTracking()
                .OrderBy(l => l.Name)
                .Select(l => new GroupScoreboardLabEntry
                {
                    LabId = l.Id,
                    Name = l.Name,
                    ServerBaseUrl = l.ServerBaseUrl,
                    Active = l.Executions.Any(le => le.GroupId == groupId && le.PreStart <= now && now < le.End)
                })
                .ToListAsync(cancellationToken);
            var labExecution = await _dbContext.LabExecutions.AsNoTracking()
                .FirstOrDefaultAsync(le => le.GroupId == groupId && le.LabId == labId, cancellationToken);

            var groupMembers = await _dbContext.Users.AsNoTracking()
                .Where(u => u.GroupId == groupId)
                .OrderBy(u => u.DisplayName)
                .Select(u => new {u.Id, u.DisplayName})
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

            var exercises = await _dbContext.Exercises.AsNoTracking()
                .Where(e => e.LabId == labId)
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var exerciseSubmissions = (await _dbConn.QueryAsync<ExerciseSubmissionEntity>($@"
                    SELECT es.*
                    FROM `ExerciseSubmissions` es
                    INNER JOIN `Exercises` e ON e.`Id` = es.`ExerciseId`
                    INNER JOIN `Users` u ON u.`Id` = es.`UserId`
                    WHERE u.`GroupId` = @groupId
                    {(passAsGroup ? "" : "AND u.`Id` = @userId")}
                    AND e.`LabId` = @labId
                    ORDER BY e.`Id`, es.`SubmissionTime`",
                    new {groupId, userId, labId}))
                .Select(es => _mapper.Map<ExerciseSubmission>(es))
                .GroupBy(es => es.ExerciseId)
                .ToDictionary(es => es.Key, es => es.ToList());

            var foundFlagsCounts = await _dbContext.FlagSubmissions.AsNoTracking()
                .Where(fs => fs.User.GroupId == groupId && fs.Flag.LabId == labId)
                .Select(fs => new
                {
                    Valid = fs.User.Group.LabExecutions
                        .Any(le => le.LabId == labId && le.PreStart <= fs.SubmissionTime && fs.SubmissionTime < le.End)
                })
                .GroupBy(fs => fs.Valid)
                .Select(fs => new {Valid = fs.Key, Count = fs.Count()})
                .ToDictionaryAsync(fs => fs.Valid, fs => fs.Count, cancellationToken);

            var scoreboard = new GroupScoreboard
            {
                LabId = labId,
                CurrentLab = labs.First(l => l.LabId == labId),
                Labs = labs,
                LabExecutionStatus = LabExecutionToStatus(now, labExecution),
                LabExecution = _mapper.Map<LabExecution>(labExecution),
                FoundFlagsCount = foundFlagsCounts.Sum(f => f.Value),
                ValidFoundFlagsCount = foundFlagsCounts.FirstOrDefault(f => f.Key).Value,
                Exercises = new List<ScoreboardUserExerciseEntry>(),
                GroupMembers = groupMembers
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
}