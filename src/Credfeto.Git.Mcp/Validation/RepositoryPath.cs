using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Credfeto.Git.Mcp.Validation.Exceptions;

namespace Credfeto.Git.Mcp.Validation;

// A path a caller names, checked to be a plain file or folder inside the repository before it is passed to git
// (after --): never an option, never pathspec magic or a glob, never outside the root or inside .git, and never
// reached through a symlinked folder
public static class RepositoryPath
{
    public const string ROOT = ".";

    private const string GIT_FOLDER = ".git";
    private const string PARENT = "..";
    private const string FORBIDDEN = "*?[\\";

    // The path relative to the repository root, with '/' separators ("." for the root itself)
    public static string Resolve(string repositoryRoot, string? path)
    {
        if (!IsAcceptableText(path))
        {
            throw new InvalidRepositoryPathException();
        }

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path: path, basePath: root));
        string relative = Path.GetRelativePath(relativeTo: root, path: full);

        if (StringComparer.Ordinal.Equals(x: relative, y: ROOT))
        {
            return ROOT;
        }

        string[] parts = relative.Split(Path.DirectorySeparatorChar);

        if (
            Path.IsPathRooted(relative)
            || parts.Any(IsForbiddenComponent)
            || HasSymlinkedFolder(root: root, parts: parts)
        )
        {
            throw new InvalidRepositoryPathException();
        }

        return string.Join(separator: '/', value: parts);
    }

    private static bool IsAcceptableText([NotNullWhen(true)] string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && path is not ['-', ..]
            && path is not [':', ..]
            && !path.Any(IsForbiddenCharacter);
    }

    private static bool IsForbiddenCharacter(char character)
    {
        return character < ' ' || character == '\u007f' || FORBIDDEN.Contains(character, StringComparison.Ordinal);
    }

    private static bool IsForbiddenComponent(string component)
    {
        return StringComparer.Ordinal.Equals(x: component, y: PARENT)
            || StringComparer.OrdinalIgnoreCase.Equals(x: component, y: GIT_FOLDER);
    }

    // Every folder on the way to the last component must be a real folder: through a symlink the path could land
    // anywhere. The last component itself may be a symlink, as git tracks the link and not its target
    private static bool HasSymlinkedFolder(string root, string[] parts)
    {
        string current = root;

        foreach (string part in parts[..^1])
        {
            current = Path.Combine(path1: current, path2: part);

            // Read without following the link, so a dangling link counts too
            if (new FileInfo(current).LinkTarget is not null)
            {
                return true;
            }

            if (!Directory.Exists(current))
            {
                // Nothing further down exists yet, so nothing further down can be a link
                return false;
            }
        }

        return false;
    }
}
