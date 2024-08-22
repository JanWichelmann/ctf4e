using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.InputModels;

public class AdminGroupInputModel
{
    public int? Id { get; set; }

    [Required]
    public int SlotId { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = ValidationStrings.FieldIsRequired)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(50)]
    public string DisplayName { get; set; }

    [StringLength(50)]
    public string ScoreboardAnnotation { get; set; }

    [StringLength(200)]
    public string ScoreboardAnnotationHoverText { get; set; }

    public bool ShowInScoreboard { get; set; }
}