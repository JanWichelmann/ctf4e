using System.Text.Json.Serialization;

namespace Ctf4e.Utilities;

public class StatusMessage(StatusMessageType type, string message)
{
    [JsonPropertyName("t")]
    public StatusMessageType Type { get; init; } = type;
    
    [JsonPropertyName("m")]
    public string Message { get; init; } = message;
    
    [JsonPropertyName("p")]
    public bool Preformatted { get; init; } = false;
    
    [JsonPropertyName("a")]
    public bool AutoHide { get; init; } = false;
}