using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Models;

namespace Ctf4e.Server.InputModels;

public class AdminLabInputModel
{
    public int? Id { get; set; }

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

    public int MaxPoints { get; set; }

    public int MaxFlagPoints { get; set; }
        
    public bool Visible { get; set; }
}