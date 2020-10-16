using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.Models.State
{
    public class UserStateFileMultipleChoiceExerciseEntry : UserStateFileExerciseEntry
    {
        public static UserStateFileStringExerciseEntry CreateState(LabConfigurationMultipleChoiceExerciseEntry exercise)
        {
            // Create empty state for this exercise
            var exerciseState = new UserStateFileStringExerciseEntry
            {
                ExerciseId = exercise.Id,
                Solved = false,
                Type = UserStateFileExerciseEntryType.MultipleChoice
            };

            return exerciseState;
        }


        public override bool Update(LabConfigurationExerciseEntry exercise)
        {
            return true;
        }

        public override bool ValidateInput(LabConfigurationExerciseEntry exercise, object input)
        {
            if(!(exercise is LabConfigurationMultipleChoiceExerciseEntry multipleChoiceExercise))
                throw new ArgumentException("Invalid exercise type", nameof(exercise));
            if(!(input is IEnumerable<int> selectedOptions))
                throw new ArgumentException("Invalid input type", nameof(input));
            var selectedIds = selectedOptions
                .OrderBy(id => id)
                .ToArray();

            // Extract correct options
            var correctIds = multipleChoiceExercise.Options
                .Select((option, index) => (option, index))
                .Where(opt => opt!.option.Correct) // IDE says this may be null, which is wrong
                .Select(opt => opt.index)
                .OrderBy(id => id)
                .ToArray();
            
            // Compare
            if(multipleChoiceExercise.RequireAll)
                return selectedIds.SequenceEqual(correctIds);
            return selectedIds.Any() && selectedIds.All(id => correctIds.Contains(id));
        }

        public override string FormatDescriptionString(LabConfigurationExerciseEntry exercise)
        {
            return exercise.DescriptionFormat ?? "";
        }
    }
}