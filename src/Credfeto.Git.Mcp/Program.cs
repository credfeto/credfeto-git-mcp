using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Configuration;
using Credfeto.Git.Mcp.Git;
using Credfeto.Git.Mcp.Output;
using Credfeto.Git.Mcp.Server;
using Credfeto.Git.Mcp.Tools;
using ModelContextProtocol.Server;

namespace Credfeto.Git.Mcp;

public static class Program
{
    private static readonly TimeSpan StaleSessionAge = TimeSpan.FromDays(2);

    public static Task<int> Main(string[] args)
    {
        return CommandLine
            .RunAsync(
                arguments: args,
                context: new(
                    Output: Console.Out,
                    Error: Console.Error,
                    ResolveEnvironment: ProcessEnvironment.ResolveAsync,
                    FindGit: GitExecutable.FromProcess,
                    Serve: ServeAsync
                ),
                cancellationToken: CancellationToken.None
            )
            .AsTask();
    }

    public static McpServerOptions CreateOptions(GitMcpEnvironment environment, string? git, OutputFolder output)
    {
        ToolContext context = new(repositoryRoot: environment.RepositoryRoot, git: git, output: output);

        return ServerOptionsFactory.Create(
            repositoryRoot: environment.RepositoryRoot,
            tools: ToolCatalog.Create(context)
        );
    }

    private static ValueTask<int> ServeAsync(GitMcpEnvironment environment, CancellationToken cancellationToken)
    {
        OutputFolder output = new(environment.OutputDirectory);
        _ = output.RemoveStaleSessions(now: TimeProvider.System.GetUtcNow(), age: StaleSessionAge);

        return StdioServerHost.ServeAsync(
            options: CreateOptions(environment: environment, git: GitExecutable.FromProcess(), output: output),
            cancellationToken: cancellationToken
        );
    }
}
