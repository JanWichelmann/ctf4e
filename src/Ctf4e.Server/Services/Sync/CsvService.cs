using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services.Sync;

public interface ICsvService
{
    /// <summary>
    ///     Returns data about passed/failed labs in CSV format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    Task<string> GetLabStatesAsync(CancellationToken cancellationToken);
}

/// <summary>
///     Provides methods to download the lab results as a CSV file.
/// </summary>
public class CsvService : ICsvService
{
    private readonly CtfDbContext _dbContext;
    private readonly IConfigurationService _configurationService;

    public CsvService(CtfDbContext dbContext, IConfigurationService configurationService)
    {
        _dbContext = dbContext;
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    /// <summary>
    ///     Returns data about passed/failed labs in CSV format.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    public async Task<string> GetLabStatesAsync(CancellationToken cancellationToken)
    {
        bool passAsGroup = await _configurationService.GetPassAsGroupAsync(cancellationToken);

        // Query existing labs
        var labs = await _dbContext.Labs.AsNoTracking()
            .OrderBy(l => l.Id)
            .Select(l => new
            {
                LabId = l.Id,
                LabName = l.Name,
                MandatoryExerciseCount = l.Exercises.Count(e => e.IsMandatory)
            })
            .ToListAsync(cancellationToken);

        // Get mapping of users and groups
        var users = await _dbContext.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);
        var groupIdLookup = users.ToDictionary(u => u.Id, u => u.GroupId);

        // Get all passed submissions of mandatory exercises
        var passedExerciseSubmissions = await _dbContext.ExerciseSubmissions.AsNoTracking()
            .Where(s => s.ExercisePassed
                        && s.Exercise.IsMandatory
                        && s.User.Group.LabExecutions
                            .Any(le => le.LabId == s.Exercise.LabId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
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
                    .ToDictionary(esg => esg.Key, esg => esg.Count() == labs.First(l => l.LabId == esg.Key).MandatoryExerciseCount)
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