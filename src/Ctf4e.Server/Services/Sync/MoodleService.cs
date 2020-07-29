using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Microsoft.EntityFrameworkCore;
using MoodleLti;
using MoodleLti.Models;

namespace Ctf4e.Server.Services.Sync
{
    public interface IMoodleService
    {
        /// <summary>
        ///     Uploads the entire lab state into the associated Moodle course.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task UploadStateToMoodleAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    ///     Provides methods for synchronizing the results with Moodle.
    /// </summary>
    public class MoodleService : IMoodleService
    {
        private readonly IMoodleGradebook _moodleGradebook;
        private readonly CtfDbContext _dbContext;
        private readonly IConfigurationService _configurationService;

        public MoodleService(IMoodleGradebook moodleGradebook, CtfDbContext dbContext, IConfigurationService configurationService)
        {
            _moodleGradebook = moodleGradebook ?? throw new ArgumentNullException(nameof(moodleGradebook));
            _dbContext = dbContext;
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        /// <summary>
        ///     Uploads the entire lab state into the associated Moodle course.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public async Task UploadStateToMoodleAsync(CancellationToken cancellationToken)
        {
            // Get current gradebook columns
            var oldCols = (await _moodleGradebook.GetColumnsAsync()).ToDictionary(c => c.Tag);

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
                .ToDictionary(u => u.Id, u => new
                {
                    User = u,
                    LabStates = passedExerciseSubmissions
                        .Where(es => passAsGroup ? groupIdLookup[es.UserId] == u.GroupId : es.UserId == u.Id)
                        .GroupBy(es => es.LabId)
                        .ToDictionary(esg => esg.Key, esg => esg.Count() == labs.First(l => l.LabId == esg.Key).MandatoryExerciseCount)
                });

            // Send data
            foreach(var lab in labs)
            {
                // Build column tag
                string tag = "ctf-lab-" + lab.LabId;

                // Title
                string title = lab.LabName;

                // Update or create column
                MoodleGradebookColumn column;
                if(oldCols.ContainsKey(tag))
                {
                    // Retrieve existing one
                    column = oldCols[tag];
                    oldCols.Remove(tag);

                    // Make sure it is up-to-date
                    column.MaximumScore = 1;
                    column.Title = title;
                    await _moodleGradebook.UpdateColumnAsync(column);
                }
                else
                {
                    // Create new column
                    column = await _moodleGradebook.CreateColumnAsync(title, 1, tag);
                }

                // Insert grades
                foreach(var student in students)
                {
                    // Lab passed?
                    student.Value.LabStates.TryGetValue(lab.LabId, out var passed);
                    await _moodleGradebook.SetGradeAsync(column.Id, student.Value.User.MoodleUserId, new MoodleGradebookGrade
                    {
                        Score = passed ? 1 : 0,
                        Comment = "",
                        Timestamp = DateTime.Now
                    });
                }
            }

            // Delete unneeded old columns
            foreach(var c in oldCols)
                await _moodleGradebook.DeleteColumnAsync(c.Value.Id);
        }
    }
}