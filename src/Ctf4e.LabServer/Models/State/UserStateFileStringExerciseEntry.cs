using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.Models.State;

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

    public override bool Update(LabConfigurationExerciseEntry exercise)
    {
        if(!(exercise is LabConfigurationStringExerciseEntry stringExercise))
            throw new ArgumentException("Invalid exercise type", nameof(exercise));
            
        // Is the expected solution still valid?
        if(stringExercise.AllowAnySolution)
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
            if(SolutionName == null || stringExercise.ValidSolutions.All(s => s.Name != SolutionName))
            {
                // Pick a random solution
                var random = stringExercise.ValidSolutions[RandomNumberGenerator.GetInt32(stringExercise.ValidSolutions.Length)];
                SolutionName = random.Name;
                return true;
            }
        }

        return false;
    }

    public override bool ValidateInput(LabConfigurationExerciseEntry exercise, object input)
    {
        if(!(exercise is LabConfigurationStringExerciseEntry stringExercise))
            throw new ArgumentException("Invalid exercise type", nameof(exercise));
        if(!(input is string inputStr))
            throw new ArgumentException("Invalid input type", nameof(input));
            
        if(stringExercise.UseRegex)
        {
            // Match any solution
            if(stringExercise.AllowAnySolution)
                return stringExercise.ValidSolutions.Any(s => Regex.IsMatch(inputStr, s.Value));

            // Match specific solution
            var pattern = stringExercise.ValidSolutions.FirstOrDefault(s => s.Name == SolutionName)?.Value;
            if(pattern == null)
                return false;
            return Regex.IsMatch(inputStr, pattern);
        }

        // Compare against any solution
        if(stringExercise.AllowAnySolution)
            return stringExercise.ValidSolutions.Any(s => s.Value == inputStr);

        // Compare against specific solutions
        return stringExercise.ValidSolutions.FirstOrDefault(s => s.Name == SolutionName)?.Value == inputStr;
    }

    public override string FormatDescriptionString(LabConfigurationExerciseEntry exercise)
    {
        if(exercise.DescriptionFormat == null)
            return "";
        return string.Format(exercise.DescriptionFormat, SolutionName);
    }
}