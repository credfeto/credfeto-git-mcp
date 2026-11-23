using System.Diagnostics;

namespace Credfeto.Git.Mcp.Git;

[DebuggerDisplay("ExitCode={ExitCode}")]
public readonly record struct GitResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => this.ExitCode == 0;
}
