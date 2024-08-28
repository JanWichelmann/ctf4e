public class NavViewModel
{
    public SubView SubView { get; set; }
    public int? LabId { get; set; }
    public int? SlotId { get; set; }
    
    public int GroupId { get; set; }
    public int UserId { get; set; }
}

public enum SubView
{
    Statistics,
    Groups,
    Users,
    GroupDashboard,
    UserDashboard,
    Export
}