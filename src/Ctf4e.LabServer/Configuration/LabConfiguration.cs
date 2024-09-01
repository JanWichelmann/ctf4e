using System.Collections.Generic;
using Ctf4e.LabServer.Configuration.Exercises;

namespace Ctf4e.LabServer.Configuration;

public class LabConfiguration
{
    public List<LabConfigurationExerciseEntry> Exercises { get; set; }
}