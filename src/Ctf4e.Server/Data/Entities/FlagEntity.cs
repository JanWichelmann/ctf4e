using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ctf4e.Server.Data.Entities
{
    public class FlagEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Code { get; set; }

        [Required]
        [StringLength(100)]
        public string Description { get; set; }

        public int BasePoints { get; set; }

        public bool IsBounty { get; set; }

        [Required]
        public LabEntity Lab { get; set; }

        public int LabId { get; set; }

        public ICollection<FlagSubmissionEntity> Submissions { get; set; }
    }
}