using System;
using System.Collections.Generic;
using Ctf4e.Server.Models;

namespace Ctf4e.Server.ViewModels
{
    public class UserScoreboard
    {
        public int LessonId { get; set; }

        public UserScoreboardLessonEntry CurrentLesson { get; set; }

        public List<UserScoreboardLessonEntry> Lessons { get; set; }

        public ScoreboardGroupStatus LessonExecutionStatus { get; set; }

        public LessonExecution LessonExecution { get; set; }

        public bool HasPassed { get; set; }

        public List<ScoreboardUserExerciseEntry> Exercises { get; set; }

        public int PassedMandatoryExercisesCount { get; set; }

        public int PassedOptionalExercisesCount { get; set; }

        public int FoundFlagsCount { get; set; }

        public int ValidFoundFlagsCount { get; set; }

        public Dictionary<int, string> GroupMembers { get; set; }
        
        public List<UserScoreboardFlagEntry> Flags { get; set; }
    }

    public class UserScoreboardLessonEntry
    {
        public int LessonId { get; set; }

        public string Name { get; set; }

        public string ServerBaseUrl { get; set; }

        public bool Active { get; set; }
    }

    public class UserScoreboardFlagEntry
    {
        public int FlagId { get; set; }
        
        public string FlagCode { get; set; }
        
        public int UserId { get; set; }
        
        public DateTime SubmissionTime { get; set; }
        
        public bool Valid { get; set; }
    }
}