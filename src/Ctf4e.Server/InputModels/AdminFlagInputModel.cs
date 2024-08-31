using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.InputModels;

public class AdminFlagInputModel
{
    public int? Id { get; set; }

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
    public int LabId { get; set; }
}