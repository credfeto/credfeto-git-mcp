using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Git;

namespace Credfeto.Git.Mcp.Output;

// Where large tool output goes: one private session folder per server process under the output directory, created
// on first use. Each result has a fixed name in it and replaces the previous one, so a session never piles up files
public sealed class OutputFolder
{
    public const string SESSION_PREFIX = "session-";

    private readonly Lazy<string> _session;

    public OutputFolder(string outputDirectory)
    {
        this.OutputDirectory = outputDirectory;
        this._session = new(valueFactory: this.CreateSession, mode: LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string OutputDirectory { get; }

    public string SessionDirectory => this._session.Value;

    // Writes to a new temporary file next to the target and then moves it into place, so a reader never sees a
    // half-written file and two calls at once can't interleave
    public async ValueTask<string> WriteAsync(
        string fileName,
        Func<string, CancellationToken, ValueTask> write,
        CancellationToken cancellationToken
    )
    {
        // Made again if it has gone, for example removed as stale by another server while this one sat idle
        string session = this.SessionDirectory;
        PrivateFiles.CreateFolder(session);

        string target = Path.Combine(path1: session, path2: fileName);
        string temporary =
            target + "." + Guid.NewGuid().ToString(format: "N", provider: CultureInfo.InvariantCulture) + ".tmp";

        try
        {
            await write(temporary, cancellationToken);
            File.Move(sourceFileName: temporary, destFileName: target, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        return target;
    }

    public ValueTask<string> WriteTextAsync(string fileName, string content, CancellationToken cancellationToken)
    {
        return this.WriteAsync(
            fileName: fileName,
            write: (path, token) => WriteTextFileAsync(path: path, content: content, cancellationToken: token),
            cancellationToken: cancellationToken
        );
    }

    // Removes the session folders earlier servers left behind once they are older than the age given
    public int RemoveStaleSessions(DateTimeOffset now, TimeSpan age)
    {
        if (!Directory.Exists(this.OutputDirectory))
        {
            return 0;
        }

        DateTime cutOff = now.UtcDateTime - age;

        return Directory
            .EnumerateDirectories(path: this.OutputDirectory, searchPattern: SESSION_PREFIX + "*")
            .Count(folder => Directory.GetLastWriteTimeUtc(folder) < cutOff && TryDelete(folder));
    }

    private string CreateSession()
    {
        PrivateFiles.CreateFolder(this.OutputDirectory);
        string session = Path.Combine(
            path1: this.OutputDirectory,
            path2: SESSION_PREFIX + Guid.NewGuid().ToString(format: "N", provider: CultureInfo.InvariantCulture)
        );
        PrivateFiles.CreateFolder(session);

        return session;
    }

    private static async ValueTask WriteTextFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using FileStream file = new(path: path, options: PrivateFiles.NewFile());
        await file.WriteAsync(buffer: Encoding.UTF8.GetBytes(content), cancellationToken: cancellationToken);
    }

    private static bool TryDelete(string folder)
    {
        try
        {
            Directory.Delete(path: folder, recursive: true);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Another server may be removing it too, or it is not ours to remove
            return false;
        }
    }
}
