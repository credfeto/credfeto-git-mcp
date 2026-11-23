using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Git;
using Credfeto.Git.Mcp.Git.Exceptions;

namespace Credfeto.Git.Mcp.Configuration;

public static class ProcessEnvironment
{
    private static readonly TimeSpan GitRootTimeout = TimeSpan.FromSeconds(30);
    private static readonly string[] ShowTopLevel = ["rev-parse", "--show-toplevel"];

    public static GitMcpEnvironmentSources Sources { get; } =
        new(
            GetVariable: Variable,
            CreatePrivateFolder: CreatePrivateTemporaryFolder,
            DefaultRepositoryRoot: GitMcpEnvironment.DEFAULT_REPOSITORY_ROOT,
            FindGitRoot: FindWorkingDirectoryGitRootAsync
        );

    public static ValueTask<GitMcpEnvironment> ResolveAsync(CancellationToken cancellationToken)
    {
        return GitMcpEnvironment.ResolveAsync(sources: Sources, cancellationToken: cancellationToken);
    }

    public static string CreatePrivateTemporaryFolder()
    {
        // The temporary directory is shared by every user, so a fixed name there could be created first, or replaced
        // by a symlink, by another account. This makes a new owner-only folder with a random name in one step
        return Directory.CreateTempSubdirectory(GitMcpEnvironment.APPLICATION_FOLDER + "-").FullName;
    }

    // The top-level folder of the git checkout holding the directory, or null when it is not in one (or git is
    // missing or fails)
    public static async ValueTask<string?> FindGitRootAsync(
        string? git,
        string directory,
        CancellationToken cancellationToken
    )
    {
        if (git is null)
        {
            return null;
        }

        GitResult result;

        try
        {
            result = await new GitRunner(git: git, repositoryRoot: directory).RunAsync(
                arguments: ShowTopLevel,
                timeout: GitRootTimeout,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception exception) when (exception is GitUnavailableException or GitTimeoutException)
        {
            return null;
        }

        if (!result.Succeeded)
        {
            return null;
        }

        // Only the one line break git adds is removed: a folder name may itself end in whitespace
        string topLevel = result.Output is [.., '\n'] ? result.Output[..^1] : result.Output;

        return Path.IsPathFullyQualified(topLevel) ? topLevel : null;
    }

    private static async ValueTask<string?> FindWorkingDirectoryGitRootAsync(CancellationToken cancellationToken)
    {
        string? directory = CurrentDirectory();

        return directory is null
            ? null
            : await FindGitRootAsync(
                git: GitExecutable.FromProcess(),
                directory: directory,
                cancellationToken: cancellationToken
            );
    }

    private static string? CurrentDirectory()
    {
        try
        {
            return Environment.CurrentDirectory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The directory the server started in has been removed or can't be read, so it is in no checkout
            return null;
        }
    }

    private static string? Variable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        return
            string.IsNullOrWhiteSpace(value)
            && StringComparer.Ordinal.Equals(x: name, y: GitMcpEnvironment.HOME_VARIABLE)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : value;
    }
}
