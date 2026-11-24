using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Credfeto.Git.Mcp.Validation.Exceptions;

namespace Credfeto.Git.Mcp.Validation;

// The rules of git check-ref-format --allow-onelevel, plus no leading '-' so a name can never be read as an option.
// Checked in process, so a value is refused before it gets near a git command line
public static class GitRefName
{
    public const string HEADS = "refs/heads/";
    public const string REMOTES = "refs/remotes/";

    private const string LOCK_SUFFIX = ".lock";
    private const string FORBIDDEN = " ~^:?*[\\";

    public static bool IsValid([NotNullWhen(true)] string? name)
    {
        return !string.IsNullOrEmpty(name)
            && name is not ['-', ..]
            && !StringComparer.Ordinal.Equals(x: name, y: "@")
            && name is not ['/', ..]
            && name is not [.., '/']
            && name is not [.., '.']
            && !name.Contains(value: "//", comparisonType: StringComparison.Ordinal)
            && !name.Contains(value: "..", comparisonType: StringComparison.Ordinal)
            && !name.Contains(value: "@{", comparisonType: StringComparison.Ordinal)
            && !name.Any(IsForbiddenCharacter)
            && name.Split('/').All(IsValidComponent);
    }

    public static string Validate(string? name)
    {
        return IsValid(name) ? name : throw new InvalidRefNameException();
    }

    // refs/heads/<branch>, with the branch validated
    public static string Branch(string? branch)
    {
        return HEADS + Validate(branch);
    }

    // refs/remotes/<remote>/<branch>, with both parts validated
    public static string RemoteBranch(string? remote, string? branch)
    {
        string validRemote = Validate(remote);

        return validRemote.Contains('/', StringComparison.Ordinal)
            ? throw new InvalidRefNameException()
            : REMOTES + validRemote + "/" + Validate(branch);
    }

    private static bool IsForbiddenCharacter(char character)
    {
        return character < ' ' || character == '\u007f' || FORBIDDEN.Contains(character, StringComparison.Ordinal);
    }

    private static bool IsValidComponent(string component)
    {
        return component is not ['.', ..]
            && !component.EndsWith(value: LOCK_SUFFIX, comparisonType: StringComparison.Ordinal);
    }
}
