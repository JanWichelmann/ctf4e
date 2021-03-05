using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Ctf4e.LessonServer.InputModels
{
    public class MultipleChoiceExerciseInputData
    {
        public int ExerciseId { get; set; }

        [Required]
        public List<int> SelectedOptions { get; set; }
    }
}