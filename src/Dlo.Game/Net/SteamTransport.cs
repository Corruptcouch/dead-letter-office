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
/// fork and commit) → E0-03 (this class) → E12-02 (one-click invites).
/// </para>
/// <para>
/// <b>What actually blocks E0-01, corrected 2026-08-25.</b> It is not the app id — see
/// <see cref="TestAppId"/>, which costs nothing. It is <b>four Steam accounts on four
/// machines</b>: Steam runs one client per machine and one account at a time, so a genuine
/// four-peer Steam test needs four boxes. A Steam client on this machine is free and missing;
/// the other three machines are the real cost. Two peers on two machines would still answer
/// most of the question — whether the bindings work at all, and against which fork — and that
/// is the half that feeds ADR 0004 and decides whether E12 changes shape.
/// </para>
/// </remarks>
// ponytail: a stub that throws, standing in for the whole Steam path.
// Ceiling: any build configured for Steam fails at the moment it tries to host or join.
// There is no partial behaviour and no fallback - see above for why a fallback is worse.
// Upgrade: E0-03 replaces the two bodies once E0-01 names a working bindings fork. The
// signatures, the file and the selector in GameTransport all stay as they are.
public sealed class SteamTransport : IGameTransport
{
    /// <summary>
    /// <b>Spacewar</b> — Valve's public test app, and the development app id for this project
    /// until it has one of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> A real app id costs the Steam Direct fee (USD 100 per title at
    /// the time of writing, recoupable against the first USD 1,000 of revenue) and cannot even
    /// be bought until partner onboarding — identity, tax forms, banking — is complete. That is
    /// not a week-one activity, and E0-01 is a week-one story. 480 removes the conflict
    /// entirely: it initialises the <i>real</i> Steamworks API and gives real SteamID64s and
    /// real P2P traffic over Valve's relay, which is everything E0-01 asks about. Nothing in
    /// that spike is a question about <i>our</i> entitlement — it is a question about the
    /// bindings.
    /// </para>
    /// <para>
    /// <b>What it costs.</b> 480 is shared by every developer in the world who is testing, so
    /// lobby <i>matchmaking</i> on it finds strangers. That is noise for E12-01 and irrelevant
    /// to this class, which only moves packets. Invites appear as Spacewar. It obviously cannot
    /// ship.
    /// </para>
    /// <para>
    /// <b>The upgrade path is one line of export config, not a code change</b> — see
    /// <see cref="AppIdSetting"/>. That is the whole reason the value is read from a setting
    /// rather than compiled in: on the day this project has its own app id, dev builds must
    /// keep using 480 while shipping builds use the real one, and a constant cannot be both.
    /// </para>
    /// </remarks>
    public const uint TestAppId = 480;

    /// <summary>The project setting that names the Steam app id.</summary>
    /// <remarks>
    /// <para>
    /// Same shape as <see cref="GameTransport.Setting"/>, deliberately: the shipping build
    /// overrides it with Godot's feature-tagged settings — <c>dlo/network/steam_app_id.steam</c>
    /// alongside the <c>steam</c> feature on the export preset (E18-01) — so swapping in a real
    /// app id is a config edit and touches no C#.
    /// </para>
    /// <para>
    /// <b>There is a second app id, and it is a file.</b> The Steamworks SDK learns its app id
    /// from Steam when Steam launches the game, and from a <c>steam_appid.txt</c> beside the
    /// executable when anything else does — the editor, a test run, a double-click. E0-01 will
    /// need one containing <c>480</c>. It is git-ignored and <b>must never be exported</b>: a
    /// shipped <c>steam_appid.txt</c> overrides what Steam says the app is, which is a silent
    /// wrong-app failure of exactly the kind arch §1.4 warns export config produces.
    /// </para>
    /// </remarks>
    public const string AppIdSetting = "dlo/network/steam_app_id";

    /// <summary>
    /// The app id this build initialises Steam with: <see cref="TestAppId"/> unless the export
    /// preset overrides it.
    /// </summary>
    /// <remarks>
    /// Read through <c>long</c> rather than a narrower accessor because Godot stores an
    /// unadorned integer setting as a 64-bit one, and an app id edited into a preset by hand
    /// is an unadorned integer.
    /// </remarks>
    public static uint AppId =>
        (uint)ProjectSettings.GetSetting(AppIdSetting, TestAppId).AsInt64();

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
