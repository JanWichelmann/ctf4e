using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Ctf4e.LessonServer.Configuration.Exercises;

namespace Ctf4e.LessonServer.Models.State
{
    public class UserStateFileScriptExerciseEntry: UserStateFileExerciseEntry
    {
        public static UserStateFileScriptExerciseEntry CreateState(LessonConfigurationScriptExerciseEntry exercise)
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

        public override bool Update(LessonConfigurationExerciseEntry exercise)
        {
            if(!(exercise is LessonConfigurationScriptExerciseEntry scriptExercise))
                throw new ArgumentException("Invalid exercise type", nameof(exercise));
            
            // No changes required
            return false;
        }

        public override bool ValidateInput(LessonConfigurationExerciseEntry exercise, object input)
        {
            // Input validation is left to state service
            throw new NotSupportedException("Script exercises must be checked using the Docker service.");
        }

        public override string FormatDescriptionString(LessonConfigurationExerciseEntry exercise)
        {
            if(exercise.DescriptionFormat == null)
                return "";
            return exercise.DescriptionFormat;
        }
    }
}