using System;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.Models.State
{
    public class UserStateFileScriptExerciseEntry: UserStateFileExerciseEntry
    {
        public static UserStateFileScriptExerciseEntry CreateState(LabConfigurationScriptExerciseEntry exercise)
        {
            // Create empty state for this exercise
            var exerciseState = new UserStateFileScriptExerciseEntry
            {
                ExerciseId = exercise.Id,
                Solved = false,
                Type = UserStateFileExerciseEntryType.Script
            };

            return exerciseState;
        }

        public override bool Update(LabConfigurationExerciseEntry exercise)
        {
            if(!(exercise is LabConfigurationScriptExerciseEntry))
                throw new ArgumentException("Invalid exercise type", nameof(exercise));
            
            // No changes required
            return false;
        }

        public override bool ValidateInput(LabConfigurationExerciseEntry exercise, object input)
        {
            // Input validation is left to state service
            throw new NotSupportedException("Script exercises must be checked using the Docker service.");
        }

        public override string FormatDescriptionString(LabConfigurationExerciseEntry exercise)
        {
            if(exercise.DescriptionFormat == null)
                return "";
            return exercise.DescriptionFormat;
        }
    }
}