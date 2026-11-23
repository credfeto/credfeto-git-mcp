using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Git.Mcp.Models;

[DebuggerDisplay("Remote={Remote}, Added={AddedCount}, Updated={UpdatedCount}, Removed={RemovedCount}")]
public sealed record FetchResponse(
    string Remote,
    int AddedCount,
    int UpdatedCount,
    int RemovedCount,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Updated,
    IReadOnlyList<string> Removed,
    bool Truncated
);
