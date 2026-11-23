using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Tests.Helpers;
using Credfeto.Git.Mcp.Tools;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Tools;

public sealed class StatusToolTests : TestBase
{
    [Fact]
    public async Task ACleanRepositoryIsCleanAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new StatusTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        JsonElement root = answer.RootElement;
        Assert.Equal(expected: "main", actual: root.GetProperty("branch").GetString());
        Assert.True(condition: root.GetProperty("clean").GetBoolean(), userMessage: "nothing changed");
        Assert.False(condition: root.GetProperty("detached").GetBoolean(), userMessage: "on a branch");
        Assert.Equal(expected: 40, actual: root.GetProperty("head").GetString()?.Length);
    }

    [Fact]
    public async Task ChangesAreListedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);
        await repository.WriteAsync(relative: "README.md", content: "changed\n", cancellationToken: cancellationToken);
        await repository.WriteAsync(relative: "staged.txt", content: "staged\n", cancellationToken: cancellationToken);
        await repository.GitAsync(arguments: ["add", "--", "staged.txt"], cancellationToken: cancellationToken);
        await repository.WriteAsync(relative: "new file.txt", content: "new\n", cancellationToken: cancellationToken);

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new StatusTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        JsonElement root = answer.RootElement;
        Assert.False(condition: root.GetProperty("clean").GetBoolean(), userMessage: "there are changes");
        Assert.Equal(expected: 3, actual: root.GetProperty("entryCount").GetInt32());
        Assert.Equal(expected: ".M", actual: States(root: root, path: "README.md"));
        Assert.Equal(expected: "A.", actual: States(root: root, path: "staged.txt"));
        Assert.Equal(expected: "??", actual: States(root: root, path: "new file.txt"));
    }

    [Fact]
    public async Task ARenameNamesTheOriginalPathAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);
        await repository.GitAsync(
            arguments: ["mv", "--", "README.md", "RENAMED.md"],
            cancellationToken: cancellationToken
        );

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new StatusTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        JsonElement entry = Assert.Single(answer.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal(expected: "renamed", actual: entry.GetProperty("kind").GetString());
        Assert.Equal(expected: "RENAMED.md", actual: entry.GetProperty("path").GetString());
        Assert.Equal(expected: "README.md", actual: entry.GetProperty("originalPath").GetString());
    }

    [Fact]
    public async Task AnEmptyRepositoryHasNoHeadAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateEmptyAsync(cancellationToken);

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new StatusTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: "main", actual: answer.RootElement.GetProperty("branch").GetString());
        Assert.False(
            condition: answer.RootElement.TryGetProperty(propertyName: "head", value: out _),
            userMessage: "no commit yet"
        );
    }

    [Fact]
    public async Task ManyEntriesGoToAFileAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        foreach (int number in Enumerable.Range(start: 0, count: StatusTool.MAXIMUM_ENTRIES + 5))
        {
            await repository.WriteAsync(
                relative: "f" + number.ToString(CultureInfo.InvariantCulture) + ".txt",
                content: "x\n",
                cancellationToken: cancellationToken
            );
        }

        using JsonDocument answer = await ToolCalls.SucceedAsync(
            tool: new StatusTool(repository.Context),
            arguments: Arguments.None(),
            cancellationToken: cancellationToken
        );

        JsonElement root = answer.RootElement;
        Assert.True(condition: root.GetProperty("truncated").GetBoolean(), userMessage: "over the limit");
        Assert.Equal(expected: StatusTool.MAXIMUM_ENTRIES, actual: root.GetProperty("entries").GetArrayLength());
        string file = root.GetProperty("fullListFile").GetString() ?? string.Empty;
        string[] lines = await System.IO.File.ReadAllLinesAsync(path: file, cancellationToken: cancellationToken);
        Assert.Equal(expected: StatusTool.MAXIMUM_ENTRIES + 5, actual: lines.Length);
    }

    [Fact]
    public async Task AnyParameterIsRefusedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        string error = await ToolCalls.FailAsync(
            tool: new StatusTool(repository.Context),
            arguments: Arguments.Parse("""{"path":"../other"}"""),
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: "Unknown parameter; this tool takes: no parameters", actual: error);
    }

    private static string States(JsonElement root, string path)
    {
        JsonElement entry = root.GetProperty("entries")
            .EnumerateArray()
            .Single(item => StringComparer.Ordinal.Equals(x: item.GetProperty("path").GetString(), y: path));

        return entry.GetProperty("index").GetString() + entry.GetProperty("workTree").GetString();
    }
}
