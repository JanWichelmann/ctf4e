using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Ctf4e.Server.Data;
using Ctf4e.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services.Sync;

public interface ICsvService
{
    /// <summary>
    ///     Returns data about passed/failed labs in CSV format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task<string> GetSummaryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns CSV-formatted information about the results of the given lab.
    /// </summary>
    /// <param name="labId">Lab ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task<string> GetLabStateAsync(int labId, CancellationToken cancellationToken);
}

/// <summary>
///     Provides methods to download the lab results as a CSV file.
/// </summary>
public class CsvService(CtfDbContext dbContext, IConfigurationService configurationService, IMapper mapper) : ICsvService
{
    /// <summary>
    ///     Returns data about passed/failed labs in CSV format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async Task<string> GetSummaryAsync(CancellationToken cancellationToken)
    {
        bool passAsGroup = await configurationService.PassAsGroup.GetAsync(cancellationToken);

        // Query existing labs
        var labs = await dbContext.Labs.AsNoTracking()
            .OrderBy(l => l.Id)
            .Select(l => new
            {
                LabId = l.Id,
                LabName = l.Name,
                MandatoryExerciseCount = l.Exercises.Count(e => e.IsMandatory)
            })
            .ToListAsync(cancellationToken);

        // Get mapping of users and groups
        var users = await dbContext.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);
        var groupIdLookup = users.ToDictionary(u => u.Id, u => u.GroupId);

        // Get all passed submissions of mandatory exercises
        var passedExerciseSubmissions = await dbContext.ExerciseSubmissions.AsNoTracking()
            .Where(s => s.ExercisePassed
                        && s.Exercise.IsMandatory
                        && s.User.Group.LabExecutions
                            .Any(le => le.LabId == s.Exercise.LabId && le.Start <= s.SubmissionTime && s.SubmissionTime < le.End))
            .Select(s => new
            {
                s.ExerciseId,
                s.Exercise.LabId,
                s.UserId
            }).Distinct()
            .ToListAsync(cancellationToken);

        // Get passed exercise counts per student and lab
        var students = users
            .Where(u => !u.IsTutor && u.GroupId != null)
            .ToDictionary(u => u.Id, u => new
            {
                User = u,
                LabStates = passedExerciseSubmissions
                    .Where(es => passAsGroup ? groupIdLookup[es.UserId] == u.GroupId : es.UserId == u.Id)
                    .GroupBy(es => es.LabId)
                    .ToDictionary(
                        esg => esg.Key,
                        esg => esg
                                   .Select(esge => esge.ExerciseId)
                                   .Distinct()
                                   .Count()
                               == labs
                                   .First(l => l.LabId == esg.Key)
                                   .MandatoryExerciseCount
                    )
            });

        // Create CSV columns
        StringBuilder csv = new StringBuilder();
        csv.Append("\"LoginId\",\"LoginName\",\"Name\"");
        foreach(var lab in labs)
        {
            csv.Append(",\"");
            csv.Append(Escape(lab.LabName));
            csv.Append('"');
        }

        csv.AppendLine();

        // Create entries
        foreach(var student in students)
        {
            csv.Append("\"");
            csv.Append(student.Value.User.MoodleUserId);
            csv.Append("\",\"");
            csv.Append(Escape(student.Value.User.MoodleName));
            csv.Append("\",\"");
            csv.Append(Escape(student.Value.User.DisplayName));
            csv.Append('"');

            foreach(var lab in labs)
            {
                student.Value.LabStates.TryGetValue(lab.LabId, out var labPassed);
                csv.Append(labPassed ? ",\"1\"" : ",\"0\"");
            }

            csv.AppendLine();
        }

