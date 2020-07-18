using System;
using System.Collections.Generic;
using Ctf4e.Server.Models;

namespace Ctf4e.Server.ViewModels
{
    public class AdminScoreboard
    {
        public List<Lab> Labs { get; set; }

        public int LabId { get; set; }

        public List<Slot> Slots { get; set; }

        public int SlotId { get; set; }

        public int MandatoryExercisesCount { get; set; }

        public int OptionalExercisesCount { get; set; }

        public int FlagCount { get; set; }

        public List<AdminScoreboardFlagEntry> Flags { get; set; }

        public List<AdminScoreboardGroupEntry> GroupEntries { get; set; }
    }

    public class AdminScoreboardFlagEntry
    {
        public Flag Flag { get; set; }

        public int SubmissionCount { get; set; }

        public int CurrentPoints { get; set; }
    }

    public class AdminScoreboardGroupEntry
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }

        public ScoreboardGroupStatus Status { get; set; }

        public bool HasPassed { get; set; }

        public List<ScoreboardGroupExerciseEntry> Exercises { get; set; }

        public int PassedMandatoryExercisesCount { get; set; }

        public int PassedOptionalExercisesCount { get; set; }

        public int FoundFlagsCount { get; set; }

        public List<AdminScoreboardGroupFlagEntry> Flags { get; set; }
    }

    public enum ScoreboardGroupStatus
    {
        Undefined,
        BeforePreStart,
        PreStart,
        Start,
        End
    }
    
    public class ScoreboardGroupExerciseEntry
    {
        public Exercise Exercise { get; set; }
      
        public int Tries { get; set; }

        public int ValidTries { get; set; }

        public bool Passed { get; set; }

        public int Points { get; set; }

        public List<ExerciseSubmission> Submissions { get; set; }
    }

    public class AdminScoreboardGroupFlagEntry
    {
        public Flag Flag { get; set; }

        public bool Submitted { get; set; }

        public bool Valid { get; set; }

        public int CurrentPoints { get; set; }

        public DateTime SubmissionTime { get; set; }
    }
}
