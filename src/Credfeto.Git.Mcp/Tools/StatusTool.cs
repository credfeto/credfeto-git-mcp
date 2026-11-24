using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Json;
using Credfeto.Git.Mcp.Models;
using Credfeto.Git.Mcp.Status;

namespace Credfeto.Git.Mcp.Tools;

public sealed class StatusTool : GitTool
{
    public const int MAXIMUM_ENTRIES = 200;
    public const string FULL_LIST_FILE = "status.txt";

    private static readonly string[] Arguments =
    [
        "status",
        "--porcelain=v2",
        "--branch",
        "-z",
        "--untracked-files=normal",
        "--ignored=no",
    ];

    private readonly ToolContext _context;

    public StatusTool(ToolContext context)
        : base(
            name: ToolNames.STATUS,
            title: "Git status",
            description: "The working tree status of the repository: the current branch (null when HEAD is detached), its upstream with how many commits it is ahead and behind, and every changed, renamed, copied, unmerged and untracked entry with its index and work tree state. Lists at most 200 entries; when there are more, the full list is written to a file whose path is returned",
            schema: [],
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
        string output = await this._context.RunCheckedAsync(
            operation: ToolNames.STATUS,
            arguments: Arguments,
            timeout: ToolContext.LocalTimeout,
            cancellationToken: cancellationToken
        );

        StatusReport report = StatusParser.Parse(output);
        bool truncated = report.Entries.Count > MAXIMUM_ENTRIES;
        string? fullListFile = truncated
            ? await this._context.Output.WriteTextAsync(
                fileName: FULL_LIST_FILE,
                content: FullList(report),
                cancellationToken: cancellationToken
            )
            : null;

        StatusResponse response = new(
            Branch: report.Branch,
            Detached: report.Branch is null,
            Head: report.Head,
            Upstream: report.Upstream,
            Ahead: report.Ahead,
            Behind: report.Behind,
            Clean: report.Entries.Count == 0,
            EntryCount: report.Entries.Count,
            Entries: [.. report.Entries.Take(MAXIMUM_ENTRIES)],
            Truncated: truncated,
            FullListFile: fullListFile
        );

        return JsonSerializer.Serialize(value: response, jsonTypeInfo: GitMcpJsonContext.Default.StatusResponse);
    }

    // One "<index><worktree> <path>" line an entry, with " (from <original>)" for a rename or copy
    private static string FullList(StatusReport report)
    {
        StringBuilder text = new();

        foreach (StatusEntry entry in report.Entries)
        {
            _ = text.Append(entry.Index).Append(entry.WorkTree).Append(' ').Append(entry.Path);

            if (entry.OriginalPath is not null)
            {
                _ = text.Append(" (from ").Append(entry.OriginalPath).Append(')');
            }

            _ = text.Append('\n');
        }

        return text.ToString();
    }
}
