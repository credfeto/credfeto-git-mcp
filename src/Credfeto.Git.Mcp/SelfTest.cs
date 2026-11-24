using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Configuration;
using Credfeto.Git.Mcp.Output;
using Credfeto.Git.Mcp.Server;
using Credfeto.Git.Mcp.Tools;
using ModelContextProtocol.Server;

namespace Credfeto.Git.Mcp;

public static class SelfTest
{
    public const int PASSED = 0;
    public const int FAILED = 1;
    public const string FAILURE_PREFIX = "self-test failed: ";

    // Builds everything the server would serve, without serving it or touching the repository or the output folder
    public static async ValueTask<int> RunAsync(
        GitMcpEnvironment environment,
        string? git,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        if (git is null)
        {
            await error.WriteLineAsync(
                buffer: (FAILURE_PREFIX + "git was not found on PATH").AsMemory(),
                cancellationToken: cancellationToken
            );

            return FAILED;
        }

        McpServerOptions options = Program.CreateOptions(
            environment: environment,
            git: git,
            output: new OutputFolder(environment.OutputDirectory)
        );

        if (options.ToolCollection is not { Count: > 0 })
        {
            await error.WriteLineAsync(
                buffer: (FAILURE_PREFIX + "the server has no tools").AsMemory(),
                cancellationToken: cancellationToken
            );

            return FAILED;
        }

        return PASSED;
    }
}
