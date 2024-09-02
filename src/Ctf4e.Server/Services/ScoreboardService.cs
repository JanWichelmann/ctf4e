// Toggles between full client-side evaluation of the scoreboard and a partial, slow database-side evaluation.

#define CLIENT_SIDE_SCOREBOARD

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
    Task<Scoreboard> GetFullScoreboardAsync(int? slotId, CancellationToken cancellationToken, bool forceUncached = false);
    Task<Scoreboard> GetLabScoreboardAsync(int labId, int? slotId, CancellationToken cancellationToken, bool forceUncached = false);
    Task<UserDashboard> GetUserScoreboardAsync(int userId, int groupId, int labId, CancellationToken cancellationToken);
}

public class ScoreboardService(CtfDbContext dbContext, IMapper mapper, IConfigurationService configurationService, IMemoryCache cache, ScoreboardUtilities scoreboardUtilities)
    : IScoreboardService
{
    /// <summary>
    /// Database connection for Dapper queries.
    /// </summary>
    private readonly IDbConnection _dbConn = new ProfiledDbConnection(dbContext.Database.GetDbConnection(), MiniProfiler.Current); // Enable profiling
    
    public async Task<Scoreboard> GetFullScoreboardAsync(int? slotId, CancellationToken cancellationToken, bool forceUncached = false)
    {
        // Is there a cached scoreboard?
        string fullScoreboardCacheKey = "scoreboard-full-" + (slotId?.ToString() ?? "all");
        if(!forceUncached && cache.TryGetValue(fullScoreboardCacheKey, out Scoreboard scoreboard))
            return scoreboard;

        // Get current time to avoid overestimating scoreboard validity time
        DateTime now = DateTime.Now;

        // Get flag point limits
        var pointLimits = await dbContext.Labs.AsNoTracking()
            .Select(l => new { l.Id, l.MaxPoints, l.MaxFlagPoints })
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        // Get list of exercises
        var exercises = await dbContext.Exercises.AsNoTracking()
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<Exercise>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        // Initialize scoreboard entries with group data
        var scoreboardEntries = (await _dbConn.QueryAsync<ScoreboardEntry>(@"
                SELECT
                  g.`Id` AS `GroupId`,
                  g.`DisplayName` AS `GroupName`,
                  g.`ScoreboardAnnotation` AS `GroupAnnotation`,
                  g.`ScoreboardAnnotationHoverText` AS `GroupAnnotationHoverText`,
                  g.`SlotId`
                FROM `Groups` g
                WHERE g.`ShowInScoreboard` = 1
                "))
            .ToList();

        Dictionary<int, List<(int ExerciseId, int LabId, int WeightSum, DateTime MinPassedSubmissionTime)>> validExerciseSubmissions = new();
#if CLIENT_SIDE_SCOREBOARD
        // Get passed exercises and associated total penalty weight
        var validExerciseSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int ExerciseId, int LabId, int Weight, bool ExercisePassed, DateTime SubmissionTime)>(@"
	            SELECT u.`GroupId`,
	                   s.`ExerciseId`,
	                   e.`LabId`,
	                   s.`Weight`,
	                   s.`ExercisePassed`,
	                   s.`SubmissionTime`
                FROM `ExerciseSubmissions` s
                JOIN `Users` u ON u.`Id` = s.`UserId`
                JOIN `Groups` g ON g.`Id` = u.`GroupId` AND g.`ShowInScoreboard` = 1
                JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
                JOIN `LabExecutions` le ON le.`GroupId` = u.`GroupId` AND le.`LabId` = e.`LabId`
                WHERE le.`Start` <= s.`SubmissionTime`
                  AND s.`SubmissionTime` < le.`End`
                ORDER BY u.`GroupId`, s.`ExerciseId`, s.`SubmissionTime`
                "))
            .ToList();
        int i = 0;
        while(i < validExerciseSubmissionsUngrouped.Count)
        {
            var currentSubmission = validExerciseSubmissionsUngrouped[i];

            int groupId = currentSubmission.GroupId;
            int exerciseId = currentSubmission.ExerciseId;
            int weightSum = 0;

            // Iterate through following submissions, until we find one that is passed
            bool handled = false;
            while(i < validExerciseSubmissionsUngrouped.Count
                  && (currentSubmission = validExerciseSubmissionsUngrouped[i]).GroupId == groupId
                  && currentSubmission.ExerciseId == exerciseId)
            {
                if(handled)
                {
                    ++i;
                    continue;
                }

                if(currentSubmission.ExercisePassed)
                {
                    if(!validExerciseSubmissions.TryGetValue(groupId, out var groupExerciseSubmissions))
                    {
                        groupExerciseSubmissions = new List<(int ExerciseId, int LabId, int WeightSum, DateTime MinPassedSubmissionTime)>();
                        validExerciseSubmissions.Add(groupId, groupExerciseSubmissions);
                    }

                    groupExerciseSubmissions.Add((exerciseId, currentSubmission.LabId, weightSum, currentSubmission.SubmissionTime));

                    // Skip remaining submissions for this exercise
                    handled = true;
                    ++i;
                    continue;
                }

                weightSum += currentSubmission.Weight;
                ++i;
            }
        }
#else
        // Disabled for now, as the DB-only query is still too slow
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
		                  AND le.`Start` <= s.`SubmissionTime`
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
#endif

        // Compute submission counts and current points of all flags over all slots
        var flags = (await _dbConn.QueryAsync<FlagEntity, long, ScoreboardFlagEntry>(@"
                    SELECT f.*,
                           COUNT(DISTINCT g.`Id`) AS `SubmissionCount`
                    FROM `Flags` f
                    LEFT JOIN(
                      `FlagSubmissions` s
                      JOIN `Users` u ON u.`Id` = s.`UserId`
                      JOIN `Groups` g ON g.`Id` = u.`GroupId` AND g.`ShowInScoreboard` = 1
                    ) ON s.`FlagId` = f.`Id`
                      AND EXISTS(
	                    SELECT 1
	                    FROM `LabExecutions` le
	                    WHERE le.`GroupId` = g.`Id`
	                      AND le.`LabId` = f.`LabId`
	                      AND le.`Start` <= s.`SubmissionTime`
	                      AND s.`SubmissionTime` < le.`End`
                      )
                    GROUP BY f.`Id`
                 ",
                (flag, submissionCount) => new ScoreboardFlagEntry
                {
                    Flag = mapper.Map<Flag>(flag),
                    SubmissionCount = (int)submissionCount
                },
                splitOn: "SubmissionCount"))
            .ToDictionary(f => f.Flag.Id);
        foreach(var f in flags)
            f.Value.CurrentPoints = scoreboardUtilities.CalculateFlagPoints(f.Value.Flag.BasePoints, f.Value.Flag.IsBounty, f.Value.SubmissionCount);

        // Get valid submissions for flags
        var validFlagSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int FlagId, int LabId, DateTime MinSubmissionTime)>(@"
                    SELECT u.`GroupId`,
                           s.`FlagId`,
                           f.`LabId`,
                           MIN(s.`SubmissionTime`) AS `MinSubmissionTime`
                    FROM `FlagSubmissions` s
                    JOIN `Flags` f ON f.`Id` = s.`FlagId`
                    JOIN `Users` u ON u.`Id` = s.`UserId`
                    JOIN `Groups` g ON g.`Id` = u.`GroupId` AND g.`ShowInScoreboard` = 1
                    JOIN `LabExecutions` le ON le.`GroupId` = u.`GroupId` AND le.`LabId` = f.`LabId`
                    WHERE le.`Start` <= s.`SubmissionTime`
                      AND s.`SubmissionTime` < le.`End`
                    GROUP BY g.`Id`, f.`Id`
                "))
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

                        if(flags[s.FlagId].Flag.IsBounty)
                        {
                            // Bug bounties are counted separately, as, in the full scoreboard, they are not subject to the lab point caps.

                            flagPointsPerLab[s.LabId] = (fl.FlagPoints, fl.BugBountyPoints + flags[s.FlagId].CurrentPoints, fl.FlagCount);
                        }
                        else
                        {
                            if(s.MinSubmissionTime > entry.LastFlagSubmissionTime)
                                entry.LastFlagSubmissionTime = s.MinSubmissionTime;

                            flagPointsPerLab[s.LabId] = (fl.FlagPoints + flags[s.FlagId].CurrentPoints, fl.BugBountyPoints, fl.FlagCount + 1);
                        }
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
                MaximumEntryCount = await configurationService.ScoreboardEntryCount.GetAsync(cancellationToken),
                Entries = scoreboardEntries,
                Flags = flags,
                ValidUntil = now.AddSeconds(await configurationService.ScoreboardCachedSeconds.GetAsync(cancellationToken))
            };
        }

        // Update cache
        var cacheDuration = TimeSpan.FromSeconds(await configurationService.ScoreboardCachedSeconds.GetAsync(cancellationToken));
        if(cacheDuration > TimeSpan.Zero)
            cache.Set(fullScoreboardCacheKey, scoreboard, cacheDuration);

        return scoreboard;
    }

    public async Task<Scoreboard> GetLabScoreboardAsync(int labId, int? slotId, CancellationToken cancellationToken, bool forceUncached = false)
    {
        // Is there a cached scoreboard?
        string scoreboardCacheKey = "scoreboard-" + labId + "-" + (slotId?.ToString() ?? "all");
        if(!forceUncached && cache.TryGetValue(scoreboardCacheKey, out Scoreboard scoreboard))
            return scoreboard;

        // Get current time to avoid overestimating scoreboard validity time
        DateTime now = DateTime.Now;

        // Get lab data
        var lab = await dbContext.Labs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == labId, cancellationToken);
        if(lab == null)
            return null;

        // Get list of exercises
        var exercises = await dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<Exercise>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(e => e.Id, cancellationToken);
        if(!exercises.Any())
            return null; // No scoreboard for empty labs

        // Initialize scoreboard entries with group data
        var scoreboardEntries = (await _dbConn.QueryAsync<ScoreboardEntry>(@"
                SELECT
                  g.`Id` AS `GroupId`,
                  g.`DisplayName` AS `GroupName`,
                  g.`ScoreboardAnnotation` AS `GroupAnnotation`,
                  g.`ScoreboardAnnotationHoverText` AS `GroupAnnotationHoverText`,
                  g.`SlotId`
                FROM `Groups` g
                WHERE g.`ShowInScoreboard` = 1
                "))
            .ToList();

        Dictionary<int, List<(int ExerciseId, int WeightSum, DateTime MinPassedSubmissionTime)>> validExerciseSubmissions = new();
#if CLIENT_SIDE_SCOREBOARD
        // Get passed exercises and associated total penalty weight
        var validExerciseSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int ExerciseId, int Weight, bool ExercisePassed, DateTime SubmissionTime)>(@"
	            SELECT u.`GroupId`,
	                   s.`ExerciseId`,
	                   s.`Weight`,
	                   s.`ExercisePassed`,
	                   s.`SubmissionTime`
                FROM `ExerciseSubmissions` s
                JOIN `Users` u ON u.`Id` = s.`UserId`
                JOIN `Groups` g ON g.`Id` = u.`GroupId` AND g.`ShowInScoreboard` = 1
                JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
                JOIN `LabExecutions` le ON le.`GroupId` = u.`GroupId` AND le.`LabId` = @labId
                WHERE e.LabId = @labId
                  AND le.`Start` <= s.`SubmissionTime`
                  AND s.`SubmissionTime` < le.`End`
                ORDER BY u.`GroupId`, s.`ExerciseId`, s.`SubmissionTime`
                ",
                new { labId }))
            .ToList();
        int i = 0;
        while(i < validExerciseSubmissionsUngrouped.Count)
        {
            var currentSubmission = validExerciseSubmissionsUngrouped[i];

            int groupId = currentSubmission.GroupId;
            int exerciseId = currentSubmission.ExerciseId;
            int weightSum = 0;

            // Iterate through following submissions, until we find one that is passed
            bool handled = false;
            while(i < validExerciseSubmissionsUngrouped.Count
                  && (currentSubmission = validExerciseSubmissionsUngrouped[i]).GroupId == groupId
                  && currentSubmission.ExerciseId == exerciseId)
            {
                if(handled)
                {
                    ++i;
                    continue;
                }

                if(currentSubmission.ExercisePassed)
                {
                    if(!validExerciseSubmissions.TryGetValue(groupId, out var groupExerciseSubmissions))
                    {
                        groupExerciseSubmissions = new List<(int ExerciseId, int WeightSum, DateTime MinPassedSubmissionTime)>();
                        validExerciseSubmissions.Add(groupId, groupExerciseSubmissions);
                    }

                    groupExerciseSubmissions.Add((exerciseId, weightSum, currentSubmission.SubmissionTime));

                    // Skip remaining submissions for this exercise
                    handled = true;
                    ++i;
                    continue;
                }

                weightSum += currentSubmission.Weight;
                ++i;
            }
        }
#else
        // Disabled for now, as the DB-only query is still too slow
        var validExerciseSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int ExerciseId, int WeightSum, DateTime MinPassedSubmissionTime)>(@"
                WITH
                  `ValidSubmissions` AS (
	                SELECT s.*,
		                   u.`GroupId` AS `GroupId`,
                          ROW_NUMBER() OVER (PARTITION BY u.`GroupId`, s.`ExerciseId`) AS `SubmissionNumber`
	                FROM `ExerciseSubmissions` s
	                INNER JOIN `Users` u ON u.`Id` = s.`UserId`
	                INNER JOIN `Exercises` e ON e.`Id` = s.`ExerciseId`
	                WHERE e.`LabId` = @labId
	                  AND EXISTS(
		                SELECT 1
		                FROM `LabExecutions` le
		                WHERE u.`GroupId` = le.`GroupId`
		                  AND le.`LabId` = @labId
		                  AND le.`Start` <= s.`SubmissionTime`
		                  AND s.`SubmissionTime` < le.`End`
	                  )
	                ORDER BY s.`SubmissionTime`
                  ),
                  `PassedSubmissionTimes` AS (
	                SELECT s.`GroupId`,
	                       s.`ExerciseId`,
                           s.`SubmissionTime`,
                           s.`SubmissionNumber`,
                           ROW_NUMBER() OVER (PARTITION BY s.`ExerciseId`, s.`GroupId` ORDER BY s.`SubmissionTime`) AS `PassRank`
	                FROM `ValidSubmissions` s
	                WHERE s.`ExercisePassed` = 1
                  )

                SELECT st.`GroupId` AS `GroupId`,
	                   st.`ExerciseId` AS `ExerciseId`,
	                   COALESCE(SUM(s.`Weight`), 0) AS `WeightSum`,
	                   st.`SubmissionTime` AS `MinPassedSubmissionTime`
                FROM `PassedSubmissionTimes` st
                INNER JOIN `Groups` g ON g.`Id` = st.`GroupId` AND g.`ShowInScoreboard` = 1
                LEFT JOIN `ValidSubmissions` s
                  ON s.`ExerciseId` = st.`ExerciseId`
                  AND s.`GroupId` = st.`GroupId`
                  AND s.`ExercisePassed` = 0
                  AND s.`SubmissionNumber` < st.`SubmissionNumber`
                WHERE st.`PassRank` = 1
                GROUP BY st.`GroupId`, st.`ExerciseId`
                ", new { labId }))
            .ToList();
        var validExerciseSubmissions = validExerciseSubmissionsUngrouped
            .GroupBy(s => s.GroupId) // This must be an in-memory operation
            .ToDictionary(s => s.Key);
#endif

        // Compute submission counts and current points of all flags
        var flags = (await _dbConn.QueryAsync<FlagEntity, long, ScoreboardFlagEntry>(@"
                    SELECT f.*,
                           COUNT(DISTINCT g.`Id`) AS `SubmissionCount`
                    FROM `Flags` f
                    LEFT JOIN(
                      `FlagSubmissions` s
                      JOIN `Users` u ON u.`Id` = s.`UserId`
                      JOIN `Groups` g ON g.`Id` = u.`GroupId` AND g.`ShowInScoreboard` = 1
                    ) ON s.`FlagId` = f.`Id`
                      AND EXISTS(
	                    SELECT 1
	                    FROM `LabExecutions` le
	                    WHERE le.`GroupId` = g.`Id`
	                      AND le.`LabId` = @labId
	                      AND le.`Start` <= s.`SubmissionTime`
	                      AND s.`SubmissionTime` < le.`End`
                      )
                    WHERE f.`LabId` = @labId
                    GROUP BY f.`Id`
                 ",
                (flag, submissionCount) => new ScoreboardFlagEntry
                {
                    Flag = mapper.Map<Flag>(flag),
                    SubmissionCount = (int)submissionCount
                },
                new { labId },
                splitOn: "SubmissionCount"))
            .ToDictionary(f => f.Flag.Id);
        foreach(var f in flags)
            f.Value.CurrentPoints = scoreboardUtilities.CalculateFlagPoints(f.Value.Flag.BasePoints, f.Value.Flag.IsBounty, f.Value.SubmissionCount);

        // Get valid submissions for flags
        var validFlagSubmissionsUngrouped = (await _dbConn.QueryAsync<(int GroupId, int FlagId, DateTime MinSubmissionTime)>(@"
                    SELECT u.`GroupId`,
                           s.`FlagId`,
                           MIN(s.`SubmissionTime`) AS `MinSubmissionTime`
                    FROM `FlagSubmissions` s
                    JOIN `Flags` f ON f.`Id` = s.`FlagId`
                    JOIN `Users` u ON u.`Id` = s.`UserId`
                    JOIN `Groups` g ON g.`Id` = u.`GroupId` AND g.`ShowInScoreboard` = 1
                    JOIN `LabExecutions` le ON le.`GroupId` = u.`GroupId` AND le.`LabId` = @labId
                    WHERE f.`LabId` = @labId
                      AND le.`Start` <= s.`SubmissionTime`
                      AND s.`SubmissionTime` < le.`End`
                    GROUP BY g.`Id`, f.`Id`
                ",
                new { labId }))
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
                        {
                            // Bug bounties are counted separately, so we can show a point breakdown.
                            // However, for the lab scoreboard, they are subject to the same total point cap.
                            // Bug bounty submission times do not affect scoreboard sorting.

                            entry.BugBountyPoints += flags[s.FlagId].CurrentPoints;
                        }
                        else
                        {
                            if(s.MinSubmissionTime > entry.LastFlagSubmissionTime)
                                entry.LastFlagSubmissionTime = s.MinSubmissionTime;

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
                MaximumEntryCount = await configurationService.ScoreboardEntryCount.GetAsync(cancellationToken),
                Entries = scoreboardEntries,
                Flags = flags,
                ValidUntil = now.AddSeconds(await configurationService.ScoreboardCachedSeconds.GetAsync(cancellationToken))
            };
        }

        // Update cache
        var cacheDuration = TimeSpan.FromSeconds(await configurationService.ScoreboardCachedSeconds.GetAsync(cancellationToken));
        if(cacheDuration > TimeSpan.Zero)
            cache.Set(scoreboardCacheKey, scoreboard, cacheDuration);

        return scoreboard;
    }

    public async Task<UserDashboard> GetUserScoreboardAsync(int userId, int groupId, int labId, CancellationToken cancellationToken)
    {
        // Consistent time
        var now = DateTime.Now;

        bool passAsGroup = await configurationService.PassAsGroup.GetAsync(cancellationToken);

        var currentLab = await dbContext.Labs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == labId, cancellationToken);
        if(currentLab == null)
            return null;

        // Get list of labs
        var labs = await dbContext.Labs.AsNoTracking()
            .OrderBy(l => l.SortIndex)
            .ThenBy(l => l.Name)
            .Select(l => new UserScoreboardLabEntry
            {
                LabId = l.Id,
                Name = l.Name,
                ServerBaseUrl = l.ServerBaseUrl,
                Active = l.Executions.Any(le => le.GroupId == groupId && le.Start <= now && now < le.End),
                Visible = l.Visible
            })
            .ToListAsync(cancellationToken);

        // Find active lab execution
        var labExecution = await dbContext.LabExecutions.AsNoTracking()
            .FirstOrDefaultAsync(le => le.GroupId == groupId && le.LabId == labId, cancellationToken);

        // Get lookup of group members
        var groupMembers = await dbContext.Users.AsNoTracking()
            .Where(u => u.GroupId == groupId)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        // Get list of exercises for current lab
        var exercises = await dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<Exercise>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        // Retrieve all exercise submissions of this user/group
        var exerciseSubmissions = (await _dbConn.QueryAsync<ExerciseSubmissionEntity>($@"
                    SELECT es.*
                    FROM `ExerciseSubmissions` es
                    INNER JOIN `Exercises` e ON e.`Id` = es.`ExerciseId`
                    INNER JOIN `Users` u ON u.`Id` = es.`UserId`
                    WHERE u.`GroupId` = @groupId
                    AND e.`LabId` = @labId
                    ORDER BY e.`Id`, es.`SubmissionTime`",
                new { groupId, userId, labId }))
            .Select(es => mapper.Map<ExerciseSubmission>(es))
            .GroupBy(es => es.ExerciseId)
            .ToDictionary(es => es.Key, es => es.ToList());

        // Retrieve all flag submissions of this group
        var foundFlags = await dbContext.FlagSubmissions.AsNoTracking()
            .Where(fs => fs.User.GroupId == groupId && fs.Flag.LabId == labId && !fs.Flag.IsBounty) // Do not show bounty flags
            .OrderBy(fs => fs.SubmissionTime)
            .Select(fs => new UserScoreboardFlagEntry
            {
                Valid = fs.User.Group.LabExecutions
                    .Any(le => le.LabId == labId && le.Start <= fs.SubmissionTime && fs.SubmissionTime < le.End),
                FlagId = fs.FlagId,
                UserId = fs.UserId,
                SubmissionTime = fs.SubmissionTime
            })
            .ToListAsync(cancellationToken);
        var foundFlagsGrouped = foundFlags
            .GroupBy(fs => fs.FlagId)
            .ToList();

        // Retrieve flag codes
        var flags = await dbContext.Flags.AsNoTracking()
            .Where(f => f.LabId == labId)
            .ProjectTo<Flag>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(f => f.Id, cancellationToken);
        foreach(var fs in foundFlags)
            fs.FlagCode = flags[fs.FlagId].Code;

        // Build scoreboard
        var scoreboard = new UserDashboard
        {
            LabId = labId,
            CurrentLab = labs.First(l => l.LabId == labId),
            Labs = labs,
            LabExecutionStatus = ScoreboardUtilities.GetLabExecutionStatus(now, labExecution),
            LabExecution = mapper.Map<LabExecution>(labExecution),
            FoundFlagsCount = foundFlagsGrouped.Count,
            ValidFoundFlagsCount = foundFlagsGrouped.Count(ff => ff.Any(ffs => ffs.Valid)),
            HasFoundAllFlags = foundFlagsGrouped.Count == flags.Count(f => !f.Value.IsBounty),
            Exercises = new List<UserScoreboardExerciseEntry>(),
            GroupMembers = groupMembers,
            Flags = foundFlags,
            PassAsGroupEnabled = await configurationService.PassAsGroup.GetAsync(cancellationToken)
        };

        // Check exercise submissions
        int mandatoryExerciseCount = 0;
        int passedMandatoryExercisesCount = 0;
        int passedOptionalExercisesCount = 0;
        foreach(var exercise in exercises)
        {
            if(exercise.IsMandatory)
                ++mandatoryExerciseCount;

            if(exerciseSubmissions.TryGetValue(exercise.Id, out var submissions))
            {
                var (groupMemberHasPassed, points, validTries) = ScoreboardUtilities.CalculateExercisePoints(exercise, submissions, labExecution);

                // If passing as group is disabled, do another check whether this user has a valid passing submission
                bool passed = groupMemberHasPassed;
                if(!passAsGroup)
                    passed = submissions.Any(s => s.ExercisePassed && s.UserId == userId && labExecution.Start <= s.SubmissionTime && s.SubmissionTime < labExecution.End);

                scoreboard.Exercises.Add(new UserScoreboardExerciseEntry
                {
                    Exercise = exercise,
                    Tries = submissions.Count,
                    ValidTries = validTries,
                    Passed = passed,
                    GroupMemberHasPassed = groupMemberHasPassed,
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
                scoreboard.Exercises.Add(new UserScoreboardExerciseEntry
                {
                    Exercise = exercise,
                    Tries = 0,
                    ValidTries = 0,
                    Passed = false,
                    GroupMemberHasPassed = false,
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