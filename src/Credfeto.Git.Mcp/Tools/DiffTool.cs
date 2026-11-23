using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Diff;
using Credfeto.Git.Mcp.Json;
using Credfeto.Git.Mcp.Models;
using Credfeto.Git.Mcp.Tools.Exceptions;
using Credfeto.Git.Mcp.Validation;

namespace Credfeto.Git.Mcp.Tools;

public sealed class DiffTool : GitTool
{
    public const int MAXIMUM_FILES = 200;
    public const string MAIN_BRANCH = "main";
    public const string ORIGIN = "origin";

    // No colour, and no external diff or textconv programs from the repository's configuration
    private static readonly string[] Common = ["diff", "--no-color", "--no-ext-diff", "--no-textconv"];
    private static readonly string[] Summary = ["--numstat", "-z", "--"];
    private static readonly string[] Patch = ["--patch", "--"];

    private static readonly SchemaProperty Base = new(
        Name: "base",
        Description: "What to compare: main (the commits on the current branch since it left main: the merge base of HEAD and origin/main, or the local main when there is no origin/main, to HEAD), staged (HEAD to the index) or unstaged (the index to the work tree; untracked files are not included)",
        Required: true
    )
    {
        Values = DiffBase.All,
    };

    private readonly ToolContext _context;

    public DiffTool(ToolContext context)
        : base(
            name: ToolNames.DIFF,
            title: "Git diff",
            description: "A summary of a diff (each file with its insertions and deletions, at most 200 files listed, and the totals) plus the path of a file holding the full patch. base picks what is compared; there is no path, ref or option parameter",
            schema: [Base],
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
        string baseName = arguments.GetChoice(Base);
        DiffSelection selection = await this.SelectAsync(baseName: baseName, cancellationToken: cancellationToken);

        string numstat = await this._context.RunCheckedAsync(
            operation: ToolNames.DIFF,
            arguments: [.. Common, .. selection.Arguments, .. Summary],
            timeout: ToolContext.LocalTimeout,
            cancellationToken: cancellationToken
        );

        IReadOnlyList<DiffFile> files = NumstatParser.Parse(numstat);
        string? diffFile =
            files.Count == 0
                ? null
                : await this._context.RunToFileCheckedAsync(
                    operation: ToolNames.DIFF,
                    arguments: [.. Common, .. selection.Arguments, .. Patch],
                    fileName: "diff-" + baseName + ".patch",
                    cancellationToken: cancellationToken
                );

        DiffResponse response = new(
            Base: baseName,
            Compares: selection.Compares,
            FileCount: files.Count,
            Insertions: files.Sum(static file => file.Insertions ?? 0),
            Deletions: files.Sum(static file => file.Deletions ?? 0),
            Files: [.. files.Take(MAXIMUM_FILES)],
            Truncated: files.Count > MAXIMUM_FILES,
            DiffFile: diffFile,
            DiffBytes: diffFile is null ? 0 : new FileInfo(diffFile).Length
        );

        return JsonSerializer.Serialize(value: response, jsonTypeInfo: GitMcpJsonContext.Default.DiffResponse);
    }

    private ValueTask<DiffSelection> SelectAsync(string baseName, CancellationToken cancellationToken)
    {
        return baseName switch
        {
            DiffBase.STAGED => ValueTask.FromResult(
                new DiffSelection(Arguments: ["--cached"], Compares: "HEAD to the index")
            ),
            DiffBase.UNSTAGED => ValueTask.FromResult(
                new DiffSelection(
                    Arguments: [],
                    Compares: "the index to the work tree (untracked files are not included)"
                )
            ),
            _ => this.SelectMainAsync(cancellationToken),
        };
    }

    private async ValueTask<DiffSelection> SelectMainAsync(CancellationToken cancellationToken)
    {
        string main = await this.FindMainAsync(cancellationToken);

        return new(
            Arguments: ["--merge-base", main, "HEAD"],
            Compares: "the merge base of HEAD and " + main + " to HEAD"
        );
    }

    // origin/main when it has been fetched, else the local main
    private async ValueTask<string> FindMainAsync(CancellationToken cancellationToken)
    {
        string[] candidates =
        [
            GitRefName.RemoteBranch(remote: ORIGIN, branch: MAIN_BRANCH),
            GitRefName.Branch(MAIN_BRANCH),
        ];

        foreach (string candidate in candidates)
        {
            if (
                await GitRefs.ExistsAsync(
                    git: this._context.CreateGit(),
                    fullRef: candidate,
                    cancellationToken: cancellationToken
                )
            )
            {
                return candidate;
            }
        }

        throw new ToolFailedException("There is no origin/main or main branch to compare with");
    }
}
