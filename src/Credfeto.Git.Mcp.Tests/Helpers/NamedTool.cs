using System.Threading;
using System.Threading.Tasks;
using Credfeto.Git.Mcp.Tools;

namespace Credfeto.Git.Mcp.Tests.Helpers;

// A tool that only has a name, to check ordering
internal sealed class NamedTool : GitTool
{
    public NamedTool(string name)
        : base(name: name, title: name, description: name, schema: [], hints: ToolHints.LocalReadOnly) { }

    protected override ValueTask<string> ExecuteAsync(ToolArguments arguments, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult("{}");
    }
}
