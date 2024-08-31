using System.Collections.Generic;

namespace Ctf4e.Server.Models;

public class Group
{
    public int Id { get; set; }

    public int SlotId { get; set; }

    public Slot Slot { get; set; }

    public string DisplayName { get; set; }
        
    public string ScoreboardAnnotation { get; set; }
        
    public string ScoreboardAnnotationHoverText { get; set; }

    public bool ShowInScoreboard { get; set; }

    public ICollection<User> Members { get; set; }

    public ICollection<LabExecution> LabExecutions { get; set; }
}