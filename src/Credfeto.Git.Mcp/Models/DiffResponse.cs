using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Git.Mcp.Models;

[DebuggerDisplay("Base={Base}, Files={FileCount}, DiffFile={DiffFile}")]
public sealed record DiffResponse(
    string Base,
    string Compares,
    int FileCount,
    int Insertions,
    int Deletions,
    IReadOnlyList<DiffFile> Files,
    bool Truncated,
    string? DiffFile,
    long DiffBytes
);
