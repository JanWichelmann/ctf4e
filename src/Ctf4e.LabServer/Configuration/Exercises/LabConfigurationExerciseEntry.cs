using System.Text.Json.Serialization;

namespace Ctf4e.LabServer.Configuration.Exercises;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LabConfigurationStringExerciseEntry), "String")]
[JsonDerivedType(typeof(LabConfigurationMultipleChoiceExerciseEntry), "MultipleChoice")]
[JsonDerivedType(typeof(LabConfigurationScriptExerciseEntry), "Script")]
public abstract class LabConfigurationExerciseEntry
{
    /// <summary>
    /// ID of this exercise. This is used to keep track of already solved exercises, and thus should not be changed during runtime.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Used to identify the lab's exercises in the CTF system. Can be null.
    /// </summary>
    public int? CtfExerciseNumber { get; set; }

    /// <summary>
    /// Optional. Flag code to show when this exercise is solved. Needs to be present when <see cref="CtfExerciseNumber"/> is null.
    /// </summary>
    public string FlagCode { get; set; }

    /// <summary>
    /// Exercise title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Optional. Format string for the exercise description. {0} can be used as a placeholder for a solution name.
    /// This field allows HTML.
    /// </summary>
    public string DescriptionFormat { get; set; }

    /// <summary>
    /// Optional. URL which needs to visited for solving this exercise.
    /// </summary>
    public string Link { get; set; }

    /// <summary>
    /// Checks whether all parameters of this instance are valid. Used for testing sanity of a new configuration file.
    /// </summary>
    /// <returns></returns>
    public virtual bool Validate(out string error)
    {
        // Exercises should either point to a CTF exercise or a flag code
        if(CtfExerciseNumber == null && FlagCode == null)
        {
            error = $"Exercise \"{Title}\" (#{Id}): There is neither a CTF exercise number nor a flag code.";
            return false;
        }

        error = null;
        return true;
    }
}