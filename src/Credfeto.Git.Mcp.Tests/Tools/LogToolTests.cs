using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Output;
using Credfeto.Git.Mcp.Tests.Helpers;
using Credfeto.Git.Mcp.Tools;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Tools;

public sealed class LogToolTests : TestBase
{
    [Fact]
    public async Task TheNewestCommitsComeFirstAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);
        await repository.WriteAsync(relative: "a.txt", content: "a\n", cancellationToken: cancellationToken);
        await repository.CommitAllAsync(
            message: "second: with a\u001fseparator-looking subject",
            cancellationToken: cancellationToken
        );
        await repository.WriteAsync(relative: "b.txt", content: "b\n", cancellationToken: cancellationToken);
        await repository.CommitAllAsync(message: "third", cancellationToken: cancellationToken);

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new LogTool(repository.Context),
            arguments: Arguments.Parse("""{"count":2}"""),
            cancellationToken: cancellationToken
        );

        string?[] subjects =
        [
            .. answer
                .RootElement.GetProperty("commits")
                .EnumerateArray()
                .Select(static commit => commit.GetProperty("subject").GetString()),
        ];
        Assert.Equal(expected: ["third", "second: with a\u001fseparator-looking subject"], actual: subjects);
        JsonElement newest = answer.RootElement.GetProperty("commits")[0];
        // Not compared with the test identity: git exports GIT_AUTHOR_NAME to hooks, so under a commit hook it wins
        Assert.False(
            condition: string.IsNullOrEmpty(newest.GetProperty("author").GetString()),
            userMessage: "the author is named"
        );
        Assert.Equal(expected: 40, actual: newest.GetProperty("sha").GetString()?.Length);
    }

    [Fact]
    public async Task TheDefaultCountCoversAShortHistoryAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new LogTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: 1, actual: answer.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ABranchWithNoCommitsHasAnEmptyLogAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateEmptyAsync(cancellationToken);

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new LogTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: 0, actual: answer.RootElement.GetProperty("count").GetInt32());
    }

    [Theory]
    [InlineData("""{"count":0}""")]
    [InlineData("""{"count":101}""")]
    [InlineData("""{"count":-1}""")]
    [InlineData("""{"count":"5"}""")]
    [InlineData("""{"count":"--output=/tmp/x"}""")]
    [InlineData("""{"count":2.5}""")]
    [InlineData("""{"ref":"main"}""")]
    public async Task ABadCountIsRefusedAsync(string json)
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        _ = await ToolCalls.FailAsync(
            tool: new LogTool(repository.Context),
            arguments: Arguments.Parse(json),
            cancellationToken: cancellationToken
        );
    }

    [Fact]
    public async Task OutsideARepositoryGitsErrorGoesToAFileAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TemporaryDirectory directory = new();
        string notARepository = directory.Combine("plain");
        _ = Directory.CreateDirectory(notARepository);
        ToolContext context = new(
            repositoryRoot: notARepository,
            git: TestGit.Executable,
            output: new OutputFolder(directory.Combine("output"))
        );

        string error = await ToolCalls.FailAsync(
            tool: new LogTool(context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        Assert.StartsWith(
            expectedStartString: "git log failed with exit code 128; git's error output is in ",
            actualString: error,
            comparisonType: StringComparison.Ordinal
        );
        string errorFile = error[(error.LastIndexOf(value: " in ", comparisonType: StringComparison.Ordinal) + 4)..];
        string text = await File.ReadAllTextAsync(path: errorFile, cancellationToken: cancellationToken);
        Assert.Contains(
            expectedSubstring: "not a git repository",
            actualString: text,
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task WithoutGitTheToolFailsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TemporaryDirectory directory = new();
        ToolContext context = new(
            repositoryRoot: directory.Path,
            git: null,
            output: new OutputFolder(directory.Combine("output"))
        );

        string error = await ToolCalls.FailAsync(
            tool: new LogTool(context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: "git was not found on PATH", actual: error);
    }
}
