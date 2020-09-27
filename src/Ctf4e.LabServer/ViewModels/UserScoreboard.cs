using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.LabServer.Options;

namespace Ctf4e.LabServer.ViewModels
{
    public class UserScoreboard
    {
        public UserScoreboardExerciseEntry[] Exercises { get; set; }
    }

    public class UserScoreboardExerciseEntry
    {
        public LabOptionsExerciseEntry Exercise { get; set; }
        public bool Solved { get; set; }
        public bool SolvedByGroupMember { get; set; }
        public string Description { get; set; }
    }
}
