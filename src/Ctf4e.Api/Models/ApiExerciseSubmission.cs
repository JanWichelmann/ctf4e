using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Ctf4e.Api.Models
{
    public class ApiExerciseSubmission
    {
        public int ExerciseNumber { get; set; }

        public int GroupId { get; set; }

        public DateTime? SubmissionTime { get; set; }

        public bool ExercisePassed { get; set; }

        public int Weight { get; set; }
    }
}
