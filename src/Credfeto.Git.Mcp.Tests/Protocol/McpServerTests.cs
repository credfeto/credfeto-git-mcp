using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Configuration;
using Credfeto.Git.Mcp.Server;
using Credfeto.Git.Mcp.Tests.Helpers;
using FunFair.Test.Common;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Protocol;

public sealed class McpServerTests : TestBase
{
    private const string INITIALIZE =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"probe","version":"1.0"}}}""";

    private const string INITIALIZED = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";
    private const string LIST_TOOLS = """{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""";

    [Fact]
    public async Task ToolsAreListedInNameOrderAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TemporaryDirectory directory = new();
        GitMcpEnvironment environment = new(
            RepositoryRoot: directory.Path,
            RepositoryRootSource: RepositoryRootSource.VARIABLE,
            OutputDirectory: directory.Combine("output")
        );
        McpServerOptions options = Program.CreateOptions(
            environment: environment,
            git: TestGit.Executable,
            output: new(environment.OutputDirectory)
        );

        IReadOnlyList<string> names = await ListToolsAsync(options: options, cancellationToken: cancellationToken);

        Assert.Equal(expected: ["diff", "fetch", "log", "status"], actual: names);
        Assert.Equal(expected: [.. names.Order(System.StringComparer.OrdinalIgnoreCase)], actual: names);
    }

    private static async ValueTask<IReadOnlyList<string>> ListToolsAsync(
        McpServerOptions options,
        CancellationToken cancellationToken
    )
    {
        Pipe clientToServer = new();
        Pipe serverToClient = new();

        await using StreamServerTransport transport = new(
            clientToServer.Reader.AsStream(),
            serverToClient.Writer.AsStream(),
            "test",
            NullLoggerFactory.Instance
        );

        Task<IReadOnlyList<string>> client = ClientAsync(
            clientToServer: clientToServer,
            serverToClient: serverToClient,
            cancellationToken: cancellationToken
        );
        await GitMcpServer.RunAsync(options: options, transport: transport, cancellationToken: cancellationToken);

        return await client;
    }

    private static async Task<IReadOnlyList<string>> ClientAsync(
        Pipe clientToServer,
        Pipe serverToClient,
        CancellationToken cancellationToken
    )
    {
        await using Stream toServer = clientToServer.Writer.AsStream();
        using StreamReader fromServer = new(serverToClient.Reader.AsStream());

        try
        {
            await SendAsync(toServer: toServer, message: INITIALIZE, cancellationToken: cancellationToken);
            using JsonDocument initialized = await ReceiveAsync(
                fromServer: fromServer,
                id: 1,
                cancellationToken: cancellationToken
            );
            await SendAsync(toServer: toServer, message: INITIALIZED, cancellationToken: cancellationToken);
            await SendAsync(toServer: toServer, message: LIST_TOOLS, cancellationToken: cancellationToken);
            using JsonDocument listed = await ReceiveAsync(
                fromServer: fromServer,
                id: 2,
                cancellationToken: cancellationToken
            );

            return
            [
                .. listed
                    .RootElement.GetProperty("result")
                    .GetProperty("tools")
                    .EnumerateArray()
                    .Select(static tool => tool.GetProperty("name").GetString() ?? string.Empty),
            ];
        }
        finally
        {
            // End of input ends the session, as when a client closes the server's stdin
            await clientToServer.Writer.CompleteAsync();
        }
    }

    private static async ValueTask SendAsync(Stream toServer, string message, CancellationToken cancellationToken)
    {
        await toServer.WriteAsync(buffer: Encoding.UTF8.GetBytes(message + "\n"), cancellationToken: cancellationToken);
        await toServer.FlushAsync(cancellationToken);
    }

    private static async ValueTask<JsonDocument> ReceiveAsync(
        StreamReader fromServer,
        int id,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            string line = await fromServer.ReadLineAsync(cancellationToken)
                ?? throw new EndOfStreamException("The server closed its output");
            JsonDocument document = JsonDocument.Parse(line);

            if (
                document.RootElement.TryGetProperty(propertyName: "id", value: out JsonElement responseId)
                && responseId.ValueKind == JsonValueKind.Number
                && responseId.GetInt32() == id
            )
            {
                return document;
            }

            document.Dispose();
        }
    }
}
