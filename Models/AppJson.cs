using System.Text.Json;

namespace NoodlesSimulator.Models;

internal static class AppJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
