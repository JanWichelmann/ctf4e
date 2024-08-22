namespace Ctf4e.Server.ViewModels;

public class AdminExerciseListEntry
{
    public int Id { get; set; }

    public int ExerciseNumber { get; set; }

    public string Name { get; set; }

    public bool IsMandatory { get; set; }

    public int BasePoints { get; set; }

    public int PenaltyPoints { get; set; }
}