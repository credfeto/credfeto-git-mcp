using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Git.Exceptions;

namespace Credfeto.Git.Mcp.Git;

// Runs git on one repository: by absolute path, with -C <root>, no shell, stdin closed, and none of the variables that
// would point git at another repository
public sealed class GitRunner
{
    // Variables git exports to hooks that point it at a specific repository; inherited by the child they would
    // override the -C directory
    private static readonly string[] RepositoryLocatingVariables =
    [
        "GIT_DIR",
        "GIT_WORK_TREE",
        "GIT_INDEX_FILE",
        "GIT_OBJECT_DIRECTORY",
        "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_COMMON_DIR",
        "GIT_NAMESPACE",
        "GIT_PREFIX",
        "GIT_CEILING_DIRECTORIES",
        "GIT_DISCOVERY_ACROSS_FILESYSTEM",
    ];

    private readonly string _git;

    public GitRunner(string git, string repositoryRoot)
    {
        this._git = git;
        this.RepositoryRoot = repositoryRoot;
    }

    public string RepositoryRoot { get; }

    public ValueTask<GitResult> RunAsync(
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        return this.RunCoreAsync(
            arguments: arguments,
            outputFile: null,
            timeout: timeout,
            cancellationToken: cancellationToken
        );
    }

    // Streams git's stdout into a new file instead of memory, for output that may be large
    public ValueTask<GitResult> RunToFileAsync(
        IReadOnlyList<string> arguments,
        string outputFile,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        return this.RunCoreAsync(
            arguments: arguments,
            outputFile: outputFile,
            timeout: timeout,
            cancellationToken: cancellationToken
        );
    }

    private async ValueTask<GitResult> RunCoreAsync(
        IReadOnlyList<string> arguments,
        string? outputFile,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);

        using Process process = new() { StartInfo = this.CreateStartInfo(arguments) };

        Start(process);

        try
        {
            Task<string> error = process.StandardError.ReadToEndAsync(limit.Token);
            string output = await ReadOutputAsync(
                process: process,
                outputFile: outputFile,
                cancellationToken: limit.Token
            );
            await process.WaitForExitAsync(limit.Token);

            return new(ExitCode: process.ExitCode, Output: output, Error: await error);
        }
        catch (OperationCanceledException exception)
        {
            Kill(process);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new GitTimeoutException(
                message: "git did not finish in time and was stopped",
                innerException: exception
            );
        }
    }

    private static void Start(Process process)
    {
        try
        {
            if (!process.Start())
            {
                throw new GitUnavailableException();
            }
        }
        catch (Win32Exception exception)
        {
            throw new GitUnavailableException(message: "git could not be started", innerException: exception);
        }

        process.StandardInput.Close();
    }

    private static async ValueTask<string> ReadOutputAsync(
        Process process,
        string? outputFile,
        CancellationToken cancellationToken
    )
    {
        if (outputFile is null)
        {
            return await process.StandardOutput.ReadToEndAsync(cancellationToken);
        }

        await using FileStream file = new(path: outputFile, options: PrivateFiles.NewFile());
        await process.StandardOutput.BaseStream.CopyToAsync(destination: file, cancellationToken: cancellationToken);

        return string.Empty;
    }

    private ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = this._git,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(this.RepositoryRoot);

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (string variable in RepositoryLocatingVariables)
        {
            _ = startInfo.Environment.Remove(variable);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_PAGER"] = "cat";
        startInfo.Environment["LC_ALL"] = "C";

        return startInfo;
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It already exited
        }
    }
}
