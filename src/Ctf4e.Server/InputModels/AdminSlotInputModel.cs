using System;
using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.InputModels;

public class AdminSlotInputModel
{
    public int? Id { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = ValidationStrings.FieldIsRequired)]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(50)]
    public string Name { get; set; }

    public int? DefaultExecuteLabId { get; set; }

    [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:s}")]
    public DateTime? DefaultExecuteLabEnd { get; set; }
}