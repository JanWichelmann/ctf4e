using System;
using System.Collections.Generic;
using Ctf4e.Server.Models;

namespace Ctf4e.Server.ViewModels
{
    public class GroupScoreboard
    {
        public int LabId { get; set; }

        public GroupScoreboardLabEntry CurrentLab { get; set; }

        public List<GroupScoreboardLabEntry> Labs { get; set; }

        public ScoreboardGroupStatus LabExecutionStatus { get; set; }

        public LabExecution LabExecution { get; set; }

        public bool HasPassed { get; set; }

        public List<ScoreboardUserExerciseEntry> Exercises { get; set; }

        public int PassedMandatoryExercisesCount { get; set; }

        public int PassedOptionalExercisesCount { get; set; }

        public int FoundFlagsCount { get; set; }

        public int ValidFoundFlagsCount { get; set; }

        public Dictionary<int, string> GroupMembers { get; set; }
        
        public List<GroupScoreboardFlagEntry> Flags { get; set; }
    }

    public class GroupScoreboardLabEntry
    {
        public int LabId { get; set; }

        public string Name { get; set; }

        public string ServerBaseUrl { get; set; }

        public bool Active { get; set; }
    }

    public class GroupScoreboardFlagEntry
    {
        public int FlagId { get; set; }
        
        public string FlagCode { get; set; }
        
        public int UserId { get; set; }
        
        public DateTime SubmissionTime { get; set; }
        
        public bool Valid { get; set; }
    }
}