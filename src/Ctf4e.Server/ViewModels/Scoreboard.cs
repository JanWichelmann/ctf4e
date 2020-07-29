using System;
using System.Collections.Generic;
using Ctf4e.Server.Models;

namespace Ctf4e.Server.ViewModels
{
    public class Scoreboard
    {
        public DateTime ValidUntil { get; set; }

        public int LabId { get; set; }

        public string CurrentLabName { get; set; }

        public bool AllLabs { get; set; }

        public int MaximumEntryCount { get; set; }

        public List<ScoreboardEntry> Entries { get; set; }

        public Dictionary<int, ScoreboardFlagEntry> Flags { get; set; }
    }

    public class ScoreboardEntry
    {
        public int SlotId { get; set; }

        public int Rank { get; set; }

        public int GroupId { get; set; }

        public string GroupName { get; set; }

        public int ExercisePoints { get; set; }

        public int FlagPoints { get; set; }

        public int BugBountyPoints { get; set; }

        public int TotalPoints { get; set; }

        public int FlagCount { get; set; }

        public DateTime LastExerciseSubmissionTime { get; set; }

        public DateTime LastFlagSubmissionTime { get; set; }

        public DateTime LastSubmissionTime { get; set; }
    }

    public class ScoreboardFlagEntry
    {
        public Flag Flag { get; set; }

        public int SubmissionCount { get; set; }

        public int CurrentPoints { get; set; }
    }
}