using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Data.Entities
{
    public class LabExecutionEntity
    {
        [Required]
        public GroupEntity Group { get; set; }
        public int GroupId { get; set; }

        [Required]
        public LabEntity Lab { get; set; }
        public int LabId { get; set; }

        public DateTime PreStart { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }
    }
}
