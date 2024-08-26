using System;
using System.Collections.Generic;
using Ctf4e.Server.Models;

namespace Ctf4e.Server.ViewModels;

public class UserDashboard
{
    public int LabId { get; set; }

    public UserScoreboardLabEntry CurrentLab { get; set; }

    public List<UserScoreboardLabEntry> Labs { get; set; }

    public ScoreboardGroupStatus LabExecutionStatus { get; set; }

    public LabExecution LabExecution { get; set; }
    
    public bool PassAsGroupEnabled { get; set; }

    public bool HasPassed { get; set; }

    public List<UserScoreboardExerciseEntry> Exercises { get; set; }

    public int PassedMandatoryExercisesCount { get; set; }

    public int PassedOptionalExercisesCount { get; set; }

    public int FoundFlagsCount { get; set; }

    public int ValidFoundFlagsCount { get; set; }
        
    public bool HasFoundAllFlags { get; set; }

    public Dictionary<int, string> GroupMembers { get; set; }
        
    public List<UserScoreboardFlagEntry> Flags { get; set; }
}

public class UserScoreboardLabEntry
{
    public int LabId { get; set; }

    public string Name { get; set; }

    public string ServerBaseUrl { get; set; }

    public bool Active { get; set; }
        
    public bool Visible { get; set; }
}

public class UserScoreboardExerciseEntry
{
    public Exercise Exercise { get; set; }

    /// <summary>
    /// Total valid tries by this user and their group members.
    /// </summary>
    public int Tries { get; set; }

    public int ValidTries { get; set; }

    /// <summary>
    /// Determines whether the user has successfully solved the exercise (or a group member, when passing as group is enabled).
    /// </summary>
    public bool Passed { get; set; }
    
    /// <summary>
    /// Determines that the user or one of their group members has passed.
    /// This is used for displaying the earned points even when the user has not yet passed.
    /// </summary>
    public bool GroupMemberHasPassed { get; set; }

    public int Points { get; set; }

    public List<ExerciseSubmission> Submissions { get; set; }
}

public class UserScoreboardFlagEntry
{
    public int FlagId { get; set; }
        
    public string FlagCode { get; set; }
        
    public int UserId { get; set; }
        
    public DateTime SubmissionTime { get; set; }
        
    public bool Valid { get; set; }
}