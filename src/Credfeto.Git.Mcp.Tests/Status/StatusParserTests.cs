using Credfeto.Git.Mcp.Models;
using Credfeto.Git.Mcp.Status;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Status;

public sealed class StatusParserTests : TestBase
{
    private const string SHA = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void BranchHeadersAreRead()
    {
        StatusReport report = StatusParser.Parse(
            "# branch.oid " + SHA + "\0# branch.head feat/x\0# branch.upstream origin/feat/x\0# branch.ab +2 -3\0"
        );

        Assert.Equal(expected: SHA, actual: report.Head);
        Assert.Equal(expected: "feat/x", actual: report.Branch);
        Assert.Equal(expected: "origin/feat/x", actual: report.Upstream);
        Assert.Equal(expected: 2, actual: report.Ahead);
        Assert.Equal(expected: 3, actual: report.Behind);
        Assert.Empty(report.Entries);
    }

    [Fact]
    public void DetachedAndInitialAreNull()
    {
        StatusReport report = StatusParser.Parse("# branch.oid (initial)\0# branch.head (detached)\0");

        Assert.Null(report.Head);
        Assert.Null(report.Branch);
    }

    [Fact]
    public void APathWithSpacesIsKeptWhole()
    {
        StatusReport report = StatusParser.Parse(
            "1 .M N... 100644 100644 100644 " + SHA + " " + SHA + " dir/a file.txt\0"
        );

        StatusEntry entry = Assert.Single(report.Entries);
        Assert.Equal(expected: "dir/a file.txt", actual: entry.Path);
        Assert.Equal(expected: ".", actual: entry.Index);
        Assert.Equal(expected: "M", actual: entry.WorkTree);
    }

    [Fact]
    public void RenamesCopiesUnmergedAndUntrackedAreRead()
    {
        StatusReport report = StatusParser.Parse(MixedStatus());

        Assert.Equal(expected: MixedEntries, actual: report.Entries);
    }

    private static readonly StatusEntry[] MixedEntries =
    [
        new(Kind: StatusParser.RENAMED, Index: "R", WorkTree: ".", Path: "new.txt", OriginalPath: "old.txt"),
        new(Kind: StatusParser.COPIED, Index: "C", WorkTree: ".", Path: "copy.txt", OriginalPath: "source.txt"),
        new(Kind: StatusParser.UNMERGED, Index: "U", WorkTree: "U", Path: "both.txt", OriginalPath: null),
        new(Kind: StatusParser.UNTRACKED, Index: "?", WorkTree: "?", Path: "new file.txt", OriginalPath: null),
    ];

    private static string MixedStatus()
    {
        const string modes = "N... 100644 100644 100644 ";
        string hashes = SHA + " " + SHA + " ";

        return "2 R. "
            + modes
            + hashes
            + "R100 new.txt\0old.txt\0"
            + "2 C. "
            + modes
            + hashes
            + "C75 copy.txt\0source.txt\0"
            + "u UU "
            + modes
            + "100644 "
            + hashes
            + SHA
            + " both.txt\0"
            + "? new file.txt\0"
            + "! ignored.txt\0";
    }
}
