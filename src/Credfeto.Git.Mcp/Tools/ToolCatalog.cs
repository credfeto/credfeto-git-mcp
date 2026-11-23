using System;
using System.Collections.Generic;
using System.Linq;
using ModelContextProtocol.Server;

namespace Credfeto.Git.Mcp.Tools;

public static class ToolCatalog
{
    public static IReadOnlyList<McpServerTool> Create(ToolContext context)
    {
        return Sort([new StatusTool(context), new DiffTool(context), new LogTool(context), new FetchTool(context)]);
    }

    // The server's tool collection lists tools in the order they were added, so sorting here, in one place, keeps
    // tools/list in name order however the tools above are declared
    public static IReadOnlyList<McpServerTool> Sort(IEnumerable<McpServerTool> tools)
    {
        return
        [
            .. tools.OrderBy(
                keySelector: static tool => tool.ProtocolTool.Name,
                comparer: StringComparer.OrdinalIgnoreCase
            ),
        ];
    }
}
