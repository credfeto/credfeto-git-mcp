using System;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Git;
using Credfeto.Git.Mcp.Validation.Exceptions;

namespace Credfeto.Git.Mcp.Validation;

public static class GitRefs
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Whether a fully qualified ref (refs/...) exists, exactly as named: no abbreviation or revision syntax
    public static async ValueTask<bool> ExistsAsync(GitRunner git, string fullRef, CancellationToken cancellationToken)
    {
        string name = GitRefName.Validate(fullRef);

        if (!name.StartsWith(value: "refs/", comparisonType: StringComparison.Ordinal))
        {
            throw new InvalidRefNameException();
        }

        GitResult result = await git.RunAsync(
            arguments: ["show-ref", "--verify", "--quiet", name],
            timeout: Timeout,
            cancellationToken: cancellationToken
        );

        return result.Succeeded;
    }
}
