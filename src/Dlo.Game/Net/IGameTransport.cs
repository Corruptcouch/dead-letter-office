using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// The anti-corruption boundary between the game and whatever moves its packets
/// (arch §3.5). Two methods, and deliberately no more.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this buys.</b> Everything above this interface speaks Godot's own
/// <see cref="MultiplayerPeer"/>. No ENet type and no Steam type appears in any file outside
/// the two implementations, so swapping transports is a drop-in rather than a migration —
/// which is the entire reason E0-01's Steam risk cannot spread past this folder.
/// </para>
/// <para>
/// <b>What must not be added here.</b> Lobbies, invites, friends, matchmaking, session state.
/// Those are E12, and a transport that knows about them is E12 leaking downward (E0-02). If
/// something Steam-shaped does not fit behind these two methods, that is a design
/// conversation, not a third method.
/// </para>
/// </remarks>
public interface IGameTransport
{
    /// <summary>Starts listening and returns the host's peer.</summary>
    /// <param name="maxPeers">Clients allowed in addition to the host.</param>
    MultiplayerPeer CreateHost(int maxPeers);

    /// <summary>Connects to a host and returns the client's peer.</summary>
    /// <param name="address">
    /// <b>Opaque to callers, and defined by the implementation.</b> ENet reads it as a
    /// hostname or IP; Steam will read it as a SteamID64. Callers never build one — they get
    /// it from whatever join flow produced it (a typed address in development, an invite in a
    /// shipping build) and hand it straight back. See the gap recorded against this parameter.
    /// </param>
    MultiplayerPeer CreateClient(string address);
}
