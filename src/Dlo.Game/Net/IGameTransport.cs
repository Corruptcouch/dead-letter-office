using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// The anti-corruption boundary between the game and whatever moves its packets
/// (arch §3.5). Two methods, and deliberately no more.
/// </summary>
/// <remarks>
/// Everything above this interface speaks Godot's own <see cref="MultiplayerPeer"/>, so no ENet or
/// Steam type appears outside the two implementations and swapping transports is a drop-in rather
/// than a migration — which is what keeps E0-01's Steam risk inside this folder.
/// <para>
/// <b>What must not be added here:</b> lobbies, invites, friends, matchmaking, session state.
/// Those are E12, and a transport that knows about them is E12 leaking downward (E0-02).
/// </para>
/// </remarks>
public interface IGameTransport
{
    /// <summary>Starts listening and returns the host's peer.</summary>
    /// <param name="maxPeers">Clients allowed in addition to the host.</param>
    MultiplayerPeer CreateHost(int maxPeers);

    /// <summary>Connects to a host and returns the client's peer.</summary>
    /// <param name="address">
    /// <b>Opaque to callers, and defined by the implementation.</b> ENet reads it as a hostname or
    /// IP; Steam will read it as a SteamID64. Callers never build one — they hand back whatever the
    /// join flow produced (a typed address in development, an invite in a shipping build).
    /// </param>
    MultiplayerPeer CreateClient(string address);
}
