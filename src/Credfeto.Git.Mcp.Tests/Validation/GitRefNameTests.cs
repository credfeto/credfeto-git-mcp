using System;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Git;
using Credfeto.Git.Mcp.Tests.Helpers;
using Credfeto.Git.Mcp.Validation;
using Credfeto.Git.Mcp.Validation.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Validation;

public sealed class GitRefNameTests : TestBase
{
    [Theory]
    [InlineData("-x")]
    [InlineData("-")]
    [InlineData("--output=/tmp/x")]
    [InlineData("--output=x")]
    [InlineData("--upload-pack=touch /tmp/x")]
    [InlineData("-c core.pager=x")]
    public static void LeadingDashIsRefused(string name)
    {
        Assert.False(condition: GitRefName.IsValid(name), userMessage: "a leading '-' could be read as an option");
        _ = Assert.Throws<InvalidRefNameException>(() => GitRefName.Validate(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("@")]
    [InlineData("a..b")]
    [InlineData("..")]
    [InlineData(".hidden")]
    [InlineData("a/.hidden")]
    [InlineData("a.lock")]
    [InlineData("a/b.lock/c")]
    [InlineData("a b")]
    [InlineData("a~1")]
    [InlineData("a^")]
    [InlineData("a:b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a[b")]
    [InlineData("a\\b")]
    [InlineData("/a")]
    [InlineData("a/")]
    [InlineData("a//b")]
    [InlineData("a.")]
    [InlineData("a@{1}")]
    [InlineData("a\tb")]
    [InlineData("a\nb")]
    [InlineData("a\u007fb")]
    public static void CheckRefFormatRulesAreEnforced(string name)
    {
        Assert.False(condition: GitRefName.IsValid(name), userMessage: "not a valid ref name");
    }

    [Fact]
    public void NullIsRefused()
    {
        Assert.False(condition: GitRefName.IsValid(null), userMessage: "null is not a ref name");
    }

    [Theory]
    [InlineData("main")]
    [InlineData("feat/6-read-only-git-tools")]
    [InlineData("release/1.2.3")]
    [InlineData("refs/heads/main")]
    [InlineData("a@b")]
    [InlineData("a-")]
    [InlineData("v1.0")]
    [InlineData("a.b/c_d")]
    public static void ValidNamesAreAccepted(string name)
    {
        Assert.Equal(expected: name, actual: GitRefName.Validate(name));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("feat/x-1")]
    [InlineData("a..b")]
    [InlineData("a.lock")]
    [InlineData("@")]
    [InlineData("a@{b")]
    [InlineData("a b")]
    [InlineData("a//b")]
    [InlineData("a/")]
    [InlineData(".a")]
    [InlineData("a.")]
    [InlineData("a~b")]
    [InlineData("a[b")]
    [InlineData("a@b")]
    [InlineData("x/.y/z")]
    public async Task AgreesWithGitCheckRefFormatAsync(string name)
    {
        GitResult result = await new GitRunner(
            git: TestGit.Executable,
            repositoryRoot: System.IO.Path.GetTempPath()
        ).RunAsync(
            arguments: ["check-ref-format", "--allow-onelevel", name],
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: this.CancellationToken()
        );

        Assert.Equal(expected: result.Succeeded, actual: GitRefName.IsValid(name));
    }

    [Fact]
    public void BranchIsQualified()
    {
        Assert.Equal(expected: "refs/heads/feat/x", actual: GitRefName.Branch("feat/x"));
    }

    [Fact]
    public void RemoteBranchIsQualified()
    {
        Assert.Equal(
            expected: "refs/remotes/origin/main",
            actual: GitRefName.RemoteBranch(remote: "origin", branch: "main")
        );
    }

    [Theory]
    [InlineData("--output=x", "main")]
    [InlineData("origin", "--output=x")]
    [InlineData("a/b", "main")]
    [InlineData("origin", "a..b")]
    public static void BadPartsAreRefused(string remote, string branch)
    {
        _ = Assert.Throws<InvalidRefNameException>(() => GitRefName.RemoteBranch(remote: remote, branch: branch));
    }
}
