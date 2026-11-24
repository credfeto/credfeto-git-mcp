using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Configuration;

namespace Credfeto.Git.Mcp;

// What the command line reads and starts, so tests can run it without the process environment or a real server
[DebuggerDisplay("CommandLineContext")]
public sealed record CommandLineContext(
    TextWriter Output,
    TextWriter Error,
    Func<CancellationToken, ValueTask<GitMcpEnvironment>> ResolveEnvironment,
    Func<string?> FindGit,
    Func<GitMcpEnvironment, CancellationToken, ValueTask<int>> Serve
);
