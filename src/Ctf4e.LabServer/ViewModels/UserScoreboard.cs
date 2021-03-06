using Ctf4e.LabServer.Configuration.Exercises;
using Ctf4e.LabServer.Models.State;

namespace Ctf4e.LabServer.ViewModels
{
    public class UserScoreboard
    {
        public string DockerUserName { get; set; }
        public string DockerPassword { get; set; }
        public UserScoreboardExerciseEntry[] Exercises { get; set; }
        public UserStateLogContainer Log { get; set; }
     }

    public class UserScoreboardExerciseEntry
    {
        public LabConfigurationExerciseEntry Exercise { get; set; }
        public bool Solved { get; set; }
        public bool SolvedByGroupMember { get; set; }
        public string Description { get; set; }
    }
}
