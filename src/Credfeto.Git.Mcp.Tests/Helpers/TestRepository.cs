using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Output;
using Credfeto.Git.Mcp.Tools;

namespace Credfeto.Git.Mcp.Tests.Helpers;

// A temporary work tree (and, when asked for, a bare origin it was cloned from), plus a separate output folder
public sealed class TestRepository : IDisposable
{
    private readonly TemporaryDirectory _directory;

    private TestRepository(TemporaryDirectory directory)
    {
        this._directory = directory;
        this.Root = directory.Combine("work");
        this.Origin = directory.Combine("origin.git");
        this.OutputDirectory = directory.Combine("output");
        this.Context = new(
            repositoryRoot: this.Root,
            git: TestGit.Executable,
            output: new OutputFolder(this.OutputDirectory)
        );
    }

    public string Root { get; }

    public string Origin { get; }

    public string OutputDirectory { get; }

    public ToolContext Context { get; }

    public void Dispose()
    {
        this._directory.Dispose();
    }

    // An empty repository: main has no commits yet
    public static async ValueTask<TestRepository> CreateEmptyAsync(CancellationToken cancellationToken)
    {
        TestRepository repository = new(new TemporaryDirectory());
        _ = Directory.CreateDirectory(repository.Root);
        await TestGit.InitAsync(repository: repository.Root, cancellationToken: cancellationToken);

        return repository;
    }

    // A repository with one commit on main holding README.md
    public static async ValueTask<TestRepository> CreateAsync(CancellationToken cancellationToken)
    {
        TestRepository repository = await CreateEmptyAsync(cancellationToken);
        await repository.WriteAsync(relative: "README.md", content: "readme\n", cancellationToken: cancellationToken);
        await repository.CommitAllAsync(message: "first", cancellationToken: cancellationToken);

        return repository;
    }

    // A repository cloned from a bare origin that has one commit on main
    public static async ValueTask<TestRepository> CreateWithOriginAsync(CancellationToken cancellationToken)
    {
        TestRepository repository = await CreateAsync(cancellationToken);
        _ = await TestGit.RunAsync(
            repository: repository._directory.Path,
            arguments: ["clone", "--quiet", "--bare", repository.Root, repository.Origin],
            cancellationToken: cancellationToken
        );
        await repository.GitAsync(
            arguments: ["remote", "add", "origin", repository.Origin],
            cancellationToken: cancellationToken
        );
        await repository.GitAsync(arguments: ["fetch", "--quiet", "origin"], cancellationToken: cancellationToken);

        return repository;
    }

    public async ValueTask WriteAsync(string relative, string content, CancellationToken cancellationToken)
    {
        string path = Path.Combine(path1: this.Root, path2: relative);
        string? directory = Path.GetDirectoryName(path);

        if (directory is not null)
        {
            _ = Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path: path, contents: content, cancellationToken: cancellationToken);
    }

    public ValueTask CommitAllAsync(string message, CancellationToken cancellationToken)
    {
        return TestGit.CommitAllAsync(repository: this.Root, message: message, cancellationToken: cancellationToken);
    }

    public async ValueTask GitAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        _ = await TestGit.RunAsync(repository: this.Root, arguments: arguments, cancellationToken: cancellationToken);
    }

    // A second clone of origin, to change origin behind this repository's back
    public async ValueTask<string> CloneOriginAsync(CancellationToken cancellationToken)
    {
        string clone = this._directory.Combine("other");
        _ = await TestGit.RunAsync(
            repository: this._directory.Path,
            arguments: ["clone", "--quiet", this.Origin, clone],
            cancellationToken: cancellationToken
        );

        return clone;
    }
}
