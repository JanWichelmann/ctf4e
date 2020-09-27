namespace Ctf4e.LabServer.Models.State
{
    /// <summary>
    /// Defines the structure of the user state JSON file.
    /// </summary>
    public class UserStateFile
    {
        public int? GroupId { get; set; }
        public UserStateFileExerciseEntry[] Exercises { get; set; }
    }
}