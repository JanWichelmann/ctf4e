using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ctf4e.LabServer.Models.State;

public class ExerciseEntryJsonConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotSupportedException();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        JObject jsonObject = JObject.Load(reader);
        var exerciseType = jsonObject[nameof(UserStateFileExerciseEntry.Type)]?.ToObject<UserStateFileExerciseEntryType>();

        UserStateFileExerciseEntry exerciseState = exerciseType switch
        {
            UserStateFileExerciseEntryType.String => new UserStateFileStringExerciseEntry(),
            UserStateFileExerciseEntryType.MultipleChoice => new UserStateFileMultipleChoiceExerciseEntry(),
            UserStateFileExerciseEntryType.Script => new UserStateFileScriptExerciseEntry(),
            _ => throw new JsonSerializationException("Unknown exercise state type")
        };

        serializer.Populate(jsonObject.CreateReader(), exerciseState);

        return exerciseState;
    }

    public override bool CanConvert(Type objectType)
    {
        return typeof(UserStateFileExerciseEntry).IsAssignableFrom(objectType);
    }

    public override bool CanWrite { get; } = false;
}