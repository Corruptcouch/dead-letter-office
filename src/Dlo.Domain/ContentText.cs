using System;
using System.Collections.Generic;

namespace Dlo.Domain;

/// <summary>
/// Turns the two authored text formats into rows and key/value pairs. It reads; it judges
/// nothing — <see cref="ContentSet"/> is what decides whether the values are allowed.
/// </summary>
/// <remarks>
/// A <c>.tres</c> is read as text rather than through Godot because <c>ContentTool</c> may
/// reference Domain and nothing else (arch §1.3), and Domain may not reference Godot (arch §2).
/// That is workable precisely because arch §7 keeps <c>.tres</c> out of LFS to stay diffable:
/// a format that has to stay readable by a human stays readable by forty lines of parser.
/// </remarks>
public static class ContentText
{
    /// <summary>
    /// The <c>[resource]</c> block of a <c>.tres</c>, as <c>key → value</c> with quotes stripped.
    /// </summary>
    /// <remarks>
    /// Scalars only, which is why archetypes are authored one per file with flat properties.
    /// Anything structured — an array, a dictionary, a sub-resource — is silently not returned
    /// here, and the loader reports the property as missing rather than guessing.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Resource(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var inResource = false;

        foreach (var raw in Lines(text ?? string.Empty))
        {
            var line = raw.Trim();

            if (line.StartsWith('['))
            {
                inResource = line.Equals("[resource]", StringComparison.Ordinal);
                continue;
            }

            if (!inResource || line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }

            var split = line.IndexOf('=');
            if (split <= 0)
            {
                continue;
            }

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim().Trim('"');
            values[key] = value;
        }

        return values;
    }

    /// <summary>
    /// The rows of a data table: comma-separated, <c>#</c> for a comment, blanks skipped.
    /// </summary>
    /// <remarks>
    /// No quoting and no escapes, deliberately. A content table that needs an embedded comma is
    /// a content table that has outgrown this, and adding quoting quietly is how a format ends
    /// up with two shapes (standards §9).
    /// </remarks>
    public static IReadOnlyList<ContentRow> Rows(string text)
    {
        var rows = new List<ContentRow>();
        var number = 0;

        foreach (var raw in Lines(text ?? string.Empty))
        {
            number++;
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var fields = line.Split(',');
            for (var i = 0; i < fields.Length; i++)
            {
                fields[i] = fields[i].Trim();
            }

            rows.Add(new ContentRow(number, fields));
        }

        return rows;
    }

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
