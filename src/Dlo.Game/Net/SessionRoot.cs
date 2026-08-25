using Dlo.Domain;

using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// Owns the peer, the transport and — on the host only — the domain systems. One of the four
/// permitted autoloads (arch §6.2).
/// </summary>
/// <remarks>
/// <b><see cref="BeginSession"/> is the only place in the codebase that constructs a domain
/// system</b>, behind exactly one <c>Multiplayer.IsServer()</c> branch (arch §3.2). No
/// architecture test can catch a second one; the review checklist greps for it.
/// <para>
/// <b>Known divergence from arch §3.2, which puts that construction in <c>_Ready</c>.</b> It
/// cannot go there: this is an autoload, so <c>_Ready</c> runs at boot before any peer exists —
/// and with no peer set <c>Multiplayer.IsServer()</c> returns <b>true on every machine</b>
/// (asserted in L2, so this claim fails loudly if the engine changes it). The literal version
/// would build a <see cref="HostSession"/> on every client at startup, which is the exact thing
/// the seam prevents. Implementing it as stated needs a per-session node spawned after the peer
/// is assigned; arch §6.2 asks for an autoload, so the branch moved instead.
/// </para>
/// </remarks>
public partial class SessionRoot : Node
{
    private IGameTransport? _transport;
    private HostSession? _hostSession;
    private Carry.GrabDirector? _grabs;

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

    /// <summary>
    /// The grab authority (E1-04). A named child rather than a fifth autoload, which arch §6.2
    /// forbids — it needs an identical path on every peer, and being this node's child gives it one.
    /// </summary>
    /// <remarks>
    /// Built on every peer, host or client, because both halves of the protocol live on it: the
    /// host resolves and the client receives. Only the host ever creates a joint (arch §3.3).
    /// </remarks>
    public Carry.GrabDirector Grabs => _grabs ??= Build();

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

        // The registry goes with the session it belonged to; a stale one would answer the next
        // shift's grabs with the last shift's parcels.
        _grabs?.Parcels = null;

        _hostSession = null;
    }

    private Carry.GrabDirector Build()
    {
        var grabs = new Carry.GrabDirector { Name = Carry.GrabDirector.NodeName };
        AddChild(grabs);
        return grabs;
    }

    private void StartWith(MultiplayerPeer peer)
    {
        // The one place the lag harness is attached (E0-07). Returns the peer untouched unless a
        // development build asked for it, and throws if the flag survived into a release export.
        Multiplayer.MultiplayerPeer = LatencyPeer.WrapIfConfigured(peer);
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;

        // Before BeginSession, because an RPC that arrives for a node the tree does not have yet
        // is dropped with a warning nobody reads.
        _ = Grabs;
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

            _hostSession = new HostSession(
                new ShiftDirector(), new ShiftLedger(), random, new ParcelRegistry());
            _hostSession.PeerJoined(new PeerId(Multiplayer.GetUniqueId()));

            // The grab authority resolves capacity and the policy lock out of the registry, so
            // it has to be handed the host's one (arch §3.2 — passed in, never built there).
            Grabs.Parcels = _hostSession.Parcels;
        }
    }

    private void OnPeerConnected(long id) => _hostSession?.PeerJoined(new PeerId(id));

    private void OnPeerDisconnected(long id)
    {
        _hostSession?.PeerLeft(new PeerId(id));

        // Their held parcels drop rather than staying frozen in a disconnected hand (E12-04).
        _grabs?.ForgetCarrier(id);
    }
}
