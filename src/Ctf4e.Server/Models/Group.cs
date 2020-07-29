using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.Models
{
    public class Group
    {
        public int Id { get; set; }

        [Required]
        public int SlotId { get; set; }

        public Slot Slot { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = ValidationStrings.FieldIsRequired)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [StringLength(50)]
        public string DisplayName { get; set; }

        public bool ShowInScoreboard { get; set; }

        public ICollection<User> Members { get; set; }

        public ICollection<LabExecution> LabExecutions { get; set; }
    }
}