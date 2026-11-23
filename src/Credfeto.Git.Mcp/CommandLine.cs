using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Configuration;
using Credfeto.Git.Mcp.Configuration.Exceptions;
using Credfeto.Git.Mcp.Server;

namespace Credfeto.Git.Mcp;

// No option serves MCP over stdio, --self-test builds the server without serving it, --help shows the usage
public static class CommandLine
{
    public const string HELP = "--help";
    public const string SHORT_HELP = "-h";
    public const string SELF_TEST = "--self-test";
    public const int SUCCESS = 0;
    public const int CONFIGURATION_ERROR = 1;
    public const int USAGE_ERROR = 2;

    public static string Usage { get; } =
        string.Join(
            separator: Environment.NewLine,
            "Usage: " + ServerOptionsFactory.SERVER_NAME + " [" + SELF_TEST + " | " + HELP + "]",
            "",
            "With no option, serves MCP over stdio for the repository in "
                + GitMcpEnvironment.REPOSITORY_ROOT_VARIABLE
                + " (when unset, "
                + GitMcpEnvironment.DEFAULT_REPOSITORY_ROOT
                + " if it exists, else the git root of the current directory; a value that is not an absolute path is an",
            "error). The chosen repository root and where it came from are written to stderr at startup.",
            "",
            "Options:",
            "  "
                + SELF_TEST
                + "  Builds the server's tools and finds git on PATH, then exits: 0 when that works, 1 with a",
            "               one-line reason on stderr when it does not. Starts no MCP transport, reads no input and runs",
            "               no git command except, when "
                + GitMcpEnvironment.REPOSITORY_ROOT_VARIABLE
                + " is unset and the default folder is missing,",
            "               the one that finds the repository root.",
            "  " + SHORT_HELP + ", " + HELP + "   Shows this help and exits."
        );

    public static async ValueTask<int> RunAsync(
        IReadOnlyList<string> arguments,
        CommandLineContext context,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await DispatchAsync(arguments: arguments, context: context, cancellationToken: cancellationToken);
        }
        catch (InvalidGitMcpConfigurationException exception)
        {
            // stderr only: stdout is the MCP stream
            await context.Error.WriteLineAsync(
                buffer: exception.Message.AsMemory(),
                cancellationToken: cancellationToken
            );

            return CONFIGURATION_ERROR;
        }
    }

    private static async ValueTask<int> DispatchAsync(
        IReadOnlyList<string> arguments,
        CommandLineContext context,
        CancellationToken cancellationToken
    )
    {
        switch (arguments)
        {
            case []:
                return await ServeAsync(context: context, cancellationToken: cancellationToken);

            case [HELP]
            or [SHORT_HELP]:
                await context.Output.WriteLineAsync(buffer: Usage.AsMemory(), cancellationToken: cancellationToken);

                return SUCCESS;

            case [SELF_TEST]:
                return await SelfTest.RunAsync(
                    environment: await context.ResolveEnvironment(cancellationToken),
                    git: context.FindGit(),
                    error: context.Error,
                    cancellationToken: cancellationToken
                );

            default:
                await context.Error.WriteLineAsync(
                    buffer: ("Unrecognised arguments (see " + HELP + ")").AsMemory(),
                    cancellationToken: cancellationToken
                );

                return USAGE_ERROR;
        }
    }

    private static async ValueTask<int> ServeAsync(CommandLineContext context, CancellationToken cancellationToken)
    {
        GitMcpEnvironment environment = await context.ResolveEnvironment(cancellationToken);

        // stderr only: stdout is the MCP stream
        await context.Error.WriteLineAsync(
            buffer: (ServerOptionsFactory.SERVER_NAME + ": " + environment.DescribeRepositoryRoot()).AsMemory(),
            cancellationToken: cancellationToken
        );

        return await context.Serve(environment, cancellationToken);
    }
}
