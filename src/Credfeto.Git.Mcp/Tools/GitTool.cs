using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Tools.Exceptions;
using Credfeto.Git.Mcp.Validation.Exceptions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Credfeto.Git.Mcp.Tools;

// A tool with a hand-built schema and no reflection-based binding. Every argument is checked against the schema
// before the tool runs
public abstract class GitTool : McpServerTool
{
    protected GitTool(
        string name,
        string title,
        string description,
        IReadOnlyList<SchemaProperty> schema,
        ToolHints hints
    )
    {
        this.Schema = schema;
        this.ProtocolTool = new()
        {
            Name = name,
            Title = title,
            Description = description,
            InputSchema = ToolSchema.Create(schema),
            Annotations = new()
            {
                Title = title,
                ReadOnlyHint = hints.ReadOnly,
                DestructiveHint = false,
                IdempotentHint = true,
                OpenWorldHint = hints.OpenWorld,
            },
        };
    }

    public IReadOnlyList<SchemaProperty> Schema { get; }

    public override Tool ProtocolTool { get; }

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default
    )
    {
        return this.CallAsync(arguments: request.Params.Arguments, cancellationToken: cancellationToken);
    }

    public async ValueTask<CallToolResult> CallAsync(
        IEnumerable<KeyValuePair<string, JsonElement>>? arguments,
        CancellationToken cancellationToken
    )
    {
        try
        {
            ToolArguments parsed = new(arguments: arguments, schema: this.Schema);
            string json = await this.ExecuteAsync(arguments: parsed, cancellationToken: cancellationToken);

            return Result(text: json, isError: false);
        }
        catch (Exception exception)
            when (exception
                    is ToolArgumentException
                        or ToolFailedException
                        or InvalidRefNameException
                        or InvalidRepositoryPathException
            )
        {
            // These messages are fixed text (at most naming a file the server wrote), never caller input or git output
            return Result(text: exception.Message, isError: true);
        }
    }

    protected abstract ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken);

    private static CallToolResult Result(string text, bool isError)
    {
        return new() { Content = [new TextContentBlock { Text = text }], IsError = isError };
    }
}
