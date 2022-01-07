using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.Models;

public class User
{
    public int Id { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = ValidationStrings.FieldIsRequired)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string DisplayName { get; set; }

    public int MoodleUserId { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = ValidationStrings.FieldIsRequired)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string MoodleName { get; set; }

    public bool IsAdmin { get; set; }

    public bool IsTutor { get; set; }

    public string GroupFindingCode { get; set; }

    public ICollection<FlagSubmission> FlagSubmissions { get; set; }

    public ICollection<ExerciseSubmission> ExerciseSubmissions { get; set; }

    public int? GroupId { get; set; }
    public Group Group { get; set; }
}