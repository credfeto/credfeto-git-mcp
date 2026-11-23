using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Tests.Helpers;
using Credfeto.Git.Mcp.Tools;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Tools;

public sealed class DiffToolTests : TestBase
{
    [Fact]
    public async Task StagedChangesAreSummarisedWithTheFullPatchInAFileAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);
        await repository.WriteAsync(
            relative: "README.md",
            content: "readme\nmore\n",
            cancellationToken: cancellationToken
        );
        await repository.GitAsync(arguments: ["add", "--", "README.md"], cancellationToken: cancellationToken);

        using JsonDocument answer = await DiffAsync(
            repository: repository,
            baseName: "staged",
            cancellationToken: cancellationToken
        );

        JsonElement root = answer.RootElement;
        Assert.Equal(expected: 1, actual: root.GetProperty("fileCount").GetInt32());
        Assert.Equal(expected: 1, actual: root.GetProperty("insertions").GetInt32());
        Assert.Equal(expected: 0, actual: root.GetProperty("deletions").GetInt32());
        string patch = await File.ReadAllTextAsync(path: DiffFile(root), cancellationToken: cancellationToken);
        Assert.Contains(expectedSubstring: "+more", actualString: patch, comparisonType: StringComparison.Ordinal);
        Assert.StartsWith(
            expectedStartString: repository.OutputDirectory,
            actualString: DiffFile(root),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task UnstagedChangesLeaveOutStagedOnesAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);
        await repository.WriteAsync(relative: "staged.txt", content: "staged\n", cancellationToken: cancellationToken);
        await repository.GitAsync(arguments: ["add", "--", "staged.txt"], cancellationToken: cancellationToken);
        await repository.WriteAsync(relative: "README.md", content: "changed\n", cancellationToken: cancellationToken);

        using JsonDocument answer = await DiffAsync(
            repository: repository,
            baseName: "unstaged",
            cancellationToken: cancellationToken
        );

        JsonElement file = Assert.Single(answer.RootElement.GetProperty("files").EnumerateArray());
        Assert.Equal(expected: "README.md", actual: file.GetProperty("path").GetString());
        Assert.Equal(expected: 1, actual: file.GetProperty("deletions").GetInt32());
    }

    [Fact]
    public async Task NoChangesGiveNoDiffFileAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        using JsonDocument answer = await DiffAsync(
            repository: repository,
            baseName: "unstaged",
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: 0, actual: answer.RootElement.GetProperty("fileCount").GetInt32());
        Assert.False(
            condition: answer.RootElement.TryGetProperty(propertyName: "diffFile", value: out _),
            userMessage: "nothing to write"
        );
    }

    [Fact]
    public async Task MainComparesTheBranchWithOriginMainAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateWithOriginAsync(cancellationToken);
        await repository.GitAsync(
            arguments: ["checkout", "--quiet", "-b", "feature"],
            cancellationToken: cancellationToken
        );
        await repository.WriteAsync(
            relative: "feature.txt",
            content: "one\ntwo\n",
            cancellationToken: cancellationToken
        );
        await repository.CommitAllAsync(message: "feature", cancellationToken: cancellationToken);
        await repository.WriteAsync(relative: "uncommitted.txt", content: "x\n", cancellationToken: cancellationToken);

        using JsonDocument answer = await DiffAsync(
            repository: repository,
            baseName: "main",
            cancellationToken: cancellationToken
        );

        JsonElement root = answer.RootElement;
        JsonElement file = Assert.Single(root.GetProperty("files").EnumerateArray());
        Assert.Equal(expected: "feature.txt", actual: file.GetProperty("path").GetString());
        Assert.Equal(expected: 2, actual: root.GetProperty("insertions").GetInt32());
        Assert.Contains(
            expectedSubstring: "refs/remotes/origin/main",
            actualString: root.GetProperty("compares").GetString(),
            comparisonType: StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task MainFallsBackToTheLocalMainAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);
        await repository.GitAsync(
            arguments: ["checkout", "--quiet", "-b", "feature"],
            cancellationToken: cancellationToken
        );
        await repository.GitAsync(
            arguments: ["mv", "--", "README.md", "MOVED.md"],
            cancellationToken: cancellationToken
        );
        await repository.CommitAllAsync(message: "move", cancellationToken: cancellationToken);

        using JsonDocument answer = await DiffAsync(
            repository: repository,
            baseName: "main",
            cancellationToken: cancellationToken
        );

        JsonElement file = Assert.Single(answer.RootElement.GetProperty("files").EnumerateArray());
        Assert.Equal(expected: "MOVED.md", actual: file.GetProperty("path").GetString());
        Assert.Equal(expected: "README.md", actual: file.GetProperty("originalPath").GetString());
    }

    [Fact]
    public async Task MainWithoutAMainBranchFailsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);
        await repository.GitAsync(
            arguments: ["branch", "--quiet", "-m", "trunk"],
            cancellationToken: cancellationToken
        );

        string error = await ToolCalls.FailAsync(
            tool: new DiffTool(repository.Context),
            arguments: Arguments.Parse("""{"base":"main"}"""),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: "There is no origin/main or main branch to compare with", actual: error);
    }

    [Theory]
    [InlineData("""{"base":"--output=/tmp/credfeto-git-mcp-injected"}""")]
    [InlineData("""{"base":"HEAD~1"}""")]
    [InlineData("""{"base":"-p"}""")]
    [InlineData("""{"base":1}""")]
    [InlineData("""{}""")]
    [InlineData("""{"base":"staged","path":"../x"}""")]
    [InlineData("""{"base":"staged","extraArgs":"--output=/tmp/x"}""")]
    public async Task AnythingButAKnownBaseIsRefusedAsync(string json)
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        _ = await ToolCalls.FailAsync(
            tool: new DiffTool(repository.Context),
            arguments: Arguments.Parse(json),
            cancellationToken: cancellationToken
        );

        Assert.False(
            condition: File.Exists("/tmp/credfeto-git-mcp-injected"),
            userMessage: "the value never reached git"
        );
    }

    private static ValueTask<JsonDocument> DiffAsync(
        TestRepository repository,
        string baseName,
        CancellationToken cancellationToken
    )
    {
        return ToolCalls.SucceedAsync(
            tool: new DiffTool(repository.Context),
            arguments: Arguments.Parse("{\"base\":\"" + baseName + "\"}"),
            cancellationToken: cancellationToken
        );
    }

    private static string DiffFile(JsonElement root)
    {
        return root.GetProperty("diffFile").GetString() ?? string.Empty;
    }
}
