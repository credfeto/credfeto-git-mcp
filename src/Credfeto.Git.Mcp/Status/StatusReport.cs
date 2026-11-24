using System.Collections.Generic;
using System.Diagnostics;
using Credfeto.Git.Mcp.Models;

namespace Credfeto.Git.Mcp.Status;

[DebuggerDisplay("Branch={Branch}, Entries={Entries.Count}")]
public sealed record StatusReport(
    string? Branch,
    string? Head,
    string? Upstream,
    int? Ahead,
    int? Behind,
    IReadOnlyList<StatusEntry> Entries
);
