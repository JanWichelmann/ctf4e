using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.ViewModels
{
    public class UserScoreboard
    {
        public UserScoreboardExerciseEntry[] Exercises { get; set; }
    }

    public class UserScoreboardExerciseEntry
    {
        public LabConfigurationExerciseEntry Exercise { get; set; }
        public bool Solved { get; set; }
        public bool SolvedByGroupMember { get; set; }
        public string Description { get; set; }
    }
}
