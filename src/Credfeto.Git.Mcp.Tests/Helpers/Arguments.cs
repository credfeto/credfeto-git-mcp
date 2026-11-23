using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Credfeto.Git.Mcp.Tests.Helpers;

internal static class Arguments
{
    public static Dictionary<string, JsonElement> None()
    {
        return [with(StringComparer.Ordinal)];
    }

    // The arguments of a tools/call, written as the JSON object a client would send
    public static Dictionary<string, JsonElement> Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        Dictionary<string, JsonElement> arguments = [with(StringComparer.Ordinal)];

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            arguments[property.Name] = property.Value.Clone();
        }

        return arguments;
    }
}
