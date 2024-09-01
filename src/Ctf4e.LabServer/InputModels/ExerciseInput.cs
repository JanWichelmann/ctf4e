using System.ComponentModel.DataAnnotations;

namespace Ctf4e.LabServer.InputModels;

public class ExerciseInput
{
    [Required]
    public int ExerciseId { get; set; }
}