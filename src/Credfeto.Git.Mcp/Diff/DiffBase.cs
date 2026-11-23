namespace Credfeto.Git.Mcp.Diff;

public static class DiffBase
{
    public const string MAIN = "main";
    public const string STAGED = "staged";
    public const string UNSTAGED = "unstaged";

    public static readonly string[] All = [MAIN, STAGED, UNSTAGED];
}
