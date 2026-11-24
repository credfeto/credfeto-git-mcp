using System.IO;
using Credfeto.Git.Mcp.Tests.Helpers;
using Credfeto.Git.Mcp.Validation;
using Credfeto.Git.Mcp.Validation.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Validation;

public sealed class RepositoryPathTests : TestBase
{
    [Theory]
    [InlineData("-x")]
    [InlineData("-")]
    [InlineData("--output=/tmp/x")]
    [InlineData("--output=x")]
    [InlineData("--all")]
    public static void LeadingDashIsRefused(string path)
    {
        AssertRefused(path);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../x")]
    [InlineData("../../etc/passwd")]
    [InlineData("a/../../x")]
    [InlineData("a/b/../../../x")]
    [InlineData("./..")]
    public static void ParentEscapesAreRefused(string path)
    {
        AssertRefused(path);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("/")]
    [InlineData("/tmp")]
    public static void AbsolutePathsOutsideTheRootAreRefused(string path)
    {
        AssertRefused(path);
    }

    [Fact]
    public void ASiblingFolderSharingTheRootsPrefixIsRefused()
    {
        using TemporaryDirectory directory = new();
        string root = directory.Combine("repo");

        _ = Assert.Throws<InvalidRepositoryPathException>(() =>
            RepositoryPath.Resolve(repositoryRoot: root, path: directory.Combine("repo2/file.txt"))
        );
    }

    [Theory]
    [InlineData(":(top)x")]
    [InlineData(":!x")]
    [InlineData(":/x")]
    [InlineData("*.cs")]
    [InlineData("a?b")]
    [InlineData("a[b]")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a\nb")]
    [InlineData("a\0b")]
    public static void MagicGlobsAndControlCharactersAreRefused(string path)
    {
        AssertRefused(path);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".git/config")]
    [InlineData("a/.git/hooks/pre-commit")]
    [InlineData(".GIT/config")]
    public static void TheGitFolderIsRefused(string path)
    {
        AssertRefused(path);
    }

    [Theory]
    [InlineData("a.txt", "a.txt")]
    [InlineData("dir/a.txt", "dir/a.txt")]
    [InlineData("./dir/a.txt", "dir/a.txt")]
    [InlineData("dir/../a.txt", "a.txt")]
    [InlineData("dir/", "dir")]
    [InlineData(".", ".")]
    [InlineData("..foo", "..foo")]
    [InlineData(".gitignore", ".gitignore")]
    [InlineData("a b.txt", "a b.txt")]
    public static void PathsInsideTheRootAreMadeRelative(string path, string expected)
    {
        using TemporaryDirectory root = new();

        Assert.Equal(expected: expected, actual: RepositoryPath.Resolve(repositoryRoot: root.Path, path: path));
    }

    [Fact]
    public void AnAbsolutePathInsideTheRootIsMadeRelative()
    {
        using TemporaryDirectory root = new();

        Assert.Equal(
            expected: "dir/a.txt",
            actual: RepositoryPath.Resolve(repositoryRoot: root.Path, path: root.Combine("dir/a.txt"))
        );
    }

    [Fact]
    public void APathThroughASymlinkedFolderIsRefused()
    {
        using TemporaryDirectory directory = new();
        string root = directory.Combine("repo");
        string outside = directory.Combine("outside");
        _ = Directory.CreateDirectory(root);
        _ = Directory.CreateDirectory(outside);
        _ = Directory.CreateSymbolicLink(path: Path.Combine(path1: root, path2: "link"), pathToTarget: outside);

        _ = Assert.Throws<InvalidRepositoryPathException>(() =>
            RepositoryPath.Resolve(repositoryRoot: root, path: "link/file.txt")
        );
    }

    [Fact]
    public void ASymlinkItselfIsAccepted()
    {
        using TemporaryDirectory directory = new();
        string root = directory.Combine("repo");
        _ = Directory.CreateDirectory(root);
        _ = File.CreateSymbolicLink(path: Path.Combine(path1: root, path2: "link"), pathToTarget: "/etc/hostname");

        Assert.Equal(expected: "link", actual: RepositoryPath.Resolve(repositoryRoot: root, path: "link"));
    }

    private static void AssertRefused(string path)
    {
        using TemporaryDirectory root = new();

        _ = Assert.Throws<InvalidRepositoryPathException>(() =>
            RepositoryPath.Resolve(repositoryRoot: root.Path, path: path)
        );
    }
}
