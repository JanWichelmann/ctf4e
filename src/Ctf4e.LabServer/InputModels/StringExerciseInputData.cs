using System.ComponentModel.DataAnnotations;

namespace Ctf4e.LabServer.InputModels
{
    public class StringExerciseInputData
    {
        public int ExerciseId { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string Input { get; set; }
    }
}
