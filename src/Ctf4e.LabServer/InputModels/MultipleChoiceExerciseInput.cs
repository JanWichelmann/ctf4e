using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.LabServer.InputModels;

public class MultipleChoiceExerciseInput : ExerciseInput
{
    [Required]
    public List<int> SelectedOptions { get; set; }
}