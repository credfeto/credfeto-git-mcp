using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Git.Mcp.Models;

[DebuggerDisplay("Count={Count}")]
public sealed record LogResponse(int Count, IReadOnlyList<LogCommit> Commits);
