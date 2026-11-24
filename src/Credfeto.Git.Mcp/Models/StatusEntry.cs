using System.Diagnostics;

namespace Credfeto.Git.Mcp.Models;

// Index and WorkTree are git's one-letter states ('.' unchanged, M, A, D, R, C, T, U, '?' untracked)
[DebuggerDisplay("{Index}{WorkTree} {Path}")]
public sealed record StatusEntry(string Kind, string Index, string WorkTree, string Path, string? OriginalPath);
