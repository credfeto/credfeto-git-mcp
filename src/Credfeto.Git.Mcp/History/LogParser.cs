using System;
using System.Collections.Generic;
using System.Linq;
using Credfeto.Git.Mcp.Models;

namespace Credfeto.Git.Mcp.History;

// Reads git log -z with FORMAT: commits end in NUL, fields are split by the unit separator, and the subject is last
public static class LogParser
{
    public const string FORMAT = "--format=%H%x1f%an%x1f%aI%x1f%s";

    private static readonly char[] UnitSeparator = ['\u001f'];

    public static IReadOnlyList<LogCommit> Parse(string output)
    {
        return
        [
            .. output
                .Split(separator: '\0', options: StringSplitOptions.RemoveEmptyEntries)
                .Select(static record => record.Split(separator: UnitSeparator, count: 4))
                .Where(static fields => fields.Length == 4)
                .Select(static fields => new LogCommit(
                    Sha: fields[0],
                    Author: fields[1],
                    Date: fields[2],
                    Subject: fields[3]
                )),
        ];
    }
}
