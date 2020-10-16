using System.Linq;
using System.Security.Cryptography;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.Models.State
{
    public class UserStateFileStringExerciseEntry : UserStateFileExerciseEntry
    {
        public string SolutionName { get; set; }

        public static UserStateFileStringExerciseEntry CreateState(LabConfigurationStringExerciseEntry exercise)
        {
            // Create empty state for this exercise
            var exerciseState = new UserStateFileStringExerciseEntry
            {
                ExerciseId = exercise.Id,
                Solved = false,
                Type = UserStateFileExerciseEntryType.String
            };

            // Require a specific, randomly picked solution?
            if(!exercise.AllowAnySolution)
            {
                var random = exercise.ValidSolutions[RandomNumberGenerator.GetInt32(exercise.ValidSolutions.Length)];
                exerciseState.SolutionName = random.Name;
            }

            return exerciseState;
        }

        /// <summary>
        /// Checks whether the internal state is still consistent with the given exercise data, and updates it if necessary.
        /// The return value indicates whether the state has been updated.
        /// </summary>
        /// <param name="exercise">Exercise data.</param>
        public bool Update(LabConfigurationStringExerciseEntry exercise)
        {
            // Is the expected solution still valid?
            if(exercise.AllowAnySolution)
            {
                // We allow any solution, so no need to store a name
                if(SolutionName != null)
                {
                    // Drop previous solution name
                    SolutionName = null;
                    return true;
                }
            }
            else
            {
                // Check whether the exercise requires a specific, existing solution
                if(SolutionName == null || exercise.ValidSolutions.All(s => s.Name != SolutionName))
                {
                    // Pick a random solution
                    var random = exercise.ValidSolutions[RandomNumberGenerator.GetInt32(exercise.ValidSolutions.Length)];
                    SolutionName = random.Name;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether the given input string is a valid solution.
        /// </summary>
        /// <param name="exercise">Exercise data.</param>
        /// <param name="input">Input.</param>
        /// <returns></returns>
        public bool ValidateInput(LabConfigurationStringExerciseEntry exercise, string input)
        {
            if(exercise.AllowAnySolution)
                return exercise.ValidSolutions.Any(s => s.Value == input);

            return exercise.ValidSolutions.FirstOrDefault(s => s.Name == SolutionName)?.Value == input;
        }

        /// <summary>
        /// Returns a description string for the given exercise, with placeholders replaced by values from this state.
        /// </summary>
        /// <param name="exercise">Exercise data.</param>
        /// <returns></returns>
        public string FormatDescriptionString(LabConfigurationStringExerciseEntry exercise)
        {
            if(exercise.DescriptionFormat == null)
                return "";
            return string.Format(exercise.DescriptionFormat, SolutionName);
        }
    }
}