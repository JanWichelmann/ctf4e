using System;

namespace Ctf4e.Server.ViewModels;

public class AdminLabExecutionListEntry
{
    public int GroupId { get; set; }

    public int LabId { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }
}