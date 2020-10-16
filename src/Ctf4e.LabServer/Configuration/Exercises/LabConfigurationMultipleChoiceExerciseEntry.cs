using System.Collections.Generic;
using System.Linq;

namespace Ctf4e.LabServer.Configuration.Exercises
{
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
                error = $"Für die Aufgabe \"{Title}\" (#{Id}) existieren keine Optionen.";
                return false;
            }

            // Make sure that there is at least one correct option
            if(!Options.Any(o => o.Correct))
            {
                error = $"Für die Aufgabe \"{Title}\" (#{Id}) existiert keine korrekte Option.";
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
}