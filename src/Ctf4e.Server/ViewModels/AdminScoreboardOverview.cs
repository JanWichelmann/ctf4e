using System.Collections.Generic;

namespace Ctf4e.Server.ViewModels;

/// <summary>
/// Contains a summary of all group/user scores for a specific lab/slot.
/// </summary>
public class AdminScoreboardOverview
{
    public int LabId { get; set; }
    public int SlotId { get; set; }
    
    public bool PassAsGroup { get; set; }
    
    public int MandatoryExercisesCount { get; set; }

    public int OptionalExercisesCount { get; set; }

    public int FlagCount { get; set; }
    
    public List<UserEntry> UserEntries { get; set; }
    public List<GroupEntry> GroupEntries { get; set; }

    public class UserEntry
    {
        public int Id { get; set; }
        public string DisplayName { get; set; }

        public LabExecutionStatus Status { get; set; }

        public bool HasPassed { get; set; }

        public int PassedMandatoryExercisesCount { get; set; }

        public int PassedOptionalExercisesCount { get; set; }

        public int FoundFlagsCount { get; set; }
        
        public GroupEntry Group { get; set; }
    }
    
    public class GroupEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public LabExecutionStatus Status { get; set; }

        public bool HasPassed { get; set; }

        public int PassedMandatoryExercisesCount { get; set; }

        public int PassedOptionalExercisesCount { get; set; }

        public int FoundFlagsCount { get; set; }
        
        public List<UserEntry> Members { get; set; }
    }
}