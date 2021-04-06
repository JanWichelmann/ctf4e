using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ctf4e.Server.Data.Entities
{
    public class GroupEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public SlotEntity Slot { get; set; }

        public int SlotId { get; set; }

        [Required]
        [StringLength(50)]
        public string DisplayName { get; set; }
        
        [StringLength(50)]
        public string ScoreboardAnnotation { get; set; }
        
        [StringLength(200)]
        public string ScoreboardAnnotationHoverText { get; set; }
        
        public bool ShowInScoreboard { get; set; }

        public ICollection<UserEntity> Members { get; set; }

        public ICollection<LabExecutionEntity> LabExecutions { get; set; }
    }
}