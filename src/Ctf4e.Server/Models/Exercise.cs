using System.Collections.Generic;

namespace Ctf4e.Server.Models;

public class Exercise
{
    public int Id { get; set; }

    public int LabId { get; set; }

    public Lab Lab { get; set; }

    public int ExerciseNumber { get; set; }

    public string Name { get; set; }

    public bool IsMandatory { get; set; }

    public int BasePoints { get; set; }

    public int PenaltyPoints { get; set; }

    public ICollection<ExerciseSubmission> Submissions { get; set; }
}