
using System.Text.Json.Serialization;

namespace Ctf4e.LabServer.Models.State;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserStateFileExerciseEntryType
{
    String,
    MultipleChoice,
    Script
}