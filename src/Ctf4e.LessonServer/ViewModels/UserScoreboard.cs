using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.LessonServer.Configuration.Exercises;
using Ctf4e.LessonServer.Models.State;

namespace Ctf4e.LessonServer.ViewModels
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
        public LessonConfigurationExerciseEntry Exercise { get; set; }
        public bool Solved { get; set; }
        public bool SolvedByGroupMember { get; set; }
        public string Description { get; set; }
    }
}
