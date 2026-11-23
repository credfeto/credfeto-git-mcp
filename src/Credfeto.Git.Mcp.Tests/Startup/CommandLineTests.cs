using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Configuration;
using Credfeto.Git.Mcp.Configuration.Exceptions;
using Credfeto.Git.Mcp.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Startup;

public sealed class CommandLineTests : TestBase
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task HelpShowsTheUsageAsync(string option)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CommandLine.RunAsync(
            arguments: [option],
            context: Context(output: output, error: error, git: TestGit.Executable),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: CommandLine.SUCCESS, actual: exitCode);
        Assert.Contains(
            expectedSubstring: "Usage: credfeto-git-mcp",
            actualString: output.ToString(),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Contains(
            expectedSubstring: GitMcpEnvironment.REPOSITORY_ROOT_VARIABLE,
            actualString: output.ToString(),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("--repo=/etc")]
    [InlineData("serve")]
    public async Task UnknownArgumentsAreAUsageErrorAsync(string argument)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CommandLine.RunAsync(
            arguments: [argument],
            context: Context(output: output, error: error, git: TestGit.Executable),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: CommandLine.USAGE_ERROR, actual: exitCode);
        Assert.Empty(output.ToString());
    }

    [Fact]
    public async Task SelfTestPassesWithGitAsync()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CommandLine.RunAsync(
            arguments: [CommandLine.SELF_TEST],
            context: Context(output: output, error: error, git: TestGit.Executable),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTest.PASSED, actual: exitCode);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task SelfTestFailsWithoutGitAsync()
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CommandLine.RunAsync(
            arguments: [CommandLine.SELF_TEST],
            context: Context(output: output, error: error, git: null),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: SelfTest.FAILED, actual: exitCode);
        Assert.StartsWith(
            expectedStartString: SelfTest.FAILURE_PREFIX,
            actualString: error.ToString(),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task AConfigurationErrorExitsWithOneAsync()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        CommandLineContext context = Context(output: output, error: error, git: TestGit.Executable) with
        {
            ResolveEnvironment = static _ => throw new InvalidGitMcpConfigurationException("bad configuration"),
        };

        int exitCode = await CommandLine.RunAsync(
            arguments: [],
            context: context,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: CommandLine.CONFIGURATION_ERROR, actual: exitCode);
        Assert.Contains(
            expectedSubstring: "bad configuration",
            actualString: error.ToString(),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task ServingNamesTheRepositoryRootOnStandardErrorAsync()
    {
        using StringWriter output = new();
        using StringWriter error = new();
        CommandLineContext context = Context(output: output, error: error, git: TestGit.Executable) with
        {
            Serve = static (_, _) => ValueTask.FromResult(42),
        };

        int exitCode = await CommandLine.RunAsync(
            arguments: [],
            context: context,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: 42, actual: exitCode);
        Assert.Contains(
            expectedSubstring: "credfeto-git-mcp: repository root /repo (from GIT_MCP_REPO_ROOT)",
            actualString: error.ToString(),
            comparisonType: StringComparison.Ordinal
        );
        Assert.Empty(output.ToString());
    }

    private static CommandLineContext Context(TextWriter output, TextWriter error, string? git)
    {
        return new(
            Output: output,
            Error: error,
            ResolveEnvironment: static _ =>
                ValueTask.FromResult(
                    new GitMcpEnvironment(
                        RepositoryRoot: "/repo",
                        RepositoryRootSource: RepositoryRootSource.VARIABLE,
                        OutputDirectory: "/output"
                    )
                ),
            FindGit: () => git,
            Serve: static (_, _) => throw new InvalidOperationException("not expected to serve")
        );
    }
}
