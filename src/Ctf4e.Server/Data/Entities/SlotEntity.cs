﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ctf4e.Server.Data.Entities;

public class SlotEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; }
    
    public int? DefaultExecuteLabId { get; set; }
    public LabEntity DefaultExecuteLab { get; set; }
    
    public DateTime? DefaultExecuteLabEnd { get; set; }
    
    public ICollection<GroupEntity> Groups { get; set; }
}