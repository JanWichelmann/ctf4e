﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.Data.Entities;

public class LabEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = ValidationStrings.FieldIsRequired)]
    [StringLength(100)]
    public string Name { get; set; }

    [Required(ErrorMessage = ValidationStrings.FieldIsRequired)]
    [StringLength(256)]
    public string ServerBaseUrl { get; set; }

    [Required(ErrorMessage = ValidationStrings.FieldIsRequired)]
    [StringLength(100)]
    public string ApiCode { get; set; }

    public int MaxPoints { get; set; }

    public int MaxFlagPoints { get; set; }

    public bool Visible { get; set; }

    public int SortIndex { get; set; }

    public ICollection<LabExecutionEntity> Executions { get; set; }

    public ICollection<FlagEntity> Flags { get; set; }

    public ICollection<ExerciseEntity> Exercises { get; set; }
}