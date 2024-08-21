using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Ctf4e.Utilities;

public static class TempDataExtensions
{
    public static void SetJson<T>(this ITempDataDictionary tempData, string key, T value) where T : struct
    {
        tempData[key] = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });
    }

    public static T? GetJson<T>(this ITempDataDictionary tempData, string key) where T : struct
    {
        if(tempData.TryGetValue(key, out var value) && value is string str)
            return JsonSerializer.Deserialize<T>(str);
        return null;
    }
}