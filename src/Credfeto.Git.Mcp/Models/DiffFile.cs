using System.Diagnostics;

namespace Credfeto.Git.Mcp.Models;

// Insertions and Deletions are null for a binary file
[DebuggerDisplay("{Path}: +{Insertions} -{Deletions}")]
public sealed record DiffFile(string Path, string? OriginalPath, int? Insertions, int? Deletions, bool Binary);
