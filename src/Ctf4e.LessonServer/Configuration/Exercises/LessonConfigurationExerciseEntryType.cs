using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ctf4e.LessonServer.Configuration.Exercises
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LessonConfigurationExerciseEntryType
    {
        String,
        MultipleChoice,
        Script
    }
}