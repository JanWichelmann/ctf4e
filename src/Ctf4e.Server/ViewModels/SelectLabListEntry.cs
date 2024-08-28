namespace Ctf4e.Server.ViewModels;

/// <summary>
/// An entry in a lab selection list/dropdown.
/// </summary>
public class SelectLabListEntry
{
    public int Id { get; set; }

    public string Name { get; set; }
        
    public bool Visible { get; set; }
}