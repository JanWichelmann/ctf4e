namespace Ctf4e.Server.ViewModels;

public class AdminLabListEntry
{
    public int Id { get; set; }

    public string Name { get; set; }

    public int MaxPoints { get; set; }

    public int MaxFlagPoints { get; set; }
        
    public bool Visible { get; set; }

    public int ExecutionCount { get; set; }

    public int FlagCount { get; set; }

    public int ExerciseCount { get; set; }
}