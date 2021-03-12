using System.Collections.Generic;
using System.Linq;

namespace Ctf4e.LabServer.Configuration.Exercises
{
    /// <summary>
    /// Expects and evaluates a string input for a static list of possible solutions.
    /// </summary>
    public class LabConfigurationStringExerciseEntry : LabConfigurationExerciseEntry
    {
        /// <summary>
        /// Controls whether the user is allowed to enter any of the valid solutions to pass (true),
        /// or whether only a certain, randomly picked solution is allowed.
        /// </summary>
        public bool AllowAnySolution { get; set; }
        
        /// <summary>
        /// Controls whether the solutions are treated as regular expressions.
        /// </summary>
        public bool UseRegex { get; set; }
        
        /// <summary>
        /// Controls whether the exercise allows multiline inputs. Regular expressions are not affected (explicitly test for \n).
        /// </summary>
        public bool Multiline { get; set; }

        public LabOptionsStringExerciseSolutionEntry[] ValidSolutions { get; set; }

        public override bool Validate(out string error)
        {
            // There must be at least one solution
            if(!ValidSolutions.Any())
            {
                error = $"Exercise \"{Title}\" (#{Id}): There are no solutions.";
                return false;
            }

            // Make sure that solutions have names, if a specific one must be found
            HashSet<string> solutionNames = new HashSet<string>();
            foreach(var solution in ValidSolutions)
            {
                if(!AllowAnySolution)
                {
                    if(solution.Name == null)
                    {
                        error = $"Solution \"{solution.Value}\" for exercise \"{Title}\" (#{Id}): Missing name.";
                        return false;
                    }

                    if(solutionNames.Contains(solution.Name))
                    {
                        error = $"Exercise \"{Title}\" (#{Id}): The solution name \"{solution.Value}\" is ambiguous.";
                        return false;
                    }

                    solutionNames.Add(solution.Name);
                }
            }

            return base.Validate(out error);
        }
    }

    public class LabOptionsStringExerciseSolutionEntry
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}