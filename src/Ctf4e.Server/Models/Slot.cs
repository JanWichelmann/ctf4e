using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Ctf4e.Server.Constants;

namespace Ctf4e.Server.Models;

public class Slot
{
    public int Id { get; set; }

    public string Name { get; set; }

    public int? DefaultExecuteLabId { get; set; }
    public Lab DefaultExecuteLab { get; set; }

    public DateTime? DefaultExecuteLabEnd { get; set; }

    public ICollection<Group> Groups { get; set; }
}