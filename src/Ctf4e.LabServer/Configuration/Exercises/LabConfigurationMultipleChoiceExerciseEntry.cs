using System.Linq;

namespace Ctf4e.LabServer.Configuration.Exercises;

public class LabConfigurationMultipleChoiceExerciseEntry : LabConfigurationExerciseEntry
{
    /// <summary>
    /// Controls whether all correct options have to be selected (true), or any subset of the correct is sufficient (false).
    /// </summary>
    public bool RequireAll { get; set; }

    public LabConfigurationMultipleChoiceExerciseOptionEntry[] Options { get; set; }

    public override bool Validate(out string error)
    {
        // There must be at least one option
        if(!Options.Any())
        {
            error = $"Exercise \"{Title}\" (#{Id}): There are no options.";
            return false;
        }

        // Make sure that there is at least one correct option
        if(!Options.Any(o => o.Correct))
        {
            error = $"Exercise \"{Title}\" (#{Id}): There is no correct option.";
            return false;
        }

        return base.Validate(out error);
    }
}

public class LabConfigurationMultipleChoiceExerciseOptionEntry
{
    public bool Correct { get; set; }
    public string Value { get; set; }
}