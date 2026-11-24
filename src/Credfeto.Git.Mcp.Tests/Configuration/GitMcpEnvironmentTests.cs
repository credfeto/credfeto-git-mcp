using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Configuration;
using Credfeto.Git.Mcp.Configuration.Exceptions;
using Credfeto.Git.Mcp.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Configuration;

public sealed class GitMcpEnvironmentTests : TestBase
{
    [Fact]
    public async Task TheVariableWinsAsync()
    {
        GitMcpEnvironment environment = await ResolveAsync(
            variables: new(StringComparer.Ordinal)
            {
                [GitMcpEnvironment.REPOSITORY_ROOT_VARIABLE] = "/repo/",
                [GitMcpEnvironment.HOME_VARIABLE] = "/home/u",
            },
            defaultRoot: "/missing",
            gitRoot: "/git-root",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "/repo", actual: environment.RepositoryRoot);
        Assert.Equal(expected: RepositoryRootSource.VARIABLE, actual: environment.RepositoryRootSource);
        Assert.Equal(expected: "/home/u/.local/state/credfeto-git-mcp/output", actual: environment.OutputDirectory);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ARootThatIsNotAbsoluteIsAnErrorAsync(string value)
    {
        CancellationToken cancellationToken = this.CancellationToken();

        _ = await Assert.ThrowsAsync<InvalidGitMcpConfigurationException>(async () =>
            _ = await ResolveAsync(
                variables: new(StringComparer.Ordinal) { [GitMcpEnvironment.REPOSITORY_ROOT_VARIABLE] = value },
                defaultRoot: "/missing",
                gitRoot: "/git-root",
                cancellationToken: cancellationToken
            )
        );
    }

    [Fact]
    public async Task TheDefaultFolderIsUsedWhenItExistsAsync()
    {
        using TemporaryDirectory directory = new();

        GitMcpEnvironment environment = await ResolveAsync(
            variables: new(StringComparer.Ordinal) { [GitMcpEnvironment.STATE_HOME_VARIABLE] = "/state" },
            defaultRoot: directory.Path,
            gitRoot: "/git-root",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: directory.Path, actual: environment.RepositoryRoot);
        Assert.Equal(expected: RepositoryRootSource.DEFAULT, actual: environment.RepositoryRootSource);
        Assert.Equal(expected: "/state/credfeto-git-mcp/output", actual: environment.OutputDirectory);
    }

    [Fact]
    public async Task TheGitRootIsTheLastFallbackAsync()
    {
        GitMcpEnvironment environment = await ResolveAsync(
            variables: new(StringComparer.Ordinal) { [GitMcpEnvironment.OUTPUT_DIRECTORY_VARIABLE] = "/out" },
            defaultRoot: "/missing",
            gitRoot: "/git-root",
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "/git-root", actual: environment.RepositoryRoot);
        Assert.Equal(expected: RepositoryRootSource.GIT_ROOT, actual: environment.RepositoryRootSource);
        Assert.Equal(expected: "/out", actual: environment.OutputDirectory);
    }

    [Fact]
    public async Task NoRootAtAllIsAnErrorAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();

        _ = await Assert.ThrowsAsync<InvalidGitMcpConfigurationException>(async () =>
            _ = await ResolveAsync(
                variables: [with(StringComparer.Ordinal)],
                defaultRoot: "/missing",
                gitRoot: null,
                cancellationToken: cancellationToken
            )
        );
    }

    [Fact]
    public async Task WithNoHomeAPrivateFolderIsMadeAsync()
    {
        GitMcpEnvironment environment = await ResolveAsync(
            variables: new(StringComparer.Ordinal) { [GitMcpEnvironment.REPOSITORY_ROOT_VARIABLE] = "/repo" },
            defaultRoot: "/missing",
            gitRoot: null,
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: "/tmp/private", actual: environment.OutputDirectory);
    }

    [Fact]
    public async Task TheGitRootOfACheckoutIsFoundAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);
        await repository.WriteAsync(relative: "sub/file.txt", content: "x\n", cancellationToken: cancellationToken);

        string? root = await ProcessEnvironment.FindGitRootAsync(
            git: TestGit.Executable,
            directory: System.IO.Path.Combine(path1: repository.Root, path2: "sub"),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: repository.Root, actual: root);
    }

    private static ValueTask<GitMcpEnvironment> ResolveAsync(
        Dictionary<string, string> variables,
        string defaultRoot,
        string? gitRoot,
        CancellationToken cancellationToken
    )
    {
        GitMcpEnvironmentSources sources = new(
            GetVariable: name => variables.GetValueOrDefault(name),
            CreatePrivateFolder: static () => "/tmp/private",
            DefaultRepositoryRoot: defaultRoot,
            FindGitRoot: _ => ValueTask.FromResult(gitRoot)
        );

        return GitMcpEnvironment.ResolveAsync(sources: sources, cancellationToken: cancellationToken);
    }
}
