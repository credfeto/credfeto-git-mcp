using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Tools;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Helpers;

internal static class ToolCalls
{
    // The JSON answer of a call that must succeed
    public static async ValueTask<JsonDocument> SucceedAsync(
        GitTool tool,
        Dictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken
    )
    {
        CallToolResult result = await tool.CallAsync(arguments: arguments, cancellationToken: cancellationToken);
        string text = Text(result);
        Assert.False(condition: result.IsError ?? false, userMessage: text);

        return JsonDocument.Parse(text);
    }

    // The error text of a call that must fail
    public static async ValueTask<string> FailAsync(
        GitTool tool,
        Dictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken
    )
    {
        CallToolResult result = await tool.CallAsync(arguments: arguments, cancellationToken: cancellationToken);
        string text = Text(result);
        Assert.True(condition: result.IsError ?? false, userMessage: text);

        return text;
    }

    private static string Text(CallToolResult result)
    {
        return Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    }
}
