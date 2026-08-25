using Dlo.Domain;

using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// Owns the peer, the transport and — on the host only — the domain systems. One of the four
/// permitted autoloads (arch §6.2).
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="BeginSession"/> is the only place in the codebase that constructs a domain
/// system</b>, and it does so behind exactly one <c>Multiplayer.IsServer()</c> branch
/// (arch §3.2). No architecture test can catch a second one; the review checklist greps for it.
/// </para>
/// <para>
/// <b>Known divergence from arch §3.2, which puts that construction in <c>_Ready</c>.</b>
/// It cannot go there. This is an autoload, so <c>_Ready</c> runs once at boot, before anyone
/// has hosted or joined and before a <c>MultiplayerPeer</c> exists — and with no peer set,
/// <c>Multiplayer.IsServer()</c> returns <b>true on every machine</b> (asserted in the L2
/// suite, so this claim fails loudly if the engine ever changes it). Following the snippet
/// literally would therefore build a <see cref="HostSession"/> on every client at startup:
/// precisely the client-side domain system the seam exists to prevent, created by the code
/// meant to prevent it. So the branch moved to the point where a peer is known.
/// What the stated version would need: a per-session node spawned after the peer is assigned,
/// rather than an autoload. Arch §6.2 asks for an autoload, so the branch moved instead.
/// </para>
/// </remarks>
public partial class SessionRoot : Node
{
    private IGameTransport? _transport;
    private HostSession? _hostSession;

    /// <summary>
    /// The transport used for the next <see cref="Host"/> or <see cref="Join"/>. Resolved from
    /// configuration on first use, and settable so tests can pin ENet without touching project
    /// settings.
    /// </summary>
    public IGameTransport Transport
    {
        get => _transport ??= GameTransport.ForCurrentBuild();
        set => _transport = value;
    }

    /// <summary>The live host session, or <c>null</c> on a client and between sessions.</summary>
    public HostSession? Session => _hostSession;

    /// <summary>Whether a session is currently running on this peer.</summary>
    public bool IsInSession => Multiplayer.MultiplayerPeer is not null
        && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer;

    /// <summary>Starts hosting and begins the session.</summary>
    /// <param name="maxPeers">Clients allowed in addition to the host.</param>
    public void Host(int maxPeers)
    {
        StartWith(Transport.CreateHost(maxPeers));
    }

    /// <summary>Connects to a host and begins the session.</summary>
    /// <param name="address">Transport-defined; see <see cref="IGameTransport.CreateClient"/>.</param>
    public void Join(string address)
    {
        StartWith(Transport.CreateClient(address));
    }

    /// <summary>
    /// Ends the session on this peer: closes the transport and drops the domain systems.
    /// </summary>
    /// <remarks>
    /// Safe to call when no session is running, and safe to call twice. Teardown that throws
    /// when it is already torn down turns every error path into two errors (standards §10:
    /// invalid state recovers rather than disabling the node forever).
    /// </remarks>
    public void Leave()
    {
        if (Multiplayer.MultiplayerPeer is { } peer && peer is not OfflineMultiplayerPeer)
        {
            Multiplayer.PeerConnected -= OnPeerConnected;
            Multiplayer.PeerDisconnected -= OnPeerDisconnected;
            peer.Close();
        }

        // Godot substitutes an OfflineMultiplayerPeer rather than accepting null here, so
        // "no session" is that type and not the absence of one - see IsInSession.
        Multiplayer.MultiplayerPeer = null;
        _hostSession = null;
    }

    private void StartWith(MultiplayerPeer peer)
    {
        Multiplayer.MultiplayerPeer = peer;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        BeginSession();
    }

    private void BeginSession()
    {
        // THE construction site (arch §3.2). One place, one branch. Clients construct nothing,
        // so there is no client-side ShiftDirector to drift out of step with the host's.
        if (Multiplayer.IsServer())
        {
            var random = new SeededRandom(unchecked((int)Time.GetTicksMsec()));

            // The seed is reported so a bug report can carry it (arch §4.2). Without this line
            // the injected IRandom buys nothing - a seed nobody can read is not reproducible.
            GD.Print($"Session seed: {random.Seed}");

            _hostSession = new HostSession(new ShiftDirector(), new ShiftLedger(), random);
            _hostSession.PeerJoined(new PeerId(Multiplayer.GetUniqueId()));
        }
    }

    private void OnPeerConnected(long id) => _hostSession?.PeerJoined(new PeerId(id));

    private void OnPeerDisconnected(long id) => _hostSession?.PeerLeft(new PeerId(id));
}
