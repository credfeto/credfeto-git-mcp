using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Tests.Helpers;
using Credfeto.Git.Mcp.Validation;
using Credfeto.Git.Mcp.Validation.Exceptions;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Validation;

public sealed class GitRefsTests : TestBase
{
    [Fact]
    public async Task AnExistingBranchExistsAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        bool exists = await GitRefs.ExistsAsync(
            git: repository.Context.CreateGit(),
            fullRef: "refs/heads/main",
            cancellationToken: cancellationToken
        );

        Assert.True(condition: exists, userMessage: "main has a commit");
    }

    [Theory]
    [InlineData("refs/heads/missing")]
    [InlineData("refs/remotes/origin/main")]
    public async Task AMissingRefDoesNotExistAsync(string fullRef)
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        bool exists = await GitRefs.ExistsAsync(
            git: repository.Context.CreateGit(),
            fullRef: fullRef,
            cancellationToken: cancellationToken
        );

        Assert.False(condition: exists, userMessage: "no such ref");
    }

    [Theory]
    [InlineData("main")]
    [InlineData("HEAD")]
    [InlineData("--output=/tmp/x")]
    [InlineData("-x")]
    [InlineData("refs/heads/main^{commit}")]
    [InlineData("refs/heads/a..b")]
    public async Task ANameThatIsNotAFullValidRefIsRefusedAsync(string fullRef)
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TestRepository repository = await TestRepository.CreateAsync(cancellationToken);

        _ = await Assert.ThrowsAsync<InvalidRefNameException>(async () =>
            _ = await GitRefs.ExistsAsync(
                git: repository.Context.CreateGit(),
                fullRef: fullRef,
                cancellationToken: cancellationToken
            )
        );
    }
}
