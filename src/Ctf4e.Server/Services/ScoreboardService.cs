using System;
using System.Collections.Generic;
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
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services
{
    public interface IScoreboardService
    {
        Task<AdminScoreboard> GetAdminScoreboardAsync(int labId, int slotId, CancellationToken cancellationToken = default);
        Task<Scoreboard> GetFullScoreboardAsync(CancellationToken cancellationToken = default);
        Task<Scoreboard> GetLabScoreboardAsync(int labId, CancellationToken cancellationToken = default);
        Task<GroupScoreboard> GetGroupScoreboardAsync(int groupId, int labId, CancellationToken cancellationToken = default);
    }

    public class ScoreboardService : IScoreboardService
    {
        private readonly CtfDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly IFlagPointService _flagPointService;
        private readonly IConfigurationService _configurationService;
        private readonly IScoreboardCacheService _scoreboardCacheService;

        public ScoreboardService(CtfDbContext dbContext, IMapper mapper, IFlagPointService flagPointService, IConfigurationService configurationService, IScoreboardCacheService scoreboardCacheService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _flagPointService = flagPointService ?? throw new ArgumentNullException(nameof(flagPointService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _scoreboardCacheService = scoreboardCacheService ?? throw new ArgumentNullException(nameof(scoreboardCacheService));

            _flagPointService = flagPointService;
        }

        public async Task<AdminScoreboard> GetAdminScoreboardAsync(int labId, int slotId, CancellationToken cancellationToken = default)
        {
            // Consistent time
            var now = DateTime.Now;

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

            var groups = await _dbContext.Groups.AsNoTracking()
                .Where(g => g.SlotId == slotId)
                .OrderBy(g => g.DisplayName)
                .ToListAsync(cancellationToken);

            var exercises = await _dbContext.Exercises.AsNoTracking()
                .Where(e => e.LabId == labId)
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var exerciseSubmissionsUngrouped = await _dbContext.ExerciseSubmissions.AsNoTracking()
                .Where(e => e.Exercise.LabId == labId && e.Group.SlotId == slotId)
                .OrderBy(e => e.ExerciseId)
                .ThenBy(e => e.SubmissionTime)
                .ProjectTo<ExerciseSubmission>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
            var exerciseSubmissions = exerciseSubmissionsUngrouped
                .GroupBy(e => e.GroupId) // This needs to be done in memory (no aggregation)
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
                .Where(f => f.Flag.LabId == labId && f.Group.SlotId == slotId)
                .ProjectTo<FlagSubmission>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
            var flagSubmissions = flagSubmissionsUngrouped
                .GroupBy(f => f.GroupId) // This needs to be done in memory (no aggregation)
                .ToDictionary(
                    f => f.Key,
                    f => f.GroupBy(fs => fs.FlagId)
                        .ToDictionary(fs => fs.Key, fs => fs.First())); // There can only be one submission per flag and group

            // Compute submission counts and current points of all flags over all slots
            var scoreboardFlagStatus = await _dbContext.Flags.AsNoTracking()
                .Where(f => f.LabId == labId)
                .Select(f => new AdminScoreboardFlagEntry
                {
                    Flag = _mapper.Map<Flag>(f),
                    SubmissionCount = f.Submissions
                        .Count(fs => fs.Group.ShowInScoreboard && fs.Group.LabExecutions.Any(le => le.LabId == labId && le.PreStart <= fs.SubmissionTime && fs.SubmissionTime < le.End))
                }).ToDictionaryAsync(f => f.Flag.Id, cancellationToken);
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
                GroupEntries = new List<AdminScoreboardGroupEntry>()
            };

            // For each group, collect exercise and flag data
            foreach(var group in groups)
            {
                labExecutions.TryGetValue(group.Id, out var groupLabExecution);

                var groupEntry = new AdminScoreboardGroupEntry
                {
                    GroupId = group.Id,
                    GroupName = group.DisplayName,
                    Status = LabExecutionToStatus(now, groupLabExecution),
                    Exercises = new List<ScoreboardGroupExerciseEntry>(),
                    Flags = new List<AdminScoreboardGroupFlagEntry>()
                };

                // Exercises
                exerciseSubmissions.TryGetValue(group.Id, out var groupExerciseSubmissions);
                int passedMandatoryExercisesCount = 0;
                int passedOptionalExercisesCount = 0;
                foreach(var exercise in exercises)
                {
                    if(groupExerciseSubmissions != null && groupExerciseSubmissions.ContainsKey(exercise.Id))
                    {
                        var submissions = groupExerciseSubmissions[exercise.Id];

                        var (passed, points, validTries) = CalculateExerciseStatus(exercise, submissions, groupLabExecution);

                        groupEntry.Exercises.Add(new ScoreboardGroupExerciseEntry
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
                        groupEntry.Exercises.Add(new ScoreboardGroupExerciseEntry
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
                flagSubmissions.TryGetValue(group.Id, out var groupFlagSubmissions);
                int foundFlagsCount = 0;
                foreach(var flag in flags)
                {
                    if(groupFlagSubmissions != null && groupFlagSubmissions.ContainsKey(flag.Id))
                    {
                        var submission = groupFlagSubmissions[flag.Id];

                        var valid = CalculateFlagStatus(submission, groupLabExecution);

                        groupEntry.Flags.Add(new AdminScoreboardGroupFlagEntry
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
                        groupEntry.Flags.Add(new AdminScoreboardGroupFlagEntry
                        {
                            Flag = flag,
                            Submitted = false,
                            Valid = false,
                            SubmissionTime = DateTime.MinValue
                        });
                    }
                }

                // General group statistics
                groupEntry.PassedMandatoryExercisesCount = passedMandatoryExercisesCount;
                groupEntry.HasPassed = passedMandatoryExercisesCount == mandatoryExercisesCount;
                groupEntry.PassedOptionalExercisesCount = passedOptionalExercisesCount;
                groupEntry.FoundFlagsCount = foundFlagsCount;
                adminScoreboard.GroupEntries.Add(groupEntry);
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

            // Split groups enabled?
            bool splitGroups = await _configurationService.GetCreateSplitGroupsAsync(cancellationToken);

            // Get flag point limits
            var flagPointLimits = await _dbContext.Labs.AsNoTracking()
                .Select(l => new {l.Id, l.MaxFlagPoints})
                .ToDictionaryAsync(l => l.Id, l => l.MaxFlagPoints, cancellationToken);

            // Get list of exercises
            var exercises = await _dbContext.Exercises.AsNoTracking()
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToDictionaryAsync(e => e.Id, cancellationToken);

            // Initialize scoreboard entries with basic group information
            var scoreboardEntries = await _dbContext.Groups.AsNoTracking()
                .Where(g => g.ShowInScoreboard)
                .Select(g => new ScoreboardEntry
                {
                    GroupId = g.Id,
                    GroupName = g.DisplayName,
                    SlotId = g.SlotId,
                    LastExerciseSubmissionTime = g.ExerciseSubmissions
                        .Where(s => s.ExercisePassed && g.LabExecutions
                            .Any(le => le.LabId == s.Exercise.LabId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
                        .Max(s => s.SubmissionTime),
                    LastFlagSubmissionTime = g.FlagSubmissions
                        .Where(s => g.LabExecutions
                            .Any(le => le.LabId == s.Flag.LabId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
                        .Max(s => s.SubmissionTime),
                }).ToListAsync(cancellationToken);

            // Get valid submission counts for passed exercises
            var validExerciseSubmissionsUngrouped = await _dbContext.ExerciseSubmissions.AsNoTracking()
                .Where(s => s.Group.LabExecutions
                                .Any(le => le.LabId == s.Exercise.LabId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End)
                            && s.SubmissionTime <= s.Group.ExerciseSubmissions
                                .FirstOrDefault(s2 => s2.ExerciseId == s.ExerciseId && s2.ExercisePassed).SubmissionTime)
                .GroupBy(s => new {s.GroupId, s.ExerciseId})
                .Select(s => new {s.Key.GroupId, s.Key.ExerciseId, WeightSum = s.Sum(ss => ss.Weight)}) // The passed submission always has weight 1
                .ToListAsync(cancellationToken);
            var validExerciseSubmissions = validExerciseSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            // Compute submission counts and current points for all flags
            var flags = await _dbContext.Flags.AsNoTracking()
                .Select(f => new ScoreboardFlagEntry
                {
                    Flag = _mapper.Map<Flag>(f),
                    SubmissionCount = f.Submissions
                        .Count(fs => fs.Group.LabExecutions.Any(le => le.LabId == f.LabId && le.PreStart <= fs.SubmissionTime && fs.SubmissionTime < le.End))
                }).ToDictionaryAsync(f => f.Flag.Id, cancellationToken);
            foreach(var f in flags)
                f.Value.CurrentPoints = CalculateFlagPoints(f.Value.Flag, f.Value.SubmissionCount);

            // Get valid submissions for flags
            var validFlagSubmissionsUngrouped = await _dbContext.FlagSubmissions.AsNoTracking()
                .Where(s => s.Group.ShowInScoreboard
                            && s.Group.LabExecutions
                                .Any(le => le.LabId == s.Flag.LabId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
                .Select(s => new {s.GroupId, s.FlagId, s.Flag.LabId})
                .ToListAsync(cancellationToken);
            var validFlagSubmissions = validFlagSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

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

            // Merge entries with same group names
            if(splitGroups)
                scoreboardEntries = MergeSplitGroups(scoreboardEntries);

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

            var scoreboard = new Scoreboard
            {
                ContainsSplitGroups = splitGroups,
                AllLabs = true,
                MaximumEntryCount = await _configurationService.GetScoreboardEntryCountAsync(cancellationToken),
                Entries = scoreboardEntries,
                Flags = flags,
                ValidUntil = now.AddSeconds(await _configurationService.GetScoreboardCachedSecondsAsync(cancellationToken))
            };

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

            // Split groups enabled?
            bool splitGroups = await _configurationService.GetCreateSplitGroupsAsync(cancellationToken);

            // Get list of exercises
            var exercises = await _dbContext.Exercises.AsNoTracking()
                .Where(e => e.LabId == labId)
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToDictionaryAsync(e => e.Id, cancellationToken);
            if(!exercises.Any())
                return null; // No scoreboard for empty labs

            // Initialize scoreboard entries with basic group information
            var scoreboardEntries = await _dbContext.Groups.AsNoTracking()
                .Where(g => g.ShowInScoreboard)
                .OrderBy(g => g.DisplayName)
                .Select(g => new ScoreboardEntry
                {
                    GroupId = g.Id,
                    GroupName = g.DisplayName,
                    SlotId = g.SlotId,
                    LastExerciseSubmissionTime = g.ExerciseSubmissions
                        .Where(s => s.ExercisePassed && g.LabExecutions
                            .Any(le => le.LabId == labId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
                        .Max(s => s.SubmissionTime),
                    LastFlagSubmissionTime = g.FlagSubmissions
                        .Where(s => g.LabExecutions
                            .Any(le => le.LabId == labId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
                        .Max(s => s.SubmissionTime),
                }).ToListAsync(cancellationToken);

            // Get valid submission counts for passed exercises
            var validExerciseSubmissionsUngrouped = await _dbContext.ExerciseSubmissions.AsNoTracking()
                .Where(s => s.Exercise.LabId == labId
                            && s.Group.LabExecutions
                                .Any(le => le.LabId == labId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End)
                            && s.SubmissionTime <= s.Group.ExerciseSubmissions
                                .FirstOrDefault(s2 => s2.ExerciseId == s.ExerciseId && s2.ExercisePassed).SubmissionTime)
                .GroupBy(s => new {s.GroupId, s.ExerciseId})
                .Select(s => new {s.Key.GroupId, s.Key.ExerciseId, WeightSum = s.Sum(ss => ss.Weight)}) // The passed submission always has weight 1
                .ToListAsync(cancellationToken);
            var validExerciseSubmissions = validExerciseSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            // Get valid submissions for flags
            var validFlagSubmissionsUngrouped = await _dbContext.FlagSubmissions.AsNoTracking()
                .Where(s => s.Group.ShowInScoreboard
                            && s.Flag.LabId == labId
                            && s.Group.LabExecutions
                                .Any(le => le.LabId == labId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
                .Select(s => new {s.GroupId, s.FlagId})
                .ToListAsync(cancellationToken);
            var validFlagSubmissions = validFlagSubmissionsUngrouped
                .GroupBy(s => s.GroupId) // This must be an in-memory operation
                .ToDictionary(s => s.Key);

            // Compute submission counts and current points for all flags
            var flags = await _dbContext.Flags.AsNoTracking()
                .Where(f => f.LabId == labId)
                .Select(f => new ScoreboardFlagEntry
                {
                    Flag = _mapper.Map<Flag>(f),
                    SubmissionCount = f.Submissions
                        .Count(fs => fs.Group.LabExecutions.Any(le => le.LabId == labId && le.PreStart <= fs.SubmissionTime && fs.SubmissionTime < le.End))
                }).ToDictionaryAsync(f => f.Flag.Id, cancellationToken);
            foreach(var f in flags)
                f.Value.CurrentPoints = CalculateFlagPoints(f.Value.Flag, f.Value.SubmissionCount);

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

            // Merge entries with same group names
            if(splitGroups)
                scoreboardEntries = MergeSplitGroups(scoreboardEntries);

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

            string labName = await _dbContext.Labs.AsNoTracking()
                .Where(l => l.Id == labId)
                .Select(l => l.Name)
                .FirstAsync(cancellationToken);
            var scoreboard = new Scoreboard
            {
                LabId = labId,
                CurrentLabName = labName,
                AllLabs = false,
                ContainsSplitGroups = splitGroups,
                MaximumEntryCount = await _configurationService.GetScoreboardEntryCountAsync(cancellationToken),
                Entries = scoreboardEntries,
                Flags = flags,
                ValidUntil = now.AddSeconds(await _configurationService.GetScoreboardCachedSecondsAsync(cancellationToken))
            };
            _scoreboardCacheService.SetLabScoreboard(labId, scoreboard);
            return scoreboard;
        }

        /// <summary>
        /// Merges scoreboard entries of split groups into single entries.
        /// </summary>
        /// <param name="scoreboardEntries">The list of group scoreboard entries. This list is assumed to be sorted by group names.</param>
        /// <returns></returns>
        private List<ScoreboardEntry> MergeSplitGroups(List<ScoreboardEntry> scoreboardEntries)
        {
            var scoreboardEntriesMerged = new List<ScoreboardEntry>();

            // The list is sorted by group names, so a linear approach is safe here
            string lastGroupName = "";
            ScoreboardEntry lastGroupEntry = null; // This variable is guaranteed to be initialized when used
            foreach(var entry in scoreboardEntries)
            {
                // Parse group name
                if(entry.GroupName.Length <= 4 || entry.GroupName[^3] != '(' || !char.IsDigit(entry.GroupName[^2]) || entry.GroupName[^1] != ')')
                {
                    scoreboardEntriesMerged.Add(entry);
                    continue;
                }

                string groupName = entry.GroupName[..^4];
                if(groupName == lastGroupName)
                {
                    // Merge entries
                    lastGroupEntry.ExercisePoints += entry.ExercisePoints;
                    lastGroupEntry.FlagPoints += entry.FlagPoints;
                    lastGroupEntry.BugBountyPoints += entry.BugBountyPoints;
                    lastGroupEntry.FlagCount += entry.FlagCount;
                    lastGroupEntry.TotalPoints = (lastGroupEntry.TotalPoints + entry.TotalPoints) / 2;
                    lastGroupEntry.LastExerciseSubmissionTime = DateTimeExtensions.Max(lastGroupEntry.LastExerciseSubmissionTime, entry.LastExerciseSubmissionTime);
                    lastGroupEntry.LastFlagSubmissionTime = DateTimeExtensions.Max(lastGroupEntry.LastFlagSubmissionTime, entry.LastFlagSubmissionTime);
                    lastGroupEntry.LastSubmissionTime = DateTimeExtensions.Max(lastGroupEntry.LastSubmissionTime, entry.LastSubmissionTime);
                    lastGroupEntry.MergedSplitGroupPartnerGroupId = entry.GroupId;
                }
                else
                {
                    lastGroupName = groupName;
                    lastGroupEntry = entry;
                    entry.GroupName = groupName;
                    scoreboardEntriesMerged.Add(entry);
                }
            }

            return scoreboardEntriesMerged;
        }

        public async Task<GroupScoreboard> GetGroupScoreboardAsync(int groupId, int labId, CancellationToken cancellationToken = default)
        {
            // Consistent time
            var now = DateTime.Now;

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

            // TODO merge this into one query when https://stackoverflow.com/questions/60854920/ef-core-3-0-generates-window-function-instead-of-join-leading-to-mysql-syntax-e is solved
            var foundFlagsCounts = await _dbContext.FlagSubmissions.AsNoTracking()
                .Where(fs => fs.GroupId == groupId && fs.Flag.LabId == labId)
                .Select(fs => new
                {
                    Valid = fs.Group.LabExecutions
                        .Any(le => le.LabId == labId && le.PreStart <= fs.SubmissionTime && fs.SubmissionTime < le.End)
                })
                .GroupBy(fs => fs.Valid)
                .Select(fs => new {Valid = fs.Key, Count = fs.Count()})
                .ToDictionaryAsync(fs => fs.Valid, fs => fs.Count, cancellationToken);
            var labExecution = await _dbContext.LabExecutions.AsNoTracking()
                .FirstOrDefaultAsync(le => le.GroupId == groupId && le.LabId == labId, cancellationToken);

            var exercises = await _dbContext.Exercises.AsNoTracking()
                .Where(e => e.LabId == labId)
                .OrderBy(e => e.ExerciseNumber)
                .ProjectTo<Exercise>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var exerciseSubmissionsUngrouped = await _dbContext.ExerciseSubmissions.AsNoTracking()
                .Where(es => es.Exercise.LabId == labId && es.GroupId == groupId)
                .OrderBy(es => es.ExerciseId)
                .ThenBy(es => es.SubmissionTime)
                .ProjectTo<ExerciseSubmission>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);
            var exerciseSubmissions = exerciseSubmissionsUngrouped
                .GroupBy(es => es.ExerciseId)
                .ToDictionary(es => es.Key, es => es.ToList());

            var scoreboard = new GroupScoreboard
            {
                LabId = labId,
                CurrentLab = labs.First(l => l.LabId == labId),
                Labs = labs,
                LabExecutionStatus = LabExecutionToStatus(now, labExecution),
                LabExecution = _mapper.Map<LabExecution>(labExecution),
                FoundFlagsCount = foundFlagsCounts.Sum(f => f.Value),
                ValidFoundFlagsCount = foundFlagsCounts.FirstOrDefault(f => f.Key).Value,
                Exercises = new List<ScoreboardGroupExerciseEntry>()
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

                    scoreboard.Exercises.Add(new ScoreboardGroupExerciseEntry
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
                    scoreboard.Exercises.Add(new ScoreboardGroupExerciseEntry
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