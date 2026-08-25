using System;
using System.Collections.Generic;

namespace Dlo.Net.Tests;

/// <summary>
/// The one line each L3 peer prints just before it exits, and the parser the harness reads it
/// with. Both halves live here because both halves are in this assembly — see the csproj for
/// why that was worth doing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The report is the failure message.</b> E0-09 requires that a failing L3 run names which
/// peer disagreed and what it held, so every peer states its whole position — not just
/// whether it passed. A peer that prints <c>status=timeout</c> and nothing else costs an hour
/// every time it fires.
/// </para>
/// <para>
/// Printed at exit rather than streamed, deliberately: stdout through a pipe is
/// block-buffered on Windows, so a line printed mid-run may not arrive until the process
/// ends. Nothing in this harness sequences on stdout — the peers find each other by
/// retrying, not by waiting for each other's output.
/// </para>
/// </remarks>
public static class PeerReport
{
    /// <summary>Marks a line as a report rather than engine noise.</summary>
    public const string Prefix = "DLO-L3";

    /// <summary>Which peer this is: <c>host</c>, or <c>client1</c>..<c>client3</c>.</summary>
    public const string Role = "role";

    /// <summary>Which scripted ending this peer was launched to play. See <see cref="Scenario"/>.</summary>
    public const string Scenario = "scenario";

    /// <summary>How this peer finished: <see cref="Ok"/>, <see cref="Timeout"/> or <see cref="Idle"/>.</summary>
    public const string Status = "status";

    /// <summary>This peer's multiplayer id. Godot gives the host 1 and clients a random uint.</summary>
    public const string Id = "id";

    /// <summary>Peers the host counts as connected, itself included. Always 0 on a client.</summary>
    public const string Crew = "crew";

    /// <summary>The replicated value this peer was holding at exit.</summary>
    public const string Beat = "beat";

    /// <summary>Intent RPCs the host received. Always 0 on a client.</summary>
    public const string Intents = "intents";

    /// <summary>What the host heard, as <c>id:beat</c> pairs, or <see cref="None"/>.</summary>
    public const string Heard = "heard";

    /// <summary>
    /// What this peer's <c>SessionRoot</c> looked like after it tore its session down:
    /// <see cref="Clean"/>, <see cref="Dirty"/>, or <see cref="Live"/> if it never did.
    /// </summary>
    public const string Teardown = "teardown";

    /// <summary>Connection attempts this peer made. &gt; 1 means it out-raced the host's boot.</summary>
    public const string Attempts = "attempts";

    /// <summary>Seconds from this peer's first frame to its last.</summary>
    public const string Elapsed = "elapsed";

    /// <summary>This peer did what it was launched to do.</summary>
    public const string Ok = "ok";

    /// <summary>This peer gave up. Every other field says what it was still waiting for.</summary>
    public const string Timeout = "timeout";

    /// <summary>Launched with no role to play — the warm-up pass that creates <c>.godot/</c>.</summary>
    public const string Idle = "idle";

    /// <summary>This peer left the session deliberately — the leaver in <c>departure</c> (E0-10).</summary>
    public const string Left = "left";

    /// <summary>This peer's host went away and it ended itself — <c>hostloss</c> (E0-10).</summary>
    public const string HostLost = "hostlost";

    /// <summary>This peer tore its own session down — the host in <c>hostloss</c> (E0-10).</summary>
    public const string TornDown = "torndown";

    /// <summary>Teardown left nothing behind: no peer, and no <c>HostSession</c>.</summary>
    public const string Clean = "clean";

    /// <summary>
    /// Teardown returned but left something behind. The whole point of the field: a
    /// <c>SessionRoot</c> that still reports a live session after <c>Leave()</c> is the
    /// orphan E0-10 exists to catch, and it is invisible from the exit code.
    /// </summary>
    public const string Dirty = "dirty";

    /// <summary>This peer never tore its session down, so there is nothing to say about it.</summary>
    public const string Live = "live";

    /// <summary>Stands in for an empty <see cref="Heard"/>, so the field never parses as absent.</summary>
    public const string None = "-";

    /// <summary>E1-06: whether this client ended up holding the contested parcel.</summary>
    public const string Won = "won";

    /// <summary>E1-06: physics joints this peer created. Must be zero anywhere but the host.</summary>
    public const string Joints = "joints";

    /// <summary>E1-06: carriers the host granted a grip to. Must be exactly one.</summary>
    public const string Holders = "holders";

    /// <summary>E1-06: the peer ids this peer believes hold the parcel.</summary>
    public const string Holder = "holder";

    /// <summary>E1-06: where this peer's copy of the parcel finished, as <c>x|y|z</c>.</summary>
    public const string Parcel = "parcel";

    /// <summary>E1-06: the largest single-frame jump this peer saw the parcel make.</summary>
    public const string Jump = "jump";

    /// <summary>Whether <paramref name="line"/> is a peer report rather than engine output.</summary>
    public static bool IsReport(string line) =>
        line.StartsWith(Prefix + " ", StringComparison.Ordinal);

    /// <summary>
    /// Reads a report line into its fields. Unknown keys are kept, so a field added on the
    /// peer side is readable here before anything is taught to ask for it.
    /// </summary>
    public static Dictionary<string, string> Parse(string line)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in line[Prefix.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = token.IndexOf('=');
            if (split > 0)
            {
                fields[token[..split]] = token[(split + 1)..];
            }
        }

        return fields;
    }
}
