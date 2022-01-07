using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.Models;

public class Exercise
{
    public int Id { get; set; }

    [Required]
    public int LabId { get; set; }

    public Lab Lab { get; set; }

    public int ExerciseNumber { get; set; }

    [Required(AllowEmptyStrings = true, ErrorMessage = ValidationStrings.FieldIsRequired)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string Name { get; set; }

    public bool IsMandatory { get; set; }

    public bool IsPreStartAvailable { get; set; }

    [Range(0, int.MaxValue)]
    public int BasePoints { get; set; }

    [Range(0, int.MaxValue)]
    public int PenaltyPoints { get; set; }

    public ICollection<ExerciseSubmission> Submissions { get; set; }
}