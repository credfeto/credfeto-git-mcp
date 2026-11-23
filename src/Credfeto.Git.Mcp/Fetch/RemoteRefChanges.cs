using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Credfeto.Git.Mcp.Fetch;

// The remote-tracking refs a fetch added, moved or removed, each sorted by name
[DebuggerDisplay("Added={Added.Count}, Updated={Updated.Count}, Removed={Removed.Count}")]
public sealed record RemoteRefChanges(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Updated,
    IReadOnlyList<string> Removed
)
{
    public static RemoteRefChanges Compare(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after
    )
    {
        return new(
            Added: Sorted(after.Keys.Where(name => !before.ContainsKey(name))),
            Updated: Sorted(
                after
                    .Where(pair =>
                        before.TryGetValue(key: pair.Key, value: out string? old)
                        && !StringComparer.Ordinal.Equals(x: old, y: pair.Value)
                    )
                    .Select(static pair => pair.Key)
            ),
            Removed: Sorted(before.Keys.Where(name => !after.ContainsKey(name)))
        );
    }

    // Reads for-each-ref --format='%(objectname) %(refname)' output, one ref a line
    public static IReadOnlyDictionary<string, string> ParseRefs(string output)
    {
        Dictionary<string, string> refs = [with(StringComparer.Ordinal)];

        foreach (string line in output.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
        {
            int space = line.IndexOf(value: ' ', comparisonType: StringComparison.Ordinal);

            if (space > 0)
            {
                refs[line[(space + 1)..]] = line[..space];
            }
        }

        return refs;
    }

    private static IReadOnlyList<string> Sorted(IEnumerable<string> names)
    {
        return [.. names.Order(StringComparer.Ordinal)];
    }
}
