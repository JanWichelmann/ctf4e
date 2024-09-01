namespace Ctf4e.Server.ViewModels;

public class AdminGroupListEntry
{
    public int Id { get; set; }

    public int SlotId { get; set; }
    public string SlotName { get; set; }

    public string DisplayName { get; set; }
        
    public string ScoreboardAnnotation { get; set; }
        
    public string ScoreboardAnnotationHoverText { get; set; }

    public bool ShowInScoreboard { get; set; }

    public int MemberCount { get; set; }

    public int LabExecutionCount { get; set; }
}