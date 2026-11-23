using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Credfeto.Git.Mcp.Tools;

// Hand-built JSON schemas with additionalProperties false: no reflection, and nothing a tool does not declare
public static class ToolSchema
{
    public static JsonElement Create(IReadOnlyList<SchemaProperty> properties)
    {
        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(propertyName: "type", value: "object");
            writer.WriteStartObject("properties");

            foreach (SchemaProperty property in properties)
            {
                WriteProperty(writer: writer, property: property);
            }

            writer.WriteEndObject();
            WriteRequired(writer: writer, properties: properties);
            writer.WriteBoolean(propertyName: "additionalProperties", value: false);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);

        return document.RootElement.Clone();
    }

    private static void WriteRequired(Utf8JsonWriter writer, IReadOnlyList<SchemaProperty> properties)
    {
        string[] required =
        [
            .. properties.Where(static property => property.Required).Select(static property => property.Name),
        ];

        if (required.Length == 0)
        {
            return;
        }

        writer.WriteStartArray("required");

        foreach (string name in required)
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
    }

    private static void WriteProperty(Utf8JsonWriter writer, SchemaProperty property)
    {
        writer.WriteStartObject(property.Name);
        writer.WriteString(propertyName: "type", value: property.WholeNumber ? "integer" : "string");
        writer.WriteString(propertyName: "description", value: property.Description);

        if (property.WholeNumber)
        {
            writer.WriteNumber(propertyName: "minimum", value: property.Minimum);
            writer.WriteNumber(propertyName: "maximum", value: property.Maximum);
            writer.WriteNumber(propertyName: "default", value: property.DefaultNumber);
        }
        else
        {
            WriteChoice(writer: writer, property: property);
        }

        writer.WriteEndObject();
    }

    private static void WriteChoice(Utf8JsonWriter writer, SchemaProperty property)
    {
        if (property.Values is not null)
        {
            writer.WriteStartArray("enum");

            foreach (string value in property.Values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        if (property.DefaultValue is not null)
        {
            writer.WriteString(propertyName: "default", value: property.DefaultValue);
        }
    }
}
