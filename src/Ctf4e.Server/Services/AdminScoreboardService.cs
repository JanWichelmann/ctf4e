using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using Ctf4e.Server.Models;
using Ctf4e.Server.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Ctf4e.Server.Services;

public interface IAdminScoreboardService
{
    Task<AdminScoreboardStatistics> GetStatisticsAsync(int labId, int slotId, CancellationToken cancellationToken);
    Task<AdminScoreboardOverview> GetOverviewAsync(int labId, int slotId, CancellationToken cancellationToken);
}

public class AdminScoreboardService(
    CtfDbContext dbContext,
    IMapper mapper,
    IConfigurationService configurationService,
    IMemoryCache cache,
    ScoreboardUtilities scoreboardUtilities)
    : IAdminScoreboardService
{
    public async Task<AdminScoreboardStatistics> GetStatisticsAsync(int labId, int slotId, CancellationToken cancellationToken)
    {
        AdminScoreboardStatistics result = new()
        {
            LabId = labId,
            SlotId = slotId
        };

        bool passAsGroup = await configurationService.GetPassAsGroupAsync(cancellationToken);

        var groupIdLookup = await dbContext.Users.AsNoTracking()
            .Where(u => u.GroupId != null && u.Group.SlotId == slotId)
            .ToDictionaryAsync(u => u.Id, u => u.GroupId, cancellationToken);

        result.Exercises = await dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<AdminScoreboardStatistics.ExerciseListEntry>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        // Get all valid, passed exercise submissions for this lab and slot
        var passedExerciseSubmissions = await dbContext.ExerciseSubmissions.AsNoTracking()
            .Where(es => es.ExercisePassed
                         && es.Exercise.LabId == labId
                         && !es.User.IsTutor
                         && es.User.GroupId != null
                         && es.User.Group.SlotId == slotId
                         && es.User.Group.LabExecutions.Any(le => le.Start <= es.SubmissionTime && es.SubmissionTime < le.End))
            .OrderBy(es => es.ExerciseId)
            .ThenBy(es => es.SubmissionTime)
            .ProjectTo<ExerciseSubmission>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        var passedSubmissionsByExercise = passedExerciseSubmissions
            .GroupBy(es => es.ExerciseId)
            .ToDictionary(g => g.Key);

        // Compute how many users/groups have passed each exercise
        foreach(var e in result.Exercises)
        {
            if(!passedSubmissionsByExercise.TryGetValue(e.Id, out var passedSubmissions))
                continue;

            if(passAsGroup)
            {
                e.PassedCount = passedSubmissions
                    .Select(es => groupIdLookup[es.UserId])
                    .Distinct()
                    .Count();
            }
            else
            {
                e.PassedCount = passedSubmissions
                    .Select(es => es.UserId)
                    .Distinct()
                    .Count();
            }
        }

        // Get flag submission counts and compute resulting points
        // Flags are calculated over all slots
        result.Flags = await dbContext.Database.SqlQuery<AdminScoreboardStatistics.FlagListEntry>(
                $"""
                 SELECT f.Id, f.Description, f.BasePoints, f.IsBounty, COUNT(DISTINCT g.`Id`) AS 'SubmissionCount'
                 FROM `Flags` f
                 LEFT JOIN(
                   `FlagSubmissions` s
                   INNER JOIN `Users` u ON u.`Id` = s.`UserId` AND u.`IsTutor` = 0
                   INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                 ) ON s.`FlagId` = f.`Id`
                   AND g.`ShowInScoreboard` = 1
                   AND EXISTS(
                     SELECT 1
                     FROM `LabExecutions` le
                     WHERE le.`GroupId` = g.`Id`
                       AND le.`LabId` = @labId
                       AND le.`Start` <= s.`SubmissionTime`
                       AND s.`SubmissionTime` < le.`End`
                   )
                 WHERE f.`LabId` = {labId}
                 GROUP BY f.`Id`
                 """)
            .ToListAsync(cancellationToken);
        foreach(var f in result.Flags)
            f.CurrentPoints = scoreboardUtilities.CalculateFlagPoints(f.BasePoints, f.IsBounty, f.SubmissionCount);

        return result;
    }

    public async Task<AdminScoreboardOverview> GetOverviewAsync(int labId, int slotId, CancellationToken cancellationToken)
    {
        // Consistent time
        var now = DateTime.Now;

        bool passAsGroup = await configurationService.GetPassAsGroupAsync(cancellationToken);

        var labExecutions = await dbContext.LabExecutions.AsNoTracking()
            .Where(l => l.LabId == labId && l.Group.SlotId == slotId)
            .ToDictionaryAsync(l => l.GroupId, cancellationToken); // Each group ID can only appear once, since it is part of the primary key

        var users = await dbContext.Users.AsNoTracking()
            .Where(u => u.GroupId != null && u.Group.SlotId == slotId)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        var groups = await dbContext.Groups.AsNoTracking()
            .Where(g => g.SlotId == slotId)
            .OrderBy(g => g.DisplayName)
            .ProjectTo<Group>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var exercises = await dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<Exercise>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        // Get all valid, passed exercise submissions per user
        var passedExerciseSubmissions = await dbContext.ExerciseSubmissions.AsNoTracking()
            .Where(es => es.ExercisePassed
                         && es.Exercise.LabId == labId
                         && es.User.GroupId != null
                         && es.User.Group.SlotId == slotId
                         && es.User.Group.LabExecutions.Any(le => le.Start <= es.SubmissionTime && es.SubmissionTime < le.End))
            .OrderBy(es => es.SubmissionTime)
            .ProjectTo<ExerciseSubmission>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        var passedExerciseSubmissionsByUser = passedExerciseSubmissions
            .GroupBy(es => es.UserId) // This needs to be done in memory (no aggregation)
            .ToDictionary(es => es.Key, es => es.Select(e => e.ExerciseId).ToHashSet());

        // Get all valid flag submissions per user
        var validFlagSubmissions = await dbContext.FlagSubmissions.AsNoTracking()
            .Where(fs => fs.Flag.LabId == labId
                         && fs.User.GroupId != null
                         && fs.User.Group.SlotId == slotId
                         && fs.User.Group.LabExecutions.Any(le => le.Start <= fs.SubmissionTime && fs.SubmissionTime < le.End))
            .ProjectTo<FlagSubmission>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        var flagSubmissionsByUser = validFlagSubmissions
            .GroupBy(fs => fs.UserId) // This needs to be done in memory (no aggregation)
            .ToDictionary(fs => fs.Key, fs => fs.Select(f => f.FlagId).ToHashSet());

        // Get flag submission counts and compute resulting points
        // Flags are calculated over all slots
        var flags = await dbContext.Database.SqlQuery<FlagState>(
                $"""
                 SELECT f.Id, f.BasePoints, f.IsBounty, COUNT(DISTINCT g.`Id`) AS 'SubmissionCount'
                 FROM `Flags` f
                 LEFT JOIN(
                   `FlagSubmissions` s
                   INNER JOIN `Users` u ON u.`Id` = s.`UserId` AND u.`IsTutor` = 0
                   INNER JOIN `Groups` g ON g.`Id` = u.`GroupId`
                 ) ON s.`FlagId` = f.`Id`
                   AND g.`ShowInScoreboard` = 1
                   AND EXISTS(
                     SELECT 1
                     FROM `LabExecutions` le
                     WHERE le.`GroupId` = g.`Id`
                       AND le.`LabId` = @labId
                       AND le.`Start` <= s.`SubmissionTime`
                       AND s.`SubmissionTime` < le.`End`
                   )
                 WHERE f.`LabId` = {labId}
                 GROUP BY f.`Id`
                 """)
            .ToListAsync(cancellationToken);
        foreach(var f in flags)
            f.CurrentPoints = scoreboardUtilities.CalculateFlagPoints(f.BasePoints, f.IsBounty, f.SubmissionCount);

        int mandatoryExercisesCount = exercises.Count(e => e.IsMandatory);
        var result = new AdminScoreboardOverview
        {
            LabId = labId,
            SlotId = slotId,
            PassAsGroup = passAsGroup,
            MandatoryExercisesCount = mandatoryExercisesCount,
            OptionalExercisesCount = exercises.Count - mandatoryExercisesCount,
            FlagCount = flags.Count,
            UserEntries = [],
            GroupEntries = []
        };

        // We first collect per-user stats, and then merge them into per-group stats
        Dictionary<int, AdminScoreboardOverview.GroupEntry> groupEntryLookup = new();

        // For each user, collect passed exercises and submitted flags
        foreach(var user in users)
        {
            labExecutions.TryGetValue(user.GroupId!.Value, out var groupLabExecution);

            // Count passed exercises
            if(!passedExerciseSubmissionsByUser.TryGetValue(user.Id, out var userExerciseSubmissions))
                userExerciseSubmissions = [];
            int passedMandatoryExercisesCount = 0;
            int passedOptionalExercisesCount = 0;
            foreach(var exercise in exercises)
            {
                // Did the user pass this exercise?
                if(!userExerciseSubmissions.Contains(exercise.Id))
                    continue;

                // Yes, count it
                if(exercise.IsMandatory)
                    ++passedMandatoryExercisesCount;
                else
                    ++passedOptionalExercisesCount;
            }

            // Count submitted flags
            if(!flagSubmissionsByUser.TryGetValue(user.Id, out var userFlagSubmissions))
                userFlagSubmissions = [];
            int foundFlagsCount = flags.Count(f => userFlagSubmissions.Contains(f.Id));

            // Find associated group entry, or create a new one
            if(!groupEntryLookup.TryGetValue(user.GroupId.Value, out var groupEntry))
            {
                // We fill in the other fields later
                groupEntry = new AdminScoreboardOverview.GroupEntry
                {
                    GroupId = user.GroupId.Value,
                    Status = ScoreboardUtilities.GetLabExecutionStatus(now, groupLabExecution),
                    Members = []
                };
                result.GroupEntries.Add(groupEntry);
                groupEntryLookup.Add(user.GroupId.Value, groupEntry);
            }

            // Initialize user entry
            var userEntry = new AdminScoreboardOverview.UserEntry
            {
                UserId = user.Id,
                UserName = user.DisplayName,
                Status = ScoreboardUtilities.GetLabExecutionStatus(now, groupLabExecution),
                HasPassed = passedMandatoryExercisesCount == mandatoryExercisesCount,
                PassedMandatoryExercisesCount = passedMandatoryExercisesCount,
                PassedOptionalExercisesCount = passedOptionalExercisesCount,
                FoundFlagsCount = foundFlagsCount,
                Group = groupEntry
            };
            groupEntry.Members.Add(userEntry);
            result.UserEntries.Add(userEntry);
        }

        // Now merge user stats into group stats
        foreach(var group in groups)
        {
            // If the group did not come up during user processing, skip it
            if(!groupEntryLookup.TryGetValue(group.Id, out var groupEntry))
                continue;

            // Count passed exercises
            // Merge sets of passed exercises from all members
            var mergedExerciseSubmissions = groupEntry.Members
                .Select(m => passedExerciseSubmissionsByUser.GetValueOrDefault(m.UserId, []))
                .Aggregate(new HashSet<int>(), (acc, set) =>
                {
                    acc.UnionWith(set);
                    return acc;
                });
            int passedMandatoryExercisesCount = 0;
            int passedOptionalExercisesCount = 0;
            foreach(var exercise in exercises)
            {
                if(!mergedExerciseSubmissions.Contains(exercise.Id))
                    continue;

                // Yes, count it
                if(exercise.IsMandatory)
                    ++passedMandatoryExercisesCount;
                else
                    ++passedOptionalExercisesCount;
            }
            
            // Count submitted flags
            // Merge sets of submitted flags from all members
            var mergedFlagSubmissions = groupEntry.Members
                .Select(m => flagSubmissionsByUser.GetValueOrDefault(m.UserId, []))
                .Aggregate(new HashSet<int>(), (acc, set) =>
                {
                    acc.UnionWith(set);
                    return acc;
                });
            int foundFlagsCount = flags.Count(f => mergedFlagSubmissions.Contains(f.Id));
            
            // Fill in the rest of the group entry
            groupEntry.GroupName = group.DisplayName;
            groupEntry.HasPassed = passedMandatoryExercisesCount == mandatoryExercisesCount;
            groupEntry.PassedMandatoryExercisesCount = passedMandatoryExercisesCount;
            groupEntry.PassedOptionalExercisesCount = passedOptionalExercisesCount;
            groupEntry.FoundFlagsCount = foundFlagsCount;
        }
        
        result.UserEntries.Sort((a, b) => string.Compare(a.UserName, b.UserName, StringComparison.InvariantCultureIgnoreCase));
        result.GroupEntries.Sort((a, b) => string.Compare(a.GroupName, b.GroupName, StringComparison.InvariantCultureIgnoreCase));

        return result;
    }

    public static void RegisterMappings(Profile profile)
    {
        profile.CreateMap<ExerciseEntity, AdminScoreboardStatistics.ExerciseListEntry>();
    }

    private class FlagState
    {
        public int Id { get; set; }
        public int BasePoints { get; set; }
        public bool IsBounty { get; set; }
        public int SubmissionCount { get; set; }

        [NotMapped]
        public int CurrentPoints { get; set; }
    }
}