namespace Ctf4e.LabServer.Models.State
{
    /// <summary>
    /// Contains the state of one exercise.
    /// </summary>
    public class UserStateFileExerciseEntry
    {
        public int ExerciseId { get; set; }
        public bool Solved { get; set; }
        public string SolutionName { get; set; }
    }
}