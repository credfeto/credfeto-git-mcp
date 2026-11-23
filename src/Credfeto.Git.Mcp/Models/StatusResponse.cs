using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Git.Mcp.Models;

[DebuggerDisplay("Branch={Branch}, Entries={EntryCount}")]
public sealed record StatusResponse(
    string? Branch,
    bool Detached,
    string? Head,
    string? Upstream,
    int? Ahead,
    int? Behind,
    bool Clean,
    int EntryCount,
    IReadOnlyList<StatusEntry> Entries,
    bool Truncated,
    string? FullListFile
);
