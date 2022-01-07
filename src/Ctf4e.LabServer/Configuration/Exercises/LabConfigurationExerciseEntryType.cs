using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ctf4e.LabServer.Configuration.Exercises;

[JsonConverter(typeof(StringEnumConverter))]
public enum LabConfigurationExerciseEntryType
{
    String,
    MultipleChoice,
    Script
}