namespace Ctf4e.LabServer.Configuration.Exercises
{
    /// <summary>
    /// Executes an external script in order to test whether a solution is correct.
    /// </summary>
    public class LabConfigurationScriptExerciseEntry : LabConfigurationExerciseEntry
    {
        /// <summary>
        /// Controls whether the exercise takes string inputs. Inputs are transmitted to the grading script via stdin.
        /// </summary>
        public bool StringInput { get; set; }
        
        /// <summary>
        /// Controls whether the exercise allows multiline string inputs.
        /// </summary>
        public bool Multiline { get; set; }
    }
}