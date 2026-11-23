using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Credfeto.Git.Mcp.Models;

namespace Credfeto.Git.Mcp.Status;

// Reads git status --porcelain=v2 --branch -z: NUL-terminated records, where a rename or copy is followed by one more
// record holding the original path
public static class StatusParser
{
    public const string CHANGED = "changed";
    public const string RENAMED = "renamed";
    public const string COPIED = "copied";
    public const string UNMERGED = "unmerged";
    public const string UNTRACKED = "untracked";

    private const string BRANCH_OID = "# branch.oid ";
    private const string BRANCH_HEAD = "# branch.head ";
    private const string BRANCH_UPSTREAM = "# branch.upstream ";
    private const string BRANCH_AHEAD_BEHIND = "# branch.ab ";
    private const string INITIAL = "(initial)";
    private const string DETACHED = "(detached)";

    private static readonly char[] Space = [' '];

    public static StatusReport Parse(string output)
    {
        string[] records = output.Split(separator: '\0', options: StringSplitOptions.RemoveEmptyEntries);
        StatusReport report = new(Branch: null, Head: null, Upstream: null, Ahead: null, Behind: null, Entries: []);
        List<StatusEntry> entries = [];

        int index = 0;

        while (index < records.Length)
        {
            string record = records[index];

            if (record is ['#', ..])
            {
                report = WithHeader(report: report, record: record);
                index++;
            }
            else
            {
                index = AddEntry(records: records, index: index, entries: entries);
            }
        }

        return report with
        {
            Entries = entries,
        };
    }

    private static StatusReport WithHeader(StatusReport report, string record)
    {
        if (TryValue(record: record, prefix: BRANCH_OID, out string? oid))
        {
            return report with { Head = StringComparer.Ordinal.Equals(x: oid, y: INITIAL) ? null : oid };
        }

        if (TryValue(record: record, prefix: BRANCH_HEAD, out string? head))
        {
            return report with { Branch = StringComparer.Ordinal.Equals(x: head, y: DETACHED) ? null : head };
        }

        if (TryValue(record: record, prefix: BRANCH_UPSTREAM, out string? upstream))
        {
            return report with { Upstream = upstream };
        }

        return TryValue(record: record, prefix: BRANCH_AHEAD_BEHIND, out string? aheadBehind)
            ? WithAheadBehind(report: report, value: aheadBehind)
            : report;
    }

    // "+<ahead> -<behind>"
    private static StatusReport WithAheadBehind(StatusReport report, string value)
    {
        string[] parts = value.Split(separator: Space, options: StringSplitOptions.RemoveEmptyEntries);

        return
            parts.Length == 2
            && int.TryParse(
                s: parts[0],
                style: NumberStyles.AllowLeadingSign,
                provider: CultureInfo.InvariantCulture,
                out int ahead
            )
            && int.TryParse(
                s: parts[1],
                style: NumberStyles.AllowLeadingSign,
                provider: CultureInfo.InvariantCulture,
                out int behind
            )
            ? report with
            {
                Ahead = Math.Abs(ahead),
                Behind = Math.Abs(behind),
            }
            : report;
    }

    private static bool TryValue(string record, string prefix, [NotNullWhen(true)] out string? value)
    {
        if (record.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
        {
            value = record[prefix.Length..];

            return true;
        }

        value = null;

        return false;
    }

    // Returns the index of the next record, which is two further on for a rename or copy
    private static int AddEntry(string[] records, int index, List<StatusEntry> entries)
    {
        string record = records[index];

        switch (record)
        {
            case ['1', ' ', ..]:
                entries.Add(Ordinary(record));

                return index + 1;
            case ['2', ' ', ..]:
                entries.Add(
                    Renamed(record: record, originalPath: index + 1 < records.Length ? records[index + 1] : null)
                );

                return index + 2;
            case ['u', ' ', ..]:
                entries.Add(Unmerged(record));

                return index + 1;
            case ['?', ' ', ..]:
                entries.Add(new(Kind: UNTRACKED, Index: "?", WorkTree: "?", Path: record[2..], OriginalPath: null));

                return index + 1;
            default:
                // Ignored files ('!') are not asked for, and anything newer git adds is skipped
                return index + 1;
        }
    }

    // 1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>
    private static StatusEntry Ordinary(string record)
    {
        string[] fields = record.Split(separator: Space, count: 9);

        return Entry(kind: CHANGED, states: fields[1], path: fields[^1], originalPath: null);
    }

    // 2 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <X><score> <path>, then <origPath> as the next record
    private static StatusEntry Renamed(string record, string? originalPath)
    {
        string[] fields = record.Split(separator: Space, count: 10);
        string kind = fields[8] is ['C', ..] ? COPIED : RENAMED;

        return Entry(kind: kind, states: fields[1], path: fields[^1], originalPath: originalPath);
    }

    // u <XY> <sub> <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path>
    private static StatusEntry Unmerged(string record)
    {
        string[] fields = record.Split(separator: Space, count: 11);

        return Entry(kind: UNMERGED, states: fields[1], path: fields[^1], originalPath: null);
    }

    private static StatusEntry Entry(string kind, string states, string path, string? originalPath)
    {
        return new(
            Kind: kind,
            Index: states[..1],
            WorkTree: states.Length > 1 ? states[1..2] : ".",
            Path: path,
            OriginalPath: originalPath
        );
    }
}
