using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Ctf4e.LabServer.InputModels
{
    public class StringExerciseInputData
    {
        public int ExerciseId { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string Input { get; set; }
    }
}
