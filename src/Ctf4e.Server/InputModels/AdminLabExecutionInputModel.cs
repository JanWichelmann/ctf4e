using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.InputModels;

public class AdminLabExecutionInputModel
{
    [Required]
    public int GroupId { get; set; }

    [Required]
    public int LabId { get; set; }

    [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:s}")]
    public DateTime Start { get; set; }

    [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:s}")]
    public DateTime End { get; set; }
}