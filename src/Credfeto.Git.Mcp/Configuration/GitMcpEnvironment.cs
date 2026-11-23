using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Configuration.Exceptions;

namespace Credfeto.Git.Mcp.Configuration;

[DebuggerDisplay("RepositoryRoot={RepositoryRoot} ({RepositoryRootSource}), OutputDirectory={OutputDirectory}")]
public sealed record GitMcpEnvironment(
    string RepositoryRoot,
    RepositoryRootSource RepositoryRootSource,
    string OutputDirectory
)
{
    public const string REPOSITORY_ROOT_VARIABLE = "GIT_MCP_REPO_ROOT";
    public const string OUTPUT_DIRECTORY_VARIABLE = "GIT_MCP_OUTPUT_DIR";
    public const string STATE_HOME_VARIABLE = "XDG_STATE_HOME";
    public const string HOME_VARIABLE = "HOME";
    public const string DEFAULT_REPOSITORY_ROOT = "/workspace/repo";
    public const string APPLICATION_FOLDER = "credfeto-git-mcp";

    private const string OUTPUT_FOLDER = "output";
    private const string PRIVATE_FOLDER_DESCRIPTION = "a new private folder in the temporary directory";

    public static async ValueTask<GitMcpEnvironment> ResolveAsync(
        GitMcpEnvironmentSources sources,
        CancellationToken cancellationToken
    )
    {
        (string repositoryRoot, RepositoryRootSource source) = await ResolveRepositoryRootAsync(
            value: sources.GetVariable(REPOSITORY_ROOT_VARIABLE),
            sources: sources,
            cancellationToken: cancellationToken
        );

        string outputDirectory = ResolveOutputDirectory(
            value: sources.GetVariable(OUTPUT_DIRECTORY_VARIABLE),
            homeOutputDirectory: HomeOutputDirectory(sources.GetVariable),
            createPrivateFolder: sources.CreatePrivateFolder
        );

        return new(RepositoryRoot: repositoryRoot, RepositoryRootSource: source, OutputDirectory: outputDirectory);
    }

    public string DescribeRepositoryRoot()
    {
        string reason = this.RepositoryRootSource switch
        {
            RepositoryRootSource.DEFAULT => "the default; " + REPOSITORY_ROOT_VARIABLE + " is not set",
            RepositoryRootSource.GIT_ROOT => "the git root of the current directory; "
                + REPOSITORY_ROOT_VARIABLE
                + " is not set and the default folder does not exist",
            _ => "from " + REPOSITORY_ROOT_VARIABLE,
        };

        return "repository root " + this.RepositoryRoot + " (" + reason + ")";
    }

    private static async ValueTask<(string Root, RepositoryRootSource Source)> ResolveRepositoryRootAsync(
        string? value,
        GitMcpEnvironmentSources sources,
        CancellationToken cancellationToken
    )
    {
        // Only an unset variable gets a fallback: a value that is set but not absolute (relative, empty or whitespace)
        // is a misconfiguration, and silently swapping it for the default would hide it
        if (value is not null)
        {
            return (
                RequireAbsolute(
                    name: REPOSITORY_ROOT_VARIABLE,
                    value: value,
                    defaultDescription: sources.DefaultRepositoryRoot
                        + " (or, when that does not exist, the git root of the current directory)"
                ),
                RepositoryRootSource.VARIABLE
            );
        }

        // The agent container mounts the repository at the default; git is only asked when that is missing
        if (Directory.Exists(sources.DefaultRepositoryRoot))
        {
            return (Normalise(sources.DefaultRepositoryRoot), RepositoryRootSource.DEFAULT);
        }

        string? gitRoot = await sources.FindGitRoot(cancellationToken);

        return IsAbsolute(gitRoot)
            ? (Normalise(gitRoot), RepositoryRootSource.GIT_ROOT)
            : throw new InvalidGitMcpConfigurationException(
                REPOSITORY_ROOT_VARIABLE
                    + " is not set, "
                    + sources.DefaultRepositoryRoot
                    + " does not exist and the current directory is not in a git checkout (or git could not be run): set "
                    + REPOSITORY_ROOT_VARIABLE
                    + " to the repository's absolute path, or start the server inside the repository"
            );
    }

    private static string ResolveOutputDirectory(
        string? value,
        string? homeOutputDirectory,
        Func<string> createPrivateFolder
    )
    {
        if (homeOutputDirectory is null)
        {
            // The private folder is only created when it is used, never just to name it in an error
            return value is null
                ? Normalise(PrivateFolder(createPrivateFolder))
                : RequireAbsolute(
                    name: OUTPUT_DIRECTORY_VARIABLE,
                    value: value,
                    defaultDescription: PRIVATE_FOLDER_DESCRIPTION
                );
        }

        string fallback = Normalise(homeOutputDirectory);

        return value is null
            ? fallback
            : RequireAbsolute(name: OUTPUT_DIRECTORY_VARIABLE, value: value, defaultDescription: fallback);
    }

    private static string PrivateFolder(Func<string> createPrivateFolder)
    {
        try
        {
            return createPrivateFolder();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidGitMcpConfigurationException(
                "There is no home directory for large tool output, and a private folder could not be created in the temporary directory: set "
                    + OUTPUT_DIRECTORY_VARIABLE
                    + " to an absolute path",
                innerException: exception
            );
        }
    }

    private static string? HomeOutputDirectory(Func<string, string?> getVariable)
    {
        // Each user gets their own folder, so one user's server can neither block nor read another user's output
        string? stateHome = getVariable(STATE_HOME_VARIABLE);

        if (IsAbsolute(stateHome))
        {
            return Path.Combine(path1: stateHome, path2: APPLICATION_FOLDER, path3: OUTPUT_FOLDER);
        }

        string? home = getVariable(HOME_VARIABLE);

        return IsAbsolute(home) ? Path.Combine(home, ".local", "state", APPLICATION_FOLDER, OUTPUT_FOLDER) : null;
    }

    private static bool IsAbsolute([NotNullWhen(true)] string? value)
    {
        // Only an absolute path is accepted, so a relative value can never resolve against whatever directory the
        // server happens to start in
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value);
    }

    private static string RequireAbsolute(string name, string value, string defaultDescription)
    {
        return IsAbsolute(value)
            ? Normalise(value)
            : throw new InvalidGitMcpConfigurationException(
                name
                    + " is set to \""
                    + value
                    + "\", which is not an absolute path: set it to an absolute path, or unset it to use "
                    + defaultDescription
            );
    }

    private static string Normalise(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
