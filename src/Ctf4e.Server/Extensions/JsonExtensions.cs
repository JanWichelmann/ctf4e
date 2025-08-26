using System.Security.Claims;
using System.Text.Json;

namespace Ctf4e.Server.Extensions;

public static class JsonExtensions
{
    public static bool TryGetJsonProperty(this Claim claim, string propertyName, out string value)
    {
        if(claim.Properties.TryGetValue(propertyName, out value))
            return true;

        try
        {
            var json = JsonDocument.Parse(claim.Value);
            if(json.RootElement.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryAsJsonElement(this string str, out JsonElement element)
    {
        try
        {
            element = JsonDocument.Parse(str).RootElement;
            return true;
        }
        catch
        {
            element = default;
            return false;
        }
    }
}