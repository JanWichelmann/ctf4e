using System;
using System.Collections.Generic;

namespace Ctf4e.Server.ViewModels;

/// <summary>
/// Used for the group/user dashboards. Contains all submissions of a given lab.
/// </summary>
public class AdminScoreboardDetails
{
    public int LabId { get; set; }
    public int SlotId { get; set; }
    public int GroupId { get; set; }
    public string GroupName { get; set; }
    
    public List<(int Id, string DisplayName)> GroupMembers { get; set; }

    /// <summary>
    /// Only set in user dashboard.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Only set in user dashboard.
    /// </summary>
    public string UserName { get; set; }

    public bool PassAsGroup { get; set; }
    public bool HasPassed { get; set; }

    public LabExecutionStatus Status { get; set; }

    public List<ExerciseEntry> Exercises { get; set; }
    public List<FlagEntry> Flags { get; set; }

    public class ExerciseEntry
    {
        public int Id { get; set; }
        public string ExerciseName { get; set; }
        public bool IsMandatory { get; set; }
        public int BasePoints { get; set; }
        public int PenaltyPoints { get; set; }

        public int Tries { get; set; }
        public int ValidTries { get; set; }
        public bool Passed { get; set; }
        public int Points { get; set; }

        public List<ExerciseSubmissionEntry> Submissions { get; set; }
    }

    public class ExerciseSubmissionEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public bool Solved { get; set; }
        public DateTime SubmissionTime { get; set; }
        public bool Valid { get; set; }
        public int Weight { get; set; }
        public bool CreatedByAdmin { get; set; }
    }

    public class FlagEntry
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public bool IsBounty { get; set; }

        public bool Submitted { get; set; }
        public bool Valid { get; set; }
        public int Points { get; set; }

        public List<FlagSubmissionEntry> Submissions { get; set; }
    }

    public class FlagSubmissionEntry
    {
        public int UserId { get; set; }
        public string UserName { get; set; }

        public bool Valid { get; set; }
        public DateTime SubmissionTime { get; set; }
    }
}