using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Ctf4e.Server.Services.Sync
{
    public interface ICsvService
    {
        /// <summary>
        ///     Returns data about passed/failed lessons in CSV format.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task<string> GetLessonStatesAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    ///     Provides methods to download the lesson results as a CSV file.
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
        ///     Returns data about passed/failed lessons in CSV format.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public async Task<string> GetLessonStatesAsync(CancellationToken cancellationToken)
        {
            bool passAsGroup = await _configurationService.GetPassAsGroupAsync(cancellationToken);

            // Query existing lessons
            var lessons = await _dbContext.Lessons.AsNoTracking()
                .OrderBy(l => l.Id)
                .Select(l => new
                {
                    LessonId = l.Id,
                    LessonName = l.Name,
                    MandatoryExerciseCount = l.Exercises.Count(e => e.IsMandatory)
                })
                .ToListAsync(cancellationToken);

            // Get mapping of users and groups
            var users = await _dbContext.Users.AsNoTracking()
                .Where(u => !u.IsAdmin
                            && !u.IsTutor
                            && u.GroupId != null)
                .OrderBy(u => u.DisplayName)
                .ToListAsync(cancellationToken);
            var groupIdLookup = users.ToDictionary(u => u.Id, u => u.GroupId);

            // Get all passed submissions of mandatory exercises
            var passedExerciseSubmissions = await _dbContext.ExerciseSubmissions.AsNoTracking()
                .Where(s => s.ExercisePassed
                            && s.Exercise.IsMandatory
                            && s.User.Group.LessonExecutions
                                .Any(le => le.LessonId == s.Exercise.LessonId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
                .Select(s => new
                {
                    s.ExerciseId,
                    s.Exercise.LessonId,
                    s.UserId
                }).Distinct()
                .ToListAsync(cancellationToken);

            // Get passed exercise counts per student and lesson
            var students = users
                .ToDictionary(u => u.Id, u => new
                {
                    User = u,
                    LessonStates = passedExerciseSubmissions
                        .Where(es => passAsGroup ? groupIdLookup[es.UserId] == u.GroupId : es.UserId == u.Id)
                        .GroupBy(es => es.LessonId)
                        .ToDictionary(esg => esg.Key, esg => esg.Count() == lessons.First(l => l.LessonId == esg.Key).MandatoryExerciseCount)
                });

            // Create CSV columns
            StringBuilder csv = new StringBuilder();
            csv.Append("\"LoginId\",\"LoginName\",\"Name\"");
            foreach(var lesson in lessons)
            {
                csv.Append(",\"");
                csv.Append(Escape(lesson.LessonName));
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

                foreach(var lesson in lessons)
                {
                    student.Value.LessonStates.TryGetValue(lesson.LessonId, out var lessonPassed);
                    csv.Append(lessonPassed ? ",\"1\"" : ",\"0\"");
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
}