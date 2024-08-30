using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ctf4e.Utilities;

public class CustomDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.ParseExact(reader.GetString(), DateTimeFormats.FullDateAndTimeGeneric, null);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateTimeFormats.FullDateAndTimeGeneric));
    }
}