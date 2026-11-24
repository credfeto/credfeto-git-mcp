using System.Diagnostics;

namespace Credfeto.Git.Mcp.Tools;

// The MCP annotations a tool advertises about what it touches
[DebuggerDisplay("ReadOnly={ReadOnly}, OpenWorld={OpenWorld}")]
public readonly record struct ToolHints(bool ReadOnly, bool OpenWorld)
{
    public static ToolHints LocalReadOnly { get; } = new(ReadOnly: true, OpenWorld: false);
}
