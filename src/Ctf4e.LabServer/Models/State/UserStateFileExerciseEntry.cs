using Newtonsoft.Json;

namespace Ctf4e.LabServer.Models.State
{
    /// <summary>
    /// Contains the state of one exercise.
    /// </summary>
    [JsonConverter(typeof(ExerciseEntryJsonConverter))]
    public abstract class UserStateFileExerciseEntry
    {
        public int ExerciseId { get; set; }
        public bool Solved { get; set; }
        public UserStateFileExerciseEntryType Type { get; set; }
    }
}