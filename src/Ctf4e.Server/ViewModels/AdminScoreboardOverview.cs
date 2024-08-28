using System.Collections.Generic;

namespace Ctf4e.Server.ViewModels;

public class AdminScoreboardOverview
{
    public List<ExerciseListEntry> Exercises { get; set; }
    
    public List<FlagListEntry> Flags { get; set; }
    
    public class ExerciseListEntry
    {
        public int Id { get; set; }

        public int ExerciseNumber { get; set; }

        public string Name { get; set; }

        public bool IsMandatory { get; set; }

        public int BasePoints { get; set; }

        public int PenaltyPoints { get; set; }

        public int PassedCount { get; set; }
    }

    public class FlagListEntry
    {
        public int Id { get; set; }

        public string Description { get; set; }

        public bool IsBounty { get; set; }

        public int CurrentPoints { get; set; }

        public int SubmissionCount { get; set; }
    }
}