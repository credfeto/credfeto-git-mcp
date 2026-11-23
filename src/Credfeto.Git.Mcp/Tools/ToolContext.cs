using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Git;
using Credfeto.Git.Mcp.Git.Exceptions;
using Credfeto.Git.Mcp.Output;
using Credfeto.Git.Mcp.Tools.Exceptions;

namespace Credfeto.Git.Mcp.Tools;

// What every tool shares: git on the configured repository and the folder for large output. A failure reaches the
// client as fixed text; git's own error output goes to a file in the session folder, and only its path is returned
public sealed class ToolContext
{
    public static readonly TimeSpan LocalTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromMinutes(5);

    private readonly string? _git;

    public ToolContext(string repositoryRoot, string? git, OutputFolder output)
    {
        this.RepositoryRoot = repositoryRoot;
        this._git = git;
        this.Output = output;
    }

    public string RepositoryRoot { get; }

    public OutputFolder Output { get; }

    public GitRunner CreateGit()
    {
        return this._git is null
            ? throw new ToolFailedException("git was not found on PATH")
            : new(git: this._git, repositoryRoot: this.RepositoryRoot);
    }

    public async ValueTask<GitResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await this.CreateGit()
                .RunAsync(arguments: arguments, timeout: timeout, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is GitUnavailableException or GitTimeoutException)
        {
            throw new ToolFailedException(message: exception.Message, innerException: exception);
        }
    }

    // git's output, or a ToolFailedException when git fails
    public async ValueTask<string> RunCheckedAsync(
        string operation,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        GitResult result = await this.RunAsync(
            arguments: arguments,
            timeout: timeout,
            cancellationToken: cancellationToken
        );

        return result.Succeeded
            ? result.Output
            : throw await this.FailureAsync(operation: operation, result: result, cancellationToken: cancellationToken);
    }

    // Streams git's output to a file in the session folder and returns the file's path
    public ValueTask<string> RunToFileCheckedAsync(
        string operation,
        IReadOnlyList<string> arguments,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        return this.Output.WriteAsync(
            fileName: fileName,
            write: (temporary, token) => this.RunToTemporaryFileAsync(operation, arguments, temporary, token),
            cancellationToken: cancellationToken
        );
    }

    // A failure throws before the file is moved into place, so the last good output is kept
    private async ValueTask RunToTemporaryFileAsync(
        string operation,
        IReadOnlyList<string> arguments,
        string temporary,
        CancellationToken cancellationToken
    )
    {
        GitResult result = await this.RunToFileAsync(
            arguments: arguments,
            outputFile: temporary,
            cancellationToken: cancellationToken
        );

        if (!result.Succeeded)
        {
            throw await this.FailureAsync(operation: operation, result: result, cancellationToken: cancellationToken);
        }
    }

    private async ValueTask<GitResult> RunToFileAsync(
        IReadOnlyList<string> arguments,
        string outputFile,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await this.CreateGit()
                .RunToFileAsync(
                    arguments: arguments,
                    outputFile: outputFile,
                    timeout: LocalTimeout,
                    cancellationToken: cancellationToken
                );
        }
        catch (Exception exception) when (exception is GitUnavailableException or GitTimeoutException)
        {
            throw new ToolFailedException(message: exception.Message, innerException: exception);
        }
    }

    public async ValueTask<ToolFailedException> FailureAsync(
        string operation,
        GitResult result,
        CancellationToken cancellationToken
    )
    {
        string errorFile = await this.Output.WriteTextAsync(
            fileName: operation + "-error.txt",
            content: result.Error,
            cancellationToken: cancellationToken
        );

        return new(
            "git "
                + operation
                + " failed with exit code "
                + result.ExitCode.ToString(CultureInfo.InvariantCulture)
                + "; git's error output is in "
                + errorFile
        );
    }
}
