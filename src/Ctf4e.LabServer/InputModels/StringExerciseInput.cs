using System.ComponentModel.DataAnnotations;

namespace Ctf4e.LabServer.InputModels;

public class StringExerciseInput : ExerciseInput
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Input { get; set; }
}