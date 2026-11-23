using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Credfeto.Git.Mcp.Tools.Exceptions;

namespace Credfeto.Git.Mcp.Tools;

// A tool call's arguments checked against the tool's schema: an unknown name or a value of the wrong kind is refused
// before the tool runs. Error messages are fixed text and never repeat the caller's value
public sealed class ToolArguments
{
    private readonly Dictionary<string, JsonElement> _values;

    public ToolArguments(
        IEnumerable<KeyValuePair<string, JsonElement>>? arguments,
        IReadOnlyList<SchemaProperty> schema
    )
    {
        this._values = [with(StringComparer.Ordinal)];

        foreach ((string name, JsonElement value) in arguments ?? [])
        {
            if (!schema.Any(property => StringComparer.Ordinal.Equals(x: property.Name, y: name)))
            {
                throw new ToolArgumentException("Unknown parameter; this tool takes: " + Names(schema));
            }

            if (value.ValueKind != JsonValueKind.Null)
            {
                this._values[name] = value;
            }
        }

        SchemaProperty? missing = schema.FirstOrDefault(property =>
            property.Required && !this._values.ContainsKey(property.Name)
        );

        if (missing is not null)
        {
            throw new ToolArgumentException("Missing required parameter " + missing.Name);
        }
    }

    public string GetChoice(SchemaProperty property)
    {
        string? value = this.GetString(property.Name) ?? property.DefaultValue;
        IReadOnlyList<string> allowed = property.Values ?? [];

        return value is not null && allowed.Contains(value: value, comparer: StringComparer.Ordinal)
            ? value
            : throw new ToolArgumentException(
                "Invalid value for "
                    + property.Name
                    + "; expected one of: "
                    + string.Join(separator: ", ", values: allowed)
            );
    }

    public int GetInteger(SchemaProperty property)
    {
        if (!this._values.TryGetValue(key: property.Name, value: out JsonElement value))
        {
            return property.DefaultNumber;
        }

        return
            value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int number)
            && number >= property.Minimum
            && number <= property.Maximum
            ? number
            : throw new ToolArgumentException(
                "Invalid value for "
                    + property.Name
                    + "; expected a whole number from "
                    + property.Minimum.ToString(CultureInfo.InvariantCulture)
                    + " to "
                    + property.Maximum.ToString(CultureInfo.InvariantCulture)
            );
    }

    private string? GetString(string name)
    {
        if (!this._values.TryGetValue(key: name, value: out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new ToolArgumentException("Invalid value for " + name + "; expected a string");
    }

    private static string Names(IReadOnlyList<SchemaProperty> schema)
    {
        return schema.Count == 0
            ? "no parameters"
            : string.Join(separator: ", ", values: schema.Select(static property => property.Name));
    }
}
