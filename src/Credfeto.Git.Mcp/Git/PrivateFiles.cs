using System;
using System.IO;

namespace Credfeto.Git.Mcp.Git;

// Files and folders only the server's own user can read: output may hold source code
public static class PrivateFiles
{
    private const UnixFileMode OWNER_FILE = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OWNER_FOLDER = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public static FileStreamOptions NewFile()
    {
        FileStreamOptions options = new()
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OWNER_FILE;
        }

        return options;
    }

    public static void CreateFolder(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(path);

            return;
        }

        _ = Directory.CreateDirectory(path: path, unixCreateMode: OWNER_FOLDER);
    }
}
