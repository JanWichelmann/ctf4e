using System;
using System.Collections.Generic;
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

namespace Ctf4e.Server.Services;

public interface IAdminScoreboardService
{
    Task<AdminScoreboardStatistics> GetStatisticsAsync(int labId, int slotId, CancellationToken cancellationToken);
    Task<AdminScoreboardOverview> GetOverviewAsync(int labId, int slotId, CancellationToken cancellationToken);
    Task<AdminScoreboardDetails> GetDetailsAsync(int labId, int? groupId, int? userId, CancellationToken cancellationToken);
}

public class AdminScoreboardService(
    CtfDbContext dbContext,
    IMapper mapper,
    IConfigurationService configurationService,
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

        bool passAsGroup = await configurationService.PassAsGroup.GetAsync(cancellationToken);

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

        // Get flag state
        var flags = await scoreboardUtilities.GetFlagStateAsync(dbContext, labId, cancellationToken);
        result.Flags = mapper.Map<List<AdminScoreboardStatistics.FlagListEntry>>(flags);

        return result;
    }

    public async Task<AdminScoreboardOverview> GetOverviewAsync(int labId, int slotId, CancellationToken cancellationToken)
    {
        // Consistent time
        var now = DateTime.Now;

        bool passAsGroup = await configurationService.PassAsGroup.GetAsync(cancellationToken);

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
                         && es.User.Group.LabExecutions.Any(le => le.LabId == labId && le.Start <= es.SubmissionTime && es.SubmissionTime < le.End))
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

        // Get flag state
        var flags = await scoreboardUtilities.GetFlagStateAsync(dbContext, labId, cancellationToken);

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
                    Id = user.GroupId.Value,
                    Status = ScoreboardUtilities.GetLabExecutionStatus(now, groupLabExecution),
                    Members = []
                };
                result.GroupEntries.Add(groupEntry);
                groupEntryLookup.Add(user.GroupId.Value, groupEntry);
            }

            // Initialize user entry
            var userEntry = new AdminScoreboardOverview.UserEntry
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
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
                .Select(m => passedExerciseSubmissionsByUser.GetValueOrDefault(m.Id, []))
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
                .Select(m => flagSubmissionsByUser.GetValueOrDefault(m.Id, []))
                .Aggregate(new HashSet<int>(), (acc, set) =>
                {
                    acc.UnionWith(set);
                    return acc;
                });
            int foundFlagsCount = flags.Count(f => mergedFlagSubmissions.Contains(f.Id));

            // Fill in the rest of the group entry
            groupEntry.Name = group.DisplayName;
            groupEntry.HasPassed = passedMandatoryExercisesCount == mandatoryExercisesCount;
            groupEntry.PassedMandatoryExercisesCount = passedMandatoryExercisesCount;
            groupEntry.PassedOptionalExercisesCount = passedOptionalExercisesCount;
            groupEntry.FoundFlagsCount = foundFlagsCount;
        }

        result.UserEntries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.InvariantCultureIgnoreCase));
        result.GroupEntries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase));

        return result;
    }

    public async Task<AdminScoreboardDetails> GetDetailsAsync(int labId, int? groupId, int? userId, CancellationToken cancellationToken)
    {
        if(groupId == null && userId == null)
            throw new ArgumentException("Either groupId or userId must be set");

        // Consistent time
        var now = DateTime.Now;

        bool passAsGroup = await configurationService.PassAsGroup.GetAsync(cancellationToken);

        // Retrieve group and all relevant users
        // For simplicity, we use the same code paths for group and user dashboards, and filter the submission lists later in the view.
        // Only exception are the passed/points calculations.
        Group group;
        if(userId != null)
        {
            group = await dbContext.Groups.AsNoTracking()
                .Where(g => g.Members.Any(u => u.Id == userId.Value))
                .ProjectTo<Group>(mapper.ConfigurationProvider, g => g.Members)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            group = await dbContext.Groups.AsNoTracking()
                .Where(g => g.Id == groupId)
                .ProjectTo<Group>(mapper.ConfigurationProvider, g => g.Members)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var userNameLookup = group.Members.ToDictionary(u => u.Id, u => u.DisplayName);

        var labExecution = await dbContext.LabExecutions.AsNoTracking()
            .Where(le => le.LabId == labId && le.GroupId == group.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var exercises = await dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<Exercise>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        // Get all exercise submissions
        var exerciseSubmissions = await dbContext.ExerciseSubmissions.AsNoTracking()
            .Where(es => es.Exercise.LabId == labId && es.User.GroupId == group.Id)
            .OrderBy(es => es.SubmissionTime)
            .ProjectTo<ExerciseSubmission>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        var exerciseSubmissionsByExercise = exerciseSubmissions
            .GroupBy(es => es.ExerciseId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all submissions
        var flagSubmissions = await dbContext.FlagSubmissions.AsNoTracking()
            .Where(fs => fs.Flag.LabId == labId && fs.User.GroupId == group.Id)
            .OrderBy(fs => fs.SubmissionTime)
            .ProjectTo<FlagSubmission>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        var flagSubmissionsByFlag = flagSubmissions
            .GroupBy(fs => fs.FlagId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get flag state
        var flags = await scoreboardUtilities.GetFlagStateAsync(dbContext, labId, cancellationToken);

        var result = new AdminScoreboardDetails
        {
            LabId = labId,
            SlotId = group.SlotId,
            GroupId = group.Id,
            GroupName = group.DisplayName,
            UserId = userId,
            UserName = userId != null ? userNameLookup[userId.Value] : null,
            GroupMembers = group.Members.Select(u => (u.Id, u.DisplayName)).ToList(),
            PassAsGroup = passAsGroup,
            HasPassed = false,
            Status = ScoreboardUtilities.GetLabExecutionStatus(now, labExecution),
            Exercises = [],
            Flags = []
        };

        bool labPassed = true;
        foreach(var exercise in exercises)
        {
            if(!exerciseSubmissionsByExercise.TryGetValue(exercise.Id, out var submissions))
                submissions = [];

            // If we view a single user, only check their own submissions
            // Note: This function only provides data for the user/group dashboard, which shows details about individual exercises and submissions.
            //       An accurate list over who passed the lab and who did not is provided by the overview routes. So we can actually produce "wrong"
            //       passed/not passed results in user mode, where pass-as-group is temporarily ignored.
            bool passed;
            bool groupMemberHasPassed;
            int points;
            int validTries;
            if(userId != null)
            {
                var filteredSubmissions = submissions.Where(s => s.UserId == userId);
                
                (passed, points, validTries) = ScoreboardUtilities.CalculateExercisePoints(exercise, filteredSubmissions, labExecution);
                groupMemberHasPassed = passed;
            }
            else
            {
                (groupMemberHasPassed, points, validTries) = ScoreboardUtilities.CalculateExercisePoints(exercise, submissions, labExecution);

                // If passing as group is disabled, check whether _all_ members have passed
                passed = groupMemberHasPassed;
                if(!passAsGroup && labExecution != null) // If labExecution is null, passed is already false and we don't need to check
                {
                    passed = group.Members.All(u => submissions.Any(s => s.ExercisePassed
                                                                         && s.UserId == u.Id
                                                                         && labExecution.Start <= s.SubmissionTime
                                                                         && s.SubmissionTime < labExecution.End));
                }
            }

            if(exercise.IsMandatory && !passed)
                labPassed = false;

            var exerciseEntry = new AdminScoreboardDetails.ExerciseEntry
            {
                Id = exercise.Id,
                ExerciseName = exercise.Name,
                IsMandatory = exercise.IsMandatory,
                BasePoints = exercise.BasePoints,
                PenaltyPoints = exercise.PenaltyPoints,
                Tries = submissions.Count,
                ValidTries = validTries,
                Passed = groupMemberHasPassed,
                Points = points,
                Submissions = submissions
                    .Select(s => new AdminScoreboardDetails.ExerciseSubmissionEntry
                    {
                        Id = s.Id,
                        UserId = s.UserId,
                        UserName = userNameLookup[s.UserId],
                        Solved = s.ExercisePassed,
                        SubmissionTime = s.SubmissionTime,
                        Valid = labExecution != null && labExecution.Start <= s.SubmissionTime && s.SubmissionTime < labExecution.End,
                        Weight = s.Weight,
                        CreatedByAdmin = s.CreatedByAdmin
                    })
                    .ToList()
            };

            result.Exercises.Add(exerciseEntry);
        }

        result.HasPassed = labPassed;

        foreach(var flag in flags)
        {
            if(!flagSubmissionsByFlag.TryGetValue(flag.Id, out var submissions))
                submissions = [];

            var flagEntry = new AdminScoreboardDetails.FlagEntry
            {
                Id = flag.Id,
                Description = flag.Description,
                IsBounty = flag.IsBounty,
                Submitted = submissions.Any(s => userId == null ? group.Members.Any(m => m.Id == s.UserId) : s.UserId == userId),
                Valid = labExecution != null && submissions.Any(s => labExecution.Start <= s.SubmissionTime && s.SubmissionTime < labExecution.End),
                Points = flag.CurrentPoints,
                Submissions = submissions
                    .Select(s => new AdminScoreboardDetails.FlagSubmissionEntry
                    {
                        UserId = s.UserId,
                        UserName = userNameLookup[s.UserId],
                        Valid = labExecution != null && labExecution.Start <= s.SubmissionTime && s.SubmissionTime < labExecution.End,
                        SubmissionTime = s.SubmissionTime
                    })
                    .ToList()
            };

            result.Flags.Add(flagEntry);
        }

        return result;
    }

    public static void RegisterMappings(Profile profile)
    {
        profile.CreateMap<ScoreboardUtilities.FlagState, AdminScoreboardStatistics.FlagListEntry>();
        profile.CreateMap<ExerciseEntity, AdminScoreboardStatistics.ExerciseListEntry>();
    }
}