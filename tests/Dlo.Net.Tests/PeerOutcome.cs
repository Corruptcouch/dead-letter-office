using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dlo.Net.Tests;

/// <summary>
/// What one L3 peer did: whether it ended on its own, what it exited with, and the position it
/// stated on the way out.
/// </summary>
public sealed class PeerOutcome
{
    private const int TailLines = 12;

    private readonly IReadOnlyDictionary<string, string> _report;

    /// <summary>Records one peer's result.</summary>
    /// <param name="role">Which peer this is.</param>
    /// <param name="exited">Whether it ended on its own, rather than being killed.</param>
    /// <param name="exitCode">Its exit code, or <see cref="int.MinValue"/> if it never exited.</param>
    /// <param name="output">Everything it wrote to stdout and stderr.</param>
    public PeerOutcome(string role, bool exited, int exitCode, IReadOnlyList<string> output)
    {
        Role = role;
        Exited = exited;
        ExitCode = exitCode;
        Output = output;

        // The LAST report line, not the first: a peer prints one line and exits, but reading
        // the first would silently pick up a stale line if that ever stops being true.
        var line = output.LastOrDefault(PeerReport.IsReport);
        _report = line is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : PeerReport.Parse(line);
    }

    /// <summary>Which peer this is: <c>host</c>, or <c>client1</c>..<c>client3</c>.</summary>
    public string Role { get; }

    /// <summary>Whether it ended on its own rather than being killed by the harness.</summary>
    public bool Exited { get; }

    /// <summary>Its exit code. 0 is a peer that did its job; 2 is one that gave up waiting.</summary>
    public int ExitCode { get; }

    /// <summary>Everything it printed, in order.</summary>
    public IReadOnlyList<string> Output { get; }

    /// <summary>Whether it stated a position before exiting.</summary>
    public bool Reported => _report.Count > 0;

    /// <summary>What the host heard, by peer id. Empty on a client, and on a silent host.</summary>
    public IReadOnlyDictionary<int, int> Heard
    {
        get
        {
            var heard = new Dictionary<int, int>();
            // Not named `field`: that is a keyword inside a property accessor in C# 14,
            // and it binds to a synthesized backing field rather than to this local.
            var pairs = Field(PeerReport.Heard);
            if (pairs is PeerReport.None or "")
            {
                return heard;
            }

            foreach (var pair in pairs.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split(':');
                if (parts.Length == 2
                    && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var id)
                    && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var beat))
                {
                    heard[id] = beat;
                }
            }

            return heard;
        }
    }

    /// <summary>One field of the report, or <c>(none)</c> if the peer never reported.</summary>
    public string Field(string key) => _report.TryGetValue(key, out var value) ? value : "(none)";

    /// <summary>One numeric field of the report, or <c>-1</c> if it is missing or unparseable.</summary>
    public int Number(string key) =>
        int.TryParse(Field(key), CultureInfo.InvariantCulture, out var value) ? value : -1;

    /// <summary>
    /// This peer's line in a failure message: what it exited with and what it was holding.
    /// </summary>
    /// <remarks>
    /// A peer that never reported gets its output instead, because "no report" on its own says
    /// nothing about why — a crash, a missing assembly and a hang all look identical without it.
    /// </remarks>
    public string Describe()
    {
        var exit = Exited
            ? ExitCode.ToString(CultureInfo.InvariantCulture)
            : "never exited, killed by the harness";

        if (Reported)
        {
            return $"  {Role}: exit {exit} · " + string.Join(' ', _report.Select(f => $"{f.Key}={f.Value}"));
        }

        var tail = new StringBuilder($"  {Role}: exit {exit} · NO REPORT. Last output:");
        foreach (var line in Output.TakeLast(TailLines))
        {
            tail.AppendLine().Append("      ").Append(line);
        }

        if (Output.Count == 0)
        {
            tail.Append(" (it printed nothing at all)");
        }

        return tail.ToString();
    }
}
