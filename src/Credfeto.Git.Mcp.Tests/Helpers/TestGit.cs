using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Git;

namespace Credfeto.Git.Mcp.Tests.Helpers;

// Drives git in a temporary repository, isolated from the user's hooks, signing and identity
public static class TestGit
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private static readonly string[] Isolation =
    [
        "-c",
        "user.name=Test",
        "-c",
        "user.email=test@example.com",
        "-c",
        "commit.gpgsign=false",
        "-c",
        "tag.gpgsign=false",
        "-c",
        "core.hooksPath=/dev/null",
        "-c",
        "init.defaultBranch=main",
    ];

    public static string Executable { get; } =
        GitExecutable.FromProcess() ?? throw new InvalidOperationException("The tests need git on PATH");

    public static async ValueTask<string> RunAsync(
        string repository,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        GitResult result = await new GitRunner(git: Executable, repositoryRoot: repository).RunAsync(
            arguments: [.. Isolation, .. arguments],
            timeout: Timeout,
            cancellationToken: cancellationToken
        );

        return result.Succeeded
            ? result.Output
            : throw new InvalidOperationException(
                "git " + string.Join(separator: ' ', values: arguments) + " failed: " + result.Error
            );
    }

    public static ValueTask InitAsync(string repository, CancellationToken cancellationToken)
    {
        return IgnoreAsync(
            RunAsync(repository: repository, arguments: ["init", "--quiet"], cancellationToken: cancellationToken)
        );
    }

    public static async ValueTask CommitAllAsync(string repository, string message, CancellationToken cancellationToken)
    {
        await IgnoreAsync(
            RunAsync(repository: repository, arguments: ["add", "--all"], cancellationToken: cancellationToken)
        );
        await IgnoreAsync(
            RunAsync(
                repository: repository,
                arguments: ["commit", "--quiet", "--no-verify", "-m", message],
                cancellationToken: cancellationToken
            )
        );
    }

    private static async ValueTask IgnoreAsync(ValueTask<string> run)
    {
        _ = await run;
    }
}
