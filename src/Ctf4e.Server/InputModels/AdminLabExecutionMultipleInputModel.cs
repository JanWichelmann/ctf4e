using System;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.Server.InputModels;

public class AdminLabExecutionMultipleInputModel
{
    [Required]
    public int SlotId { get; set; }

    [Required]
    public int LabId { get; set; }

    [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:s}")]
    public DateTime Start { get; set; }

    [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:s}")]
    public DateTime End { get; set; }

    public bool OverrideExisting { get; set; }
}