using System.Collections.Generic;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.Configuration
{
    public class LabConfiguration
    {
        public bool PassAsGroup { get; set; }

        public string PageTitle { get; set; }
        
        public string NavbarTitle { get; set; }
        
        public List<LabConfigurationExerciseEntry> Exercises { get; set; }
    }
}