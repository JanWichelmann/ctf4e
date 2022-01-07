using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ctf4e.LabServer.Configuration.Exercises;

public class ExerciseEntryJsonConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotSupportedException();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JObject jsonObject = JObject.Load(reader);
        var exerciseType = jsonObject[nameof(LabConfigurationExerciseEntry.Type)]?.ToObject<LabConfigurationExerciseEntryType>();

        LabConfigurationExerciseEntry exercise = exerciseType switch
        {
            LabConfigurationExerciseEntryType.String => new LabConfigurationStringExerciseEntry(),
            LabConfigurationExerciseEntryType.MultipleChoice => new LabConfigurationMultipleChoiceExerciseEntry(),
            LabConfigurationExerciseEntryType.Script => new LabConfigurationScriptExerciseEntry(),
            _ => throw new JsonSerializationException("Unknown exercise type")
        };

        serializer.Populate(jsonObject.CreateReader(), exercise);

        return exercise;
    }

    public override bool CanConvert(Type objectType)
    {
        return typeof(LabConfigurationExerciseEntry).IsAssignableFrom(objectType);
    }

    public override bool CanWrite { get; } = false;
}