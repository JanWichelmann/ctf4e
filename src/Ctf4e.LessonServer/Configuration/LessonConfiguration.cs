using System.Collections.Generic;
using Ctf4e.LessonServer.Configuration.Exercises;

namespace Ctf4e.LessonServer.Configuration
{
    public class LessonConfiguration
    {
        public bool PassAsGroup { get; set; }

        public string PageTitle { get; set; }
        
        public string NavbarTitle { get; set; }
        
        public List<LessonConfigurationExerciseEntry> Exercises { get; set; }
    }
}