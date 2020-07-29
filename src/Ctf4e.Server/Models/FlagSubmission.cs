using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Models
{
    public class FlagSubmission
    {
        [Required]
        public int UserId { get; set; }

        public User User { get; set; }

        [Required]
        public int FlagId { get; set; }

        public Flag Flag { get; set; }

        public DateTime SubmissionTime { get; set; }
    }
}