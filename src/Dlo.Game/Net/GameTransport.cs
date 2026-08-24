using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// Picks the transport from configuration (E0-03, arch §3.5): <b>ENet is the development and
/// test default; Steam is the shipping default.</b>
/// </summary>
/// <remarks>
/// The default here is ENet, and the shipping build overrides it with Godot's own
/// feature-tagged project settings — <c>dlo/network/transport.steam</c> alongside a
/// <c>steam</c> feature on the export preset (E18-01). That is one line of config rather than
/// a build-type branch in code, which is why this class has no idea what a build type is.
/// </remarks>
public static class GameTransport
{
    /// <summary>The project setting that names the transport.</summary>
    public const string Setting = "dlo/network/transport";

    /// <summary>Value selecting <see cref="EnetTransport"/>.</summary>
    public const string Enet = "enet";

    /// <summary>Value selecting <see cref="SteamTransport"/>.</summary>
    public const string Steam = "steam";

    /// <summary>
    /// Builds the transport this build is configured for.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The setting names a transport that does not exist. This throws rather than defaulting,
    /// because a typo silently falling back to ENet is how a shipping build loses Steam
    /// without anyone noticing (see <see cref="SteamTransport"/>).
    /// </exception>
    public static IGameTransport ForCurrentBuild() =>
        ProjectSettings.GetSetting(Setting, Enet).AsString() switch
        {
            Enet => new EnetTransport(),
            Steam => new SteamTransport(),
            var other => throw new System.InvalidOperationException(
                $"Project setting '{Setting}' is '{other}'; expected '{Enet}' or '{Steam}'."),
        };
}
