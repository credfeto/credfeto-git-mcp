using System.Collections.Generic;
using Credfeto.Git.Mcp.Diff;
using Credfeto.Git.Mcp.Models;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Diff;

public sealed class NumstatParserTests : TestBase
{
    [Fact]
    public void FilesRenamesAndBinariesAreRead()
    {
        IReadOnlyList<DiffFile> files = NumstatParser.Parse(
            "3\t1\tsrc/a b.cs\0" + "0\t0\t\0old.txt\0new.txt\0" + "-\t-\timage.png\0"
        );

        Assert.Equal(
            expected:
            [
                new DiffFile(Path: "src/a b.cs", OriginalPath: null, Insertions: 3, Deletions: 1, Binary: false),
                new DiffFile(Path: "new.txt", OriginalPath: "old.txt", Insertions: 0, Deletions: 0, Binary: false),
                new DiffFile(Path: "image.png", OriginalPath: null, Insertions: null, Deletions: null, Binary: true),
            ],
            actual: files
        );
    }

    [Fact]
    public void NoOutputIsNoFiles()
    {
        Assert.Empty(NumstatParser.Parse(string.Empty));
    }
}
