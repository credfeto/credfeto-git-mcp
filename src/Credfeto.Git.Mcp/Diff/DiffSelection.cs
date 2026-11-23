using System.Collections.Generic;
using System.Diagnostics;

namespace Credfeto.Git.Mcp.Diff;

// The git diff arguments that pick what is compared (no output options), and a description of it for the answer
[DebuggerDisplay("{Compares}")]
public sealed record DiffSelection(IReadOnlyList<string> Arguments, string Compares);
