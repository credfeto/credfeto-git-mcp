using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Output;
using Credfeto.Git.Mcp.Tests.Helpers;
using FunFair.Test.Common;
using Xunit;

namespace Credfeto.Git.Mcp.Tests.Output;

public sealed class OutputFolderTests : TestBase
{
    [Fact]
    public async Task FilesArePrivateAndReplacedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TemporaryDirectory directory = new();
        OutputFolder output = new(directory.Combine("output"));

        string first = await output.WriteTextAsync(
            fileName: "x.txt",
            content: "one",
            cancellationToken: cancellationToken
        );
        string second = await output.WriteTextAsync(
            fileName: "x.txt",
            content: "two",
            cancellationToken: cancellationToken
        );

        Assert.Equal(expected: first, actual: second);
        Assert.Equal(
            expected: "two",
            actual: await File.ReadAllTextAsync(path: second, cancellationToken: cancellationToken)
        );
        Assert.Single(Directory.GetFiles(output.SessionDirectory));

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                expected: UnixFileMode.UserRead | UnixFileMode.UserWrite,
                actual: File.GetUnixFileMode(second)
            );
            Assert.Equal(
                expected: UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                actual: File.GetUnixFileMode(output.SessionDirectory)
            );
        }
    }

    [Fact]
    public void NothingIsCreatedUntilUsed()
    {
        using TemporaryDirectory directory = new();
        OutputFolder output = new(directory.Combine("output"));

        Assert.Equal(
            expected: 0,
            actual: output.RemoveStaleSessions(now: DateTimeOffset.UnixEpoch, age: TimeSpan.Zero)
        );
        Assert.False(condition: Directory.Exists(output.OutputDirectory), userMessage: "not created");
    }

    [Fact]
    public async Task OnlyStaleSessionsAreRemovedAsync()
    {
        CancellationToken cancellationToken = this.CancellationToken();
        using TemporaryDirectory directory = new();
        OutputFolder current = new(directory.Combine("output"));
        _ = await current.WriteTextAsync(fileName: "x.txt", content: "one", cancellationToken: cancellationToken);
        string stale = Path.Combine(path1: current.OutputDirectory, path2: OutputFolder.SESSION_PREFIX + "old");
        string unrelated = Path.Combine(path1: current.OutputDirectory, path2: "keep-me");
        _ = Directory.CreateDirectory(stale);
        _ = Directory.CreateDirectory(unrelated);
        DateTime old = new(year: 2020, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);
        Directory.SetLastWriteTimeUtc(path: stale, lastWriteTimeUtc: old);
        Directory.SetLastWriteTimeUtc(path: unrelated, lastWriteTimeUtc: old);

        int removed = current.RemoveStaleSessions(
            now: new DateTimeOffset(
                year: 2020,
                month: 1,
                day: 10,
                hour: 0,
                minute: 0,
                second: 0,
                offset: TimeSpan.Zero
            ),
            age: TimeSpan.FromDays(2)
        );

        Assert.Equal(expected: 1, actual: removed);
        Assert.False(condition: Directory.Exists(stale), userMessage: "the stale session is gone");
        Assert.True(condition: Directory.Exists(unrelated), userMessage: "only session folders are removed");
        Assert.True(condition: Directory.Exists(current.SessionDirectory), userMessage: "the current session is new");
    }
}
