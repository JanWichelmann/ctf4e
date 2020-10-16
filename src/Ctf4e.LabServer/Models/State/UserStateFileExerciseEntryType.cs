using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ctf4e.LabServer.Models.State
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum UserStateFileExerciseEntryType
    {
        String
    }
}