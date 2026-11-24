using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Git.Mcp.Server;

public static class GitMcpServer
{
    // Serves MCP over the transport until the client disconnects or the token is cancelled
    public static async ValueTask RunAsync(
        McpServerOptions options,
        ITransport transport,
        CancellationToken cancellationToken
    )
    {
        await using McpServer server = McpServer.Create(
            transport: transport,
            serverOptions: options,
            loggerFactory: NullLoggerFactory.Instance,
            serviceProvider: null
        );

        await server.RunAsync(cancellationToken);
    }
}
