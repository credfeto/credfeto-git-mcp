using System.Linq;
using System.Text.Json;
using Credfeto.Git.Mcp.Output;
using Credfeto.Git.Mcp.Tests.Helpers;
using Credfeto.Git.Mcp.Tools;
using FunFair.Test.Common;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Tools;

public sealed class ToolCatalogTests : TestBase
{
    [Fact]
    public void ToolsAddedOutOfOrderAreSortedByName()
    {
        McpServerTool[] tools =
        [
            new NamedTool("status"),
            new NamedTool("Beta"),
            new NamedTool("alpha"),
            new NamedTool("diff"),
        ];

        string[] names = [.. ToolCatalog.Sort(tools).Select(static tool => tool.ProtocolTool.Name)];

        Assert.Equal(expected: ["alpha", "Beta", "diff", "status"], actual: names);
    }

    [Fact]
    public void EverySchemaRefusesUndeclaredProperties()
    {
        using TemporaryDirectory directory = new();
        ToolContext context = new(
            repositoryRoot: directory.Path,
            git: null,
            output: new OutputFolder(directory.Combine("output"))
        );

        foreach (McpServerTool tool in ToolCatalog.Create(context))
        {
            JsonElement schema = tool.ProtocolTool.InputSchema;
            Assert.False(
                condition: schema.GetProperty("additionalProperties").GetBoolean(),
                userMessage: tool.ProtocolTool.Name + " allows undeclared properties"
            );
        }
    }

    [Fact]
    public void TheDiffBaseIsAnEnum()
    {
        using TemporaryDirectory directory = new();
        ToolContext context = new(
            repositoryRoot: directory.Path,
            git: null,
            output: new OutputFolder(directory.Combine("output"))
        );

        JsonElement schema = new DiffTool(context).ProtocolTool.InputSchema;

        string?[] values =
        [
            .. schema
                .GetProperty("properties")
                .GetProperty("base")
                .GetProperty("enum")
                .EnumerateArray()
                .Select(static value => value.GetString()),
        ];
        Assert.Equal(expected: ["main", "staged", "unstaged"], actual: values);
        Assert.Equal(
            expected: "base",
            actual: Assert.Single(schema.GetProperty("required").EnumerateArray()).GetString()
        );
    }

    [Fact]
    public void TheLogCountIsBounded()
    {
        using TemporaryDirectory directory = new();
        ToolContext context = new(
            repositoryRoot: directory.Path,
            git: null,
            output: new OutputFolder(directory.Combine("output"))
        );

        JsonElement count = new LogTool(context)
            .ProtocolTool.InputSchema.GetProperty("properties")
            .GetProperty("count");

        Assert.Equal(expected: "integer", actual: count.GetProperty("type").GetString());
        Assert.Equal(expected: 1, actual: count.GetProperty("minimum").GetInt32());
        Assert.Equal(expected: 100, actual: count.GetProperty("maximum").GetInt32());
    }
}
