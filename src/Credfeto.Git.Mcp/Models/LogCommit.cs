using System.Diagnostics;

namespace Credfeto.Git.Mcp.Models;

// Date is the author date in strict ISO 8601
[DebuggerDisplay("{Sha} {Subject}")]
public sealed record LogCommit(string Sha, string Author, string Date, string Subject);
