using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Fetch;
using Credfeto.Git.Mcp.Json;
using Credfeto.Git.Mcp.Models;
using Credfeto.Git.Mcp.Tools.Exceptions;

namespace Credfeto.Git.Mcp.Tools;

public sealed class FetchTool : GitTool
{
    public const string REMOTE = "origin";
    public const int MAXIMUM_REFS = 100;

    private static readonly string[] Remotes = ["remote"];

    // Symbolic refs (origin/HEAD) print an empty line, so they are left out: they only follow the branch they name
    private static readonly string[] RemoteRefs =
    [
        "for-each-ref",
        "--format=%(if)%(symref)%(then)%(else)%(objectname) %(refname)%(end)",
        "refs/remotes/" + REMOTE + "/",
    ];

    // --prune only removes origin's remote-tracking refs that are gone from origin; local branches are never touched
    private static readonly string[] Fetch = ["fetch", "--quiet", "--prune", "--no-recurse-submodules", "--", REMOTE];

    private readonly ToolContext _context;

    public FetchTool(ToolContext context)
        : base(
            name: ToolNames.FETCH,
            title: "Git fetch",
            description: "Fetches origin (and prunes its remote-tracking refs that are gone from it), then lists the origin remote-tracking refs that were added, updated or removed, at most 100 of each. Local branches and the work tree are not changed. There is no remote, ref or option parameter",
            schema: [],
            hints: new(ReadOnly: false, OpenWorld: true)
        )
    {
        this._context = context;
    }

    protected override async ValueTask<string> ExecuteAsync(
        ToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        await this.RequireOriginAsync(cancellationToken);

        IReadOnlyDictionary<string, string> before = await this.RemoteRefsAsync(cancellationToken);
        _ = await this._context.RunCheckedAsync(
            operation: ToolNames.FETCH,
            arguments: Fetch,
            timeout: ToolContext.NetworkTimeout,
            cancellationToken: cancellationToken
        );
        IReadOnlyDictionary<string, string> after = await this.RemoteRefsAsync(cancellationToken);

        RemoteRefChanges changes = RemoteRefChanges.Compare(before: before, after: after);
        FetchResponse response = new(
            Remote: REMOTE,
            AddedCount: changes.Added.Count,
            UpdatedCount: changes.Updated.Count,
            RemovedCount: changes.Removed.Count,
            Added: [.. changes.Added.Take(MAXIMUM_REFS)],
            Updated: [.. changes.Updated.Take(MAXIMUM_REFS)],
            Removed: [.. changes.Removed.Take(MAXIMUM_REFS)],
            Truncated: changes.Added.Count > MAXIMUM_REFS
                || changes.Updated.Count > MAXIMUM_REFS
                || changes.Removed.Count > MAXIMUM_REFS
        );

        return JsonSerializer.Serialize(value: response, jsonTypeInfo: GitMcpJsonContext.Default.FetchResponse);
    }

    private async ValueTask RequireOriginAsync(CancellationToken cancellationToken)
    {
        string remotes = await this._context.RunCheckedAsync(
            operation: "remote",
            arguments: Remotes,
            timeout: ToolContext.LocalTimeout,
            cancellationToken: cancellationToken
        );

        if (!remotes.Split('\n').Contains(value: REMOTE, comparer: StringComparer.Ordinal))
        {
            throw new ToolFailedException("This repository has no origin remote");
        }
    }

    private async ValueTask<IReadOnlyDictionary<string, string>> RemoteRefsAsync(CancellationToken cancellationToken)
    {
        string output = await this._context.RunCheckedAsync(
            operation: "for-each-ref",
            arguments: RemoteRefs,
            timeout: ToolContext.LocalTimeout,
            cancellationToken: cancellationToken
        );

        return RemoteRefChanges.ParseRefs(output);
    }
}
