using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Data.Entities
{
    public class FlagSubmissionEntity
    {
        [Required]
        public UserEntity User { get; set; }
        public int UserId { get; set; }

        [Required]
        public FlagEntity Flag { get; set; }
        public int FlagId { get; set; }

        public DateTime SubmissionTime { get; set; }
    }
}