        return csv.ToString();
    }

    /// <summary>
    /// Returns CSV-formatted information about the results of the given lab.
    /// </summary>
    /// <param name="labId">Lab ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async Task<string> GetLabStateAsync(int labId, CancellationToken cancellationToken)
    {
        bool passAsGroup = await configurationService.PassAsGroup.GetAsync(cancellationToken);

        var lab = await dbContext.Labs.AsNoTracking()
            .Where(l => l.Id == labId)
            .Select(l => new
            {
                MandatoryExerciseCount = l.Exercises.Count(e => e.IsMandatory)
            })
            .FirstAsync(cancellationToken);

        var exercises = await dbContext.Exercises.AsNoTracking()
            .Where(e => e.LabId == labId)
            .OrderBy(e => e.ExerciseNumber)
            .ProjectTo<Exercise>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var labExecutions = await dbContext.LabExecutions.AsNoTracking()
            .Where(le => le.LabId == labId)
            .ToListAsync(cancellationToken);
        var labExecutionsByGroup = labExecutions
            .ToDictionary(le => le.GroupId);

        // Get mapping of users and groups
        var users = await dbContext.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);
        var groupIdLookup = users.ToDictionary(u => u.Id, u => u.GroupId);

        // Get all valid exercise submissions
        var submissions = await dbContext.ExerciseSubmissions.AsNoTracking()
            .Where(s => s.Exercise.LabId == labId
                        && s.User.Group.LabExecutions
                            .Any(le => le.LabId == s.Exercise.LabId && le.Start <= s.SubmissionTime && s.SubmissionTime < le.End))
            .ProjectTo<ExerciseSubmission>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        // Group exercise submissions by user or group ID
        // Then group them by exercise and sort the submissions by time
        var submissionsGrouped = submissions
            .GroupBy(s => passAsGroup ? groupIdLookup[s.UserId] : s.UserId)
            .ToDictionary(
                sg => sg.Key,
                sg => sg
                    .GroupBy(s => s.ExerciseId)
                    .ToDictionary(
                        sge => sge.Key,
                        sge => sge
                            .OrderBy(es => es.SubmissionTime)
                            .ToList()
                    )
            );

        // For every student, collect whether they
        // - passed an exercise
        // - how many points they got for this exercise
        // - whether they passed the entire lab
        var students = users
            .Where(u => !u.IsTutor && u.GroupId != null)
            .ToDictionary(u => u.Id, u => new
            {
                User = u,
                Exercises = exercises
                    .ToDictionary(
                        e => e.Id,
                        e =>
                        {
                            if(!submissionsGrouped.TryGetValue(passAsGroup ? u.GroupId! : u.Id, out var userSubmissions))
                                return new UserExercisePointsEntry(e.IsMandatory, false, 0);

                            userSubmissions.TryGetValue(e.Id, out var userExerciseSubmissions);
                            return new UserExercisePointsEntry(
                                e.IsMandatory,
                                ScoreboardUtilities.CalculateExercisePoints(e, userExerciseSubmissions ?? [], labExecutionsByGroup[u.GroupId!.Value])
                            );
                        }
                    )
            });

        // Create CSV columns
        StringBuilder csv = new StringBuilder();
        csv.Append("\"LoginId\",\"LoginName\",\"Name\",\"LabPassed\"");
        foreach(var exercise in exercises)
        {
            csv.Append(",\"");
            csv.Append($"Ex#{exercise.ExerciseNumber}passed");
            csv.Append('"');

            csv.Append(",\"");
            csv.Append($"Ex#{exercise.ExerciseNumber}points");
            csv.Append('"');
        }

        csv.AppendLine();

        // Create entries
        foreach(var student in students)
        {
            csv.Append("\"");
            csv.Append(student.Value.User.MoodleUserId);
            csv.Append("\",\"");
            csv.Append(Escape(student.Value.User.MoodleName));
            csv.Append("\",\"");
            csv.Append(Escape(student.Value.User.DisplayName));
            csv.Append("\",\"");
            csv.Append(student.Value.Exercises.Count(e => e.Value.IsMandatory && e.Value.Passed) == lab.MandatoryExerciseCount ? "1" : "0");
            csv.Append('"');

            foreach(var exercise in exercises)
            {
                student.Value.Exercises.TryGetValue(exercise.Id, out var exercisePoints);
                csv.Append(exercisePoints!.Passed ? ",\"1\"" : ",\"0\"");
                csv.Append(",\"" + exercisePoints.Points + "\"");
            }

            csv.AppendLine();
        }

        return csv.ToString();
    }

    private record UserExercisePointsEntry(bool IsMandatory, bool Passed, int Points)
    {
        public UserExercisePointsEntry(bool isMandatory, (bool Passed, int Points, int ValidTries) tuple)
            : this(isMandatory, tuple.Passed, tuple.Points)
        {
        }
    }

    /// <summary>
    ///     Escapes the given string.
    /// </summary>
    /// <param name="str">String.</param>
    /// <returns></returns>
    private string Escape(string str)
    {
        // " -> ""
        return str.Replace("\"", "\"\"");
    }
}