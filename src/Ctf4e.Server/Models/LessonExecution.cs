using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Models
{
    public class LessonExecution
    {
        [Required]
        public int GroupId { get; set; }

        public Group Group { get; set; }

        [Required]
        public int LessonId { get; set; }

        public Lesson Lesson { get; set; }

        public DateTime PreStart { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }
    }
}