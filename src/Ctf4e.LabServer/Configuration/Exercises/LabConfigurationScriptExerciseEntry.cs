namespace Ctf4e.LabServer.Configuration.Exercises;

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
    
    /// <summary>
    /// Overrides the container name specified in the lab options, to execute the grading script on.
    /// </summary>
    public string ContainerName { get; set; }
    
    /// <summary>
    /// Overrides the grading script path specified in the lab options.
    /// </summary>
    public string GradeScriptPath { get; set; }
}