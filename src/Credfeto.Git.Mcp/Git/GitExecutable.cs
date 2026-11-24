using System;
using System.IO;
using System.Linq;

namespace Credfeto.Git.Mcp.Git;

// git is started by absolute path (never through a shell or a relative PATH entry)
public static class GitExecutable
{
    public const string GIT = "git";

    private const UnixFileMode EXECUTABLE =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    public static string? FromProcess()
    {
        return Find(Environment.GetEnvironmentVariable("PATH"));
    }

    public static string? Find(string? searchPath)
    {
        return (searchPath ?? string.Empty)
            .Split(separator: Path.PathSeparator, options: StringSplitOptions.RemoveEmptyEntries)
            .Where(Path.IsPathFullyQualified)
            .Select(directory => Path.Combine(path1: directory, path2: GIT))
            .FirstOrDefault(IsExecutable);
    }

    private static bool IsExecutable(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            return OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & EXECUTABLE) != UnixFileMode.None;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // For example a symbolic link into a directory this user cannot read
            return false;
        }
    }
}
