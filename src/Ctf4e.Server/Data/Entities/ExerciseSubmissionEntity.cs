using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ctf4e.Server.Data.Entities
{
    public class ExerciseSubmissionEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public ExerciseEntity Exercise { get; set; }
        public int ExerciseId { get; set; }

        [Required]
        public UserEntity User { get; set; }
        public int UserId { get; set; }

        public DateTime SubmissionTime { get; set; }

        public bool ExercisePassed { get; set; }

        /// <summary>
        /// Factor for penalty points. Always 1 for succesful tries.
        /// </summary>
        public int Weight { get; set; }
    }
}
