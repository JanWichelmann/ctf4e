using Ctf4e.Server.Models;

namespace Ctf4e.Server.ViewModels;

public class AdminLabExecution
{
    public LabExecution LabExecution { get; set; }

    public int SlotId { get; set; }

    public bool OverrideExisting { get; set; }
}