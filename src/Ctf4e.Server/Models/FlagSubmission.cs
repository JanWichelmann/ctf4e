using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Models
{
    public class FlagSubmission
    {
        [Required]
        public int GroupId { get; set; }
        public Group Group { get; set; }

        [Required]
        public int FlagId { get; set; }
        public Flag Flag { get; set; }

        public DateTime SubmissionTime { get; set; }
    }
}
