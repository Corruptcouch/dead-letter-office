using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// The development, test and L3 transport (arch §3.5). Needs no Steam client, so it is what
/// CI and <c>Dlo.Net.Tests</c> run against forever, whatever E0-01 decides about Steam.
/// </summary>
public sealed class EnetTransport : IGameTransport
{
    /// <summary>
    /// The port every development session uses. Fixed rather than configurable: the L3
    /// harness runs a host and three clients on one machine, and they all want the same
    /// number. ENet is UDP, so a rerun does not wait out a TIME_WAIT.
    /// </summary>
    public const int Port = 27377;

    /// <inheritdoc/>
    public MultiplayerPeer CreateHost(int maxPeers)
    {
        var peer = new ENetMultiplayerPeer();
        Fail.IfNotOk(peer.CreateServer(Port, maxPeers), $"host on port {Port}");
        return peer;
    }

    /// <inheritdoc/>
    public MultiplayerPeer CreateClient(string address)
    {
        var peer = new ENetMultiplayerPeer();
        Fail.IfNotOk(peer.CreateClient(address, Port), $"connect to {address}:{Port}");
        return peer;
    }
}
