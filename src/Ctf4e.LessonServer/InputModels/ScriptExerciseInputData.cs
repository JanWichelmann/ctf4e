using System.ComponentModel.DataAnnotations;

namespace Ctf4e.LessonServer.InputModels
{
    public class ScriptExerciseInputData
    {
        public int ExerciseId { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string Input { get; set; }
    }
}