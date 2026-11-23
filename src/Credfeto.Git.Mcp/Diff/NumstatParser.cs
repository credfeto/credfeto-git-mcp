using System;
using System.Collections.Generic;
using System.Globalization;
using Credfeto.Git.Mcp.Models;

namespace Credfeto.Git.Mcp.Diff;

// Reads git diff --numstat -z: "<added>\t<deleted>\t<path>\0", or for a rename "<added>\t<deleted>\t\0<old>\0<new>\0";
// a binary file has "-" for both counts
public static class NumstatParser
{
    private const string BINARY = "-";

    private static readonly char[] Tab = ['\t'];

    public static IReadOnlyList<DiffFile> Parse(string output)
    {
        string[] records = output.Split('\0');
        List<DiffFile> files = [];
        int index = 0;

        while (index < records.Length)
        {
            string[] fields = records[index].Split(separator: Tab, count: 3);

            if (fields.Length != 3)
            {
                // The empty record after the last NUL
                index++;

                continue;
            }

            if (fields[2].Length == 0 && index + 2 < records.Length)
            {
                files.Add(ToFile(fields: fields, path: records[index + 2], originalPath: records[index + 1]));
                index += 3;
            }
            else
            {
                files.Add(ToFile(fields: fields, path: fields[2], originalPath: null));
                index++;
            }
        }

        return files;
    }

    private static DiffFile ToFile(string[] fields, string path, string? originalPath)
    {
        bool binary = StringComparer.Ordinal.Equals(x: fields[0], y: BINARY);

        return new(
            Path: path,
            OriginalPath: originalPath,
            Insertions: binary ? null : Count(fields[0]),
            Deletions: binary ? null : Count(fields[1]),
            Binary: binary
        );
    }

    private static int Count(string value)
    {
        return int.TryParse(s: value, style: NumberStyles.None, provider: CultureInfo.InvariantCulture, out int count)
            ? count
            : 0;
    }
}
