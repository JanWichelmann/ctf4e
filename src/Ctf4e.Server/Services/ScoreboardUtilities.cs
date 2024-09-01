using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Ctf4e.Server.Data.Entities;
using Ctf4e.Server.Models;
using Ctf4e.Server.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ctf4e.Server.Services;

/// <summary>
/// Utility functions for scoreboard calculations.
/// </summary>
public class ScoreboardUtilities
{
    private double _minPointsMultiplier;
    private int _halfPointsCount;
    
    /// <summary>
    /// Retrieves the flag point parameters from the configuration.
    ///
    /// Called once at startup.
    /// </summary>
    /// <param name="serviceScopeFactory">Service scope factory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async Task InitFlagPointParametersAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        // Retrieve configuration service
        using var scope = serviceScopeFactory.CreateScope();
        var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        // Retrieve constants
        _minPointsMultiplier = 1.0 / await configurationService.GetFlagMinimumPointsDivisorAsync(cancellationToken);
        _halfPointsCount = await configurationService.GetFlagHalfPointsSubmissionCountAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the points the flag yields for the give submission count.
    /// </summary>
    /// <param name="basePoints">Flag base points.</param>
    /// <param name="isBounty">Whether the flag is a bounty.</param>
    /// <param name="submissionCount">Number of valid submissions.</param>
    /// <returns></returns>
    public int CalculateFlagPoints(int basePoints, bool isBounty, int submissionCount)
    {
        // Bounties are not scaled
        if(isBounty)
            return basePoints;

        // Relevant constants:
        // a: Base points
        // b: Min points = multiplier*a
        // c: 50% points y = (a+b)/2
        // d: 50% points x

        // Flag points depending on submission count x:
        // (a-b)*((a-b)/(c-b))^(1/(d-1)*(-x+1))+b
        // (base is solution of (a-b)*y^(-d+1)+b=c)

        // (a-b)
        double amb = basePoints - _minPointsMultiplier * basePoints;

        // (c-b)=(a+b)/2-b=(a-b)/2
        // -> (a-b)/(c-b)=2
        double points = (amb * Math.Pow(2, (-submissionCount + 1.0) / (_halfPointsCount - 1))) + (_minPointsMultiplier * basePoints);
        return points > basePoints ? basePoints : (int)Math.Round(points);
    }
    
    /// <summary>
    /// Derives the status for the given timestamp, considering the given lab execution constraints.
    /// </summary>
    /// <param name="time">Timestamp to check.</param>
    /// <param name="labExecution">Lab execution data.</param>
    /// <returns></returns>
    public static LabExecutionStatus GetLabExecutionStatus(DateTime time, LabExecutionEntity labExecution)
    {
        if(labExecution == null)
            return LabExecutionStatus.Undefined;

        if(time < labExecution.Start)
            return LabExecutionStatus.BeforeStart;
        if(labExecution.Start <= time && time < labExecution.End)
            return LabExecutionStatus.Start;
        if(labExecution.End <= time)
            return LabExecutionStatus.End;

        return LabExecutionStatus.Undefined;
    }

    /// <summary>
    /// Calculates the points for the given exercise from the given submission list. Ignores tries that are outside of the lab execution constraints.
    /// </summary>
    /// <param name="exercise">Exercise being evaluated.</param>
    /// <param name="submissions">All submissions for this exercise.</param>
    /// <param name="labExecution">Lab execution data.</param>
    /// <returns></returns>
    public static (bool passed, int points, int validTries) CalculateExercisePoints(Exercise exercise, IEnumerable<ExerciseSubmission> submissions, LabExecutionEntity labExecution)
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
            if(submission.SubmissionTime < labExecution.Start || labExecution.End <= submission.SubmissionTime)
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
    public static bool GetFlagSubmissionIsValid(FlagSubmission flagSubmission, LabExecutionEntity labExecution)
    {
        // If the group does not have a lab execution, submitting flags is impossible
        if(labExecution == null)
            return false;

        return labExecution.Start <= flagSubmission.SubmissionTime && flagSubmission.SubmissionTime < labExecution.End;
    }

    public async Task<List<FlagState>> GetFlagStateAsync(CtfDbContext dbContext, int labId, CancellationToken cancellationToken)
    {
        var flags = await dbContext.Database.SqlQuery<FlagState>(
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
        foreach(var f in flags)
            f.CurrentPoints = CalculateFlagPoints(f.BasePoints, f.IsBounty, f.SubmissionCount);
        
        return flags;
    }
    
    public class FlagState
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public int BasePoints { get; set; }
        public bool IsBounty { get; set; }
        public int SubmissionCount { get; set; }

        [NotMapped]
        public int CurrentPoints { get; set; }
    }
}