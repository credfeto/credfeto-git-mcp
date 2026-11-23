using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Git;
using Credfeto.Git.Mcp.Json;
using Credfeto.Git.Mcp.Log;
using Credfeto.Git.Mcp.Models;

namespace Credfeto.Git.Mcp.Tools;

public sealed class LogTool : GitTool
{
    private static readonly string[] HeadExists = ["rev-parse", "--verify", "--quiet", "HEAD"];

    private static readonly SchemaProperty Count = new(
        Name: "count",
        Description: "How many of the most recent commits to return",
        Required: false
    )
    {
        WholeNumber = true,
        Minimum = 1,
        Maximum = 100,
        DefaultNumber = 20,
    };

    private readonly ToolContext _context;

    public LogTool(ToolContext context)
        : base(
            name: ToolNames.LOG,
            title: "Git log",
            description: "The most recent commits reachable from HEAD, newest first: full sha, author name, author date (ISO 8601) and subject. A branch with no commits yet gives an empty list",
            schema: [Count],
            hints: ToolHints.LocalReadOnly
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        int count = arguments.GetInteger(Count);
        IReadOnlyList<LogCommit> commits = await this.HasCommitsAsync(cancellationToken)
            ? LogParser.Parse(
                await this._context.RunCheckedAsync(
                    operation: ToolNames.LOG,
                    arguments:
                    [
                        "log",
                        "--max-count=" + count.ToString(CultureInfo.InvariantCulture),
                        "--no-color",
                        "--no-show-signature",
                        "-z",
                        LogParser.FORMAT,
                        "HEAD",
                        "--",
                    ],
                    timeout: ToolContext.LocalTimeout,
                    cancellationToken: cancellationToken
                )
            )
            : [];

        return JsonSerializer.Serialize(
            value: new LogResponse(Count: commits.Count, Commits: commits),
            jsonTypeInfo: GitMcpJsonContext.Default.LogResponse
        );
    }

    private async ValueTask<bool> HasCommitsAsync(CancellationToken cancellationToken)
    {
        GitResult result = await this._context.RunAsync(
            arguments: HeadExists,
            timeout: ToolContext.LocalTimeout,
            cancellationToken: cancellationToken
        );

        // --verify --quiet exits 1 with no output when HEAD has no commit yet; anything else is a real failure
        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw await this._context.FailureAsync(
                operation: ToolNames.LOG,
                result: result,
                cancellationToken: cancellationToken
            ),
        };
    }
}
