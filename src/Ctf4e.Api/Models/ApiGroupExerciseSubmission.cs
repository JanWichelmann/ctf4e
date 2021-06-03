using System;

namespace Ctf4e.Api.Models
{
    public class ApiGroupExerciseSubmission
    {
        public int ExerciseNumber { get; set; }

        public int GroupId { get; set; }

        public DateTime? SubmissionTime { get; set; }

        public bool ExercisePassed { get; set; }

        public int Weight { get; set; }
    }
}