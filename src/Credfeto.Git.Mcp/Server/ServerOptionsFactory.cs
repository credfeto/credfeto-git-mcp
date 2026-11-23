using System.Collections.Generic;
using System.Reflection;
using ModelContextProtocol.Server;

namespace Credfeto.Git.Mcp.Server;

public static class ServerOptionsFactory
{
    public const string SERVER_NAME = "credfeto-git-mcp";

    public static McpServerOptions Create(string repositoryRoot, IReadOnlyList<McpServerTool> tools)
    {
        McpServerPrimitiveCollection<McpServerTool> collection = [];

        foreach (McpServerTool tool in tools)
        {
            collection.Add(tool);
        }

        return new()
        {
            ServerInfo = new() { Name = SERVER_NAME, Version = Version(typeof(ServerOptionsFactory).Assembly) },
            ServerInstructions = Instructions(repositoryRoot),
            Capabilities = new() { Tools = new() },
            ToolCollection = collection,
        };
    }

    private static string Instructions(string repositoryRoot)
    {
        return "Runs a fixed set of git operations on the repository in "
            + repositoryRoot
            + ", instead of raw git command lines. There is no repository, path, ref or option parameter: every tool works on that repository only. status shows the branch and changes, diff summarises a diff and returns the path of a file with the full patch, log lists recent commits and fetch fetches origin. Large output is written to files under the server's output folder and only their paths are returned.";
    }

    private static string Version(Assembly assembly)
    {
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
    }
}
