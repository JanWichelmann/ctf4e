using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ctf4e.Server.Data.Entities
{
    public class ExerciseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public LabEntity Lab { get; set; }
        public int LabId { get; set; }

        public int ExerciseNumber { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public bool IsMandatory { get; set; }
        
        public bool IsPreStartAvailable { get; set; }

        public int BasePoints { get; set; }

        public int PenaltyPoints { get; set; }

        public ICollection<ExerciseSubmissionEntity> Submissions { get; set; }
    }
}
