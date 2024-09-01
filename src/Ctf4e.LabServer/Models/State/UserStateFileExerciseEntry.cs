using System.Text.Json.Serialization;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.Models.State;

/// <summary>
/// Contains the state of one exercise.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(UserStateFileStringExerciseEntry), "String")]
[JsonDerivedType(typeof(UserStateFileMultipleChoiceExerciseEntry), "MultipleChoice")]
[JsonDerivedType(typeof(UserStateFileScriptExerciseEntry), "Script")]
public abstract class UserStateFileExerciseEntry
{
    public int ExerciseId { get; set; }
    public bool Solved { get; set; }
        
    /// <summary>
    /// Checks whether the internal state is still consistent with the given exercise data, and updates it if necessary.
    /// The return value indicates whether the state has been updated.
    /// </summary>
    /// <param name="exercise">Exercise data.</param>
    public abstract bool Update(LabConfigurationExerciseEntry exercise);

    /// <summary>
    /// Returns whether the given input is a valid solution.
    /// </summary>
    /// <param name="exercise">Exercise data.</param>
    /// <param name="input">Input.</param>
    /// <returns></returns>
    public abstract bool ValidateInput(LabConfigurationExerciseEntry exercise, object input);

    /// <summary>
    /// Returns a description string for the given exercise, with placeholders replaced by values from this state.
    /// </summary>
    /// <param name="exercise">Exercise data.</param>
    /// <returns></returns>
    public abstract string FormatDescriptionString(LabConfigurationExerciseEntry exercise);
}