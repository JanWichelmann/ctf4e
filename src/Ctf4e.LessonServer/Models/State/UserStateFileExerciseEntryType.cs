using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ctf4e.LessonServer.Models.State
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum UserStateFileExerciseEntryType
    {
        String,
        MultipleChoice,
        Script
    }
}