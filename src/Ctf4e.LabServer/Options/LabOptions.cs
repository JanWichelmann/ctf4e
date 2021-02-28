using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.Configuration
{
    public class LabOptions
    {
        public string CtfServerBaseUrl { get; set; }
        
        public bool DevelopmentMode { get; set; }
        
        public string UserStateDirectory { get; set; }
        
        public string LabConfigurationFile { get; set; }
        
        public string DockerContainerName { get; set; }
        
        public string DockerContainerInitUserScriptPath { get; set; }
        
        public string DockerContainerGradeScriptPath { get; set; }
    }
}
