using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Git.Mcp.Tools;

// One tool parameter: a string from a fixed list (Values), or a whole number from Minimum to Maximum
[DebuggerDisplay("Name={Name}, Required={Required}")]
public sealed record SchemaProperty(string Name, string Description, bool Required)
{
    public IReadOnlyList<string>? Values { get; init; }

    public string? DefaultValue { get; init; }

    public bool WholeNumber { get; init; }

    public int Minimum { get; init; }

    public int Maximum { get; init; }

    public int DefaultNumber { get; init; }
}
