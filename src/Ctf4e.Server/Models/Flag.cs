using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.Models
{
    public class Flag
    {
        public int Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [StringLength(100)]
        public string Code { get; set; }

        [Required(AllowEmptyStrings = true)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [StringLength(100)]
        public string Description { get; set; }

        [Range(0, int.MaxValue)]
        public int BasePoints { get; set; }

        public bool IsBounty { get; set; }

        [Required]
        public int LessonId { get; set; }

        public Lesson Lesson { get; set; }

        public ICollection<FlagSubmission> Submissions { get; set; }
    }
}