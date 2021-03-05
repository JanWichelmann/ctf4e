using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Data.Entities
{
    public class LessonExecutionEntity
    {
        [Required]
        public GroupEntity Group { get; set; }

        public int GroupId { get; set; }

        [Required]
        public LessonEntity Lesson { get; set; }

        public int LessonId { get; set; }

        public DateTime PreStart { get; set; }

        public DateTime Start { get; set; }

        public DateTime End { get; set; }
    }
}