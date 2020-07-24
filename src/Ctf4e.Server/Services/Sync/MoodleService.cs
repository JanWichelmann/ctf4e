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
        /// Uploads the entire lab state into the associated Moodle course.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        Task UploadStateToMoodleAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Provides methods for synchronizing the results with Moodle.
    /// </summary>
    public class MoodleService : IMoodleService
    {
        private readonly IMoodleGradebook _moodleGradebook;
        private readonly CtfDbContext _dbContext;

        public MoodleService(IMoodleGradebook moodleGradebook, CtfDbContext dbContext)
        {
            _moodleGradebook = moodleGradebook ?? throw new ArgumentNullException(nameof(moodleGradebook));
            _dbContext = dbContext;
        }

        /// <summary>
        /// Uploads the entire lab state into the associated Moodle course.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public async Task UploadStateToMoodleAsync(CancellationToken cancellationToken)
        {
            // TODO
            /*
            // Get current gradebook columns
            var oldCols = (await _moodleGradebook.GetColumnsAsync()).ToDictionary(c => c.Tag);

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

            // Get all passed submissions of mandatory exercises
            var passedExerciseSubmissions = await _dbContext.ExerciseSubmissions.AsNoTracking()
                .Where(s => s.ExercisePassed
                            && s.Exercise.IsMandatory
                            && s.Group.LabExecutions
                                .Any(le => le.LabId == s.Exercise.LabId && le.PreStart <= s.SubmissionTime && s.SubmissionTime < le.End))
                .Select(s => new
                {
                    s.ExerciseId,
                    s.Exercise.LabId,
                    s.GroupId
                }).Distinct()
                .ToListAsync(cancellationToken);

            // Get students
            var students = await _dbContext.Users.AsNoTracking()
                .Where(u => !u.IsAdmin
                            && !u.IsTutor
                            && u.GroupId != null)
                .ToListAsync(cancellationToken);

            // Get passed exercise counts per student and lab
            var passedCounts = students
                .ToDictionary(s => s.MoodleUserId, s => passedExerciseSubmissions
                    .Where(es => es.GroupId == s.GroupId)
                    .GroupBy(es => es.LabId)
                    .ToDictionary(esg => esg.Key, esg => esg.Count()));

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
                foreach(var s in passedCounts)
                {
                    // Lab passed?
                    bool passed = false;
                    if(s.Value.TryGetValue(lab.LabId, out int passedExerciseCount))
                        passed = passedExerciseCount == lab.MandatoryExerciseCount;
                    await _moodleGradebook.SetGradeAsync(column.Id, s.Key, new MoodleGradebookGrade
                    {
                         Score= passed ? 1 : 0,
                          Comment = "",
                           Timestamp = DateTime.Now
                    });
                }
            }

            // Delete unneeded old columns
            //foreach(var c in oldCols)
            //    await _moodleGradebook.DeleteColumnAsync(c.Value.Id);
            }
            */
        }
    }
}
