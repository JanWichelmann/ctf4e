using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.Models
{
    public class Lab
    {
        public int Id { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = ValidationStrings.FieldIsRequired)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [StringLength(100)]
        public string Name { get; set; }

        [Required(AllowEmptyStrings = true, ErrorMessage = ValidationStrings.FieldIsRequired)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [StringLength(256)]
        public string ServerBaseUrl { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = ValidationStrings.FieldIsRequired)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [StringLength(100)]
        public string ApiCode { get; set; }

        public int MaxFlagPoints { get; set; }

        public ICollection<LabExecution> Executions { get; set; }

        public ICollection<Flag> Flags { get; set; }

        public ICollection<Exercise> Exercises { get; set; }
    }
}
