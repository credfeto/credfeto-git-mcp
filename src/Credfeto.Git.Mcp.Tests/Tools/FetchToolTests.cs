using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Tests.Helpers;
using Credfeto.Git.Mcp.Tools;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Tools;

public sealed class FetchToolTests : TestBase
{
    [Fact]
    public async Task ChangedRemoteTrackingRefsAreReportedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateWithOriginAsync(cancellationToken);
        string other = await repository.CloneOriginAsync(cancellationToken);
        await ChangeOriginAsync(other: other, cancellationToken: cancellationToken);

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new FetchTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        JsonElement root = answer.RootElement;
        Assert.Equal(expected: ["refs/remotes/origin/new-branch"], actual: Names(root, "added"));
        Assert.Equal(expected: ["refs/remotes/origin/main"], actual: Names(root, "updated"));
        Assert.Empty(Names(root, "removed"));
    }

    [Fact]
    public async Task NothingNewReportsNothingAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateWithOriginAsync(cancellationToken);

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new FetchTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: 0, actual: answer.RootElement.GetProperty("addedCount").GetInt32());
        Assert.Equal(expected: 0, actual: answer.RootElement.GetProperty("updatedCount").GetInt32());
    }

    [Fact]
    public async Task WithoutOriginTheFetchIsRefusedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        string error = await ToolCalls.FailAsync(
            tool: new FetchTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: "This repository has no origin remote", actual: error);
    }

    [Theory]
    [InlineData("""{"remote":"--upload-pack=touch /tmp/x"}""")]
    [InlineData("""{"refspec":"main"}""")]
    public async Task ParametersAreRefusedAsync(string json)
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateWithOriginAsync(cancellationToken);

        string error = await ToolCalls.FailAsync(
            tool: new FetchTool(repository.Context),
            arguments: Arguments.Parse(json),
            cancellationToken: cancellationToken
        );

        Assert.StartsWith(
            expectedStartString: "Unknown parameter",
            actualString: error,
            comparisonType: StringComparison.Ordinal
        );
    }

    private static async ValueTask ChangeOriginAsync(string other, CancellationToken cancellationToken)
    {
        await System.IO.File.WriteAllTextAsync(
            path: System.IO.Path.Combine(path1: other, path2: "more.txt"),
            contents: "more\n",
            cancellationToken: cancellationToken
        );
        await TestGit.CommitAllAsync(repository: other, message: "more", cancellationToken: cancellationToken);
        _ = await TestGit.RunAsync(
            repository: other,
            arguments: ["push", "--quiet", "origin", "main", "main:new-branch"],
            cancellationToken: cancellationToken
        );
    }

    private static string[] Names(JsonElement root, string property)
    {
        return [.. root.GetProperty(property).EnumerateArray().Select(static item => item.GetString() ?? string.Empty)];
    }
}
