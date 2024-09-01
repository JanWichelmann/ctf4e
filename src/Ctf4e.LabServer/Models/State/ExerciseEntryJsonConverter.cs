using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ctf4e.LabServer.Models.State;

public class ExerciseEntryJsonConverter : JsonConverter<UserStateFileExerciseEntry>
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(UserStateFileExerciseEntry).IsAssignableFrom(objectType);
    }

    public override UserStateFileExerciseEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonObject = JsonNode.Parse(ref reader);
        if(jsonObject.GetValueKind() != JsonValueKind.Object)
            throw new InvalidDataException($"Expected JSON object, but got value kind {jsonObject.GetValueKind()}");
        var exerciseType = jsonObject[nameof(UserStateFileExerciseEntry.Type)].Deserialize<UserStateFileExerciseEntryType>();

        UserStateFileExerciseEntry exerciseState = exerciseType switch
        {
            UserStateFileExerciseEntryType.String => jsonObject.Deserialize<UserStateFileStringExerciseEntry>(),
            UserStateFileExerciseEntryType.MultipleChoice => jsonObject.Deserialize<UserStateFileMultipleChoiceExerciseEntry>(),
            UserStateFileExerciseEntryType.Script => jsonObject.Deserialize<UserStateFileScriptExerciseEntry>(),
            _ => throw new InvalidDataException("Unknown exercise state type")
        };

        return exerciseState;
    }

    public override void Write(Utf8JsonWriter writer, UserStateFileExerciseEntry value, JsonSerializerOptions options)
    {
        // We use the default serialization for writes
        throw new NotSupportedException();
    }
}