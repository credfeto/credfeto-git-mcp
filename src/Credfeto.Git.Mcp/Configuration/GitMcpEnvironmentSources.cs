using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Credfeto.Git.Mcp.Configuration;

// Where the environment is read from, so tests can resolve it without touching the process environment
[DebuggerDisplay("DefaultRepositoryRoot={DefaultRepositoryRoot}")]
public sealed record GitMcpEnvironmentSources(
    Func<string, string?> GetVariable,
    Func<string> CreatePrivateFolder,
    string DefaultRepositoryRoot,
    Func<CancellationToken, ValueTask<string?>> FindGitRoot
);
