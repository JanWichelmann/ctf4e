namespace Ctf4e.Server.ViewModels;

public class AdminFlagListEntry
{
    public int Id { get; set; }

    public string Code { get; set; }

    public string Description { get; set; }

    public int BasePoints { get; set; }

    public bool IsBounty { get; set; }
}