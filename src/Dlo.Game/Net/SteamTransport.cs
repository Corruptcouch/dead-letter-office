using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// The shipping transport (arch §3.5) — <b>a stub. It does not work yet, and it says so
/// loudly rather than quietly doing something else.</b>
/// </summary>
/// <remarks>
/// <para>
/// This file exists before it works on purpose. Arch §3.5 calls the C# path to Steam P2P
/// "the least mature dependency in the whole plan", and E0-01 is the spike that finds out
/// whether it is usable at all. Writing the seam now means that spike's answer — yes, fork
/// it, or no — lands in <i>one file</i> instead of rippling through gameplay code.
/// </para>
/// <para>
/// <b>It throws rather than falling back to <see cref="EnetTransport"/>, and that is the
/// important decision here.</b> Steam is the shipping default and ENet is not; a silent
/// fallback would produce a build that looks fine on the developer's machine, ships, and then
/// cannot see a single Steam friend. A loud failure is recoverable. A quiet wrong transport
/// is a launch-day incident.
/// </para>
/// <para>
/// <b>What E0-03 fills in, and nothing else.</b> Both methods return a configured
/// <c>SteamMultiplayerPeer</c>. Everything Steam-shaped — the bindings fork, the app id,
/// <c>SteamMultiplayerPeer</c>, SteamID64s, the missing channels implementation — stays inside
/// this file. Nothing above <see cref="IGameTransport"/> changes, and E0-03's first acceptance
/// criterion is exactly that: "satisfies <c>IGameTransport</c> with no gameplay code change
/// anywhere." If filling this in requires touching a file outside this folder, the seam was
/// wrong and that is the finding.
/// </para>
/// <para>
/// <b>Blocked on, in order:</b> E0-01 (does the C# path work at four peers, and against which
/// fork and commit) → E0-03 (this class) → E12-02 (one-click invites). E0-01 additionally
/// needs a Steam client and an app id, neither of which exists on the development machine as
/// of 2026-08-24.
/// </para>
/// </remarks>
// ponytail: a stub that throws, standing in for the whole Steam path.
// Ceiling: any build configured for Steam fails at the moment it tries to host or join.
// There is no partial behaviour and no fallback - see above for why a fallback is worse.
// Upgrade: E0-03 replaces the two bodies once E0-01 names a working bindings fork. The
// signatures, the file and the selector in GameTransport all stay as they are.
public sealed class SteamTransport : IGameTransport
{
    /// <inheritdoc/>
    public MultiplayerPeer CreateHost(int maxPeers) => throw NotYet();

    /// <inheritdoc/>
    public MultiplayerPeer CreateClient(string address) => throw NotYet();

    private static System.NotSupportedException NotYet() =>
        new("SteamTransport is a stub (E0-03). The Steam C# path is unproven until the E0-01 "
            + "spike reports, so there is deliberately no fallback here. For development and "
            + "tests use the ENet transport: set the project setting "
            + $"'{GameTransport.Setting}' to '{GameTransport.Enet}'.");
}
