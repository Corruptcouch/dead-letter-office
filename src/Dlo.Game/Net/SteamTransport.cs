using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// The shipping transport (arch §3.5) — <b>a stub that throws until E0-01 reports.</b>
/// </summary>
/// <remarks>
/// It throws rather than falling back to <see cref="EnetTransport"/>: a silent fallback ships a
/// build that looks fine on the developer's machine and then cannot see a single Steam friend.
/// E0-03 fills in the two method bodies and nothing outside this file — if it needs to touch
/// gameplay code, the seam was wrong and that is the finding.
/// </remarks>
// ponytail: a stub that throws, standing in for the whole Steam path.
// Ceiling: any build configured for Steam fails the moment it hosts or joins. No fallback.
// Upgrade: E0-03 replaces the two bodies once E0-01 names a working bindings fork.
public sealed class SteamTransport : IGameTransport
{
    /// <summary>
    /// <b>Spacewar</b> — Valve's public test app, and this project's development app id.
    /// </summary>
    /// <remarks>
    /// It initialises the real Steamworks API and gives real SteamID64s and real P2P over
    /// Valve's relay, so it needs no Steam Direct fee and no partner onboarding. It cannot ship:
    /// 480 is shared by every developer testing against it, so lobby matchmaking finds strangers.
    /// </remarks>
    public const uint TestAppId = 480;

    /// <summary>The project setting that names the Steam app id.</summary>
    /// <remarks>
    /// A setting rather than a constant because dev builds must keep using 480 after shipping
    /// builds get a real id — the shipping override is Godot's feature-tagged
    /// <c>dlo/network/steam_app_id.steam</c> on the export preset (E18-01), so it costs no C#.
    /// <para>
    /// The SDK also reads a <c>steam_appid.txt</c> beside the executable when Steam did not
    /// launch the game — the editor, a test run, a double-click. It is git-ignored and
    /// <b>must never be exported</b>: a shipped one overrides what Steam says the app is.
    /// </para>
    /// </remarks>
    public const string AppIdSetting = "dlo/network/steam_app_id";

    /// <summary>
    /// The app id this build initialises Steam with: <see cref="TestAppId"/> unless the export
    /// preset overrides it.
    /// </summary>
    /// <remarks>
    /// Read through <c>long</c> because Godot stores an unadorned integer setting as a 64-bit
    /// one, and an app id hand-edited into a preset is unadorned.
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
