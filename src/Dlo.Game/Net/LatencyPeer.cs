using System.Collections.Generic;

using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// A development-only decorator that holds incoming packets back, so a four-peer session on
/// one desk behaves like one over a real connection (E0-07, arch §3.5).
/// </summary>
/// <remarks>
/// <b>Required infrastructure, not a nicety.</b> Vision §15 asks whether a shared physical object
/// still feels believable <i>over real internet</i>; without a lag harness the MVP answers an
/// easier question. E19-03 is explicit that it supplements four real connections, never substitutes.
/// <para>
/// <b>It delays what arrives, not what leaves</b>, so a round trip costs twice
/// <see cref="DelaySetting"/> — the figure people actually quote.
/// </para>
/// <para>
/// <b>Reordering is allowed only where the network would really do it.</b> Jitter is per packet,
/// but <c>Reliable</c> and <c>UnreliableOrdered</c> release order is preserved: ENet has already
/// guaranteed it, so re-breaking it here would simulate a bug that cannot happen — and would tear
/// scene replication apart doing it. Plain <c>Unreliable</c> packets may overtake each other.
/// </para>
/// </remarks>
// ponytail: LatencyPeer delays and reorders packets in a decorator over MultiplayerPeer.
// Ceiling: a fixed delay plus jitter, no bandwidth cap or congestion modelling.
// Upgrade: only if a real bug needs it - clumsy/netem on the host is closer to truth.
public partial class LatencyPeer : MultiplayerPeerExtension
{
    /// <summary>One-way delay in milliseconds. A round trip costs twice this.</summary>
    public const string DelaySetting = "dlo/network/latency_ms";

    /// <summary>Random variation added to each packet's delay, in milliseconds.</summary>
    public const string JitterSetting = "dlo/network/latency_jitter_ms";

    /// <summary>Turns the decorator on. Off by default, and refused outside a debug build.</summary>
    public const string EnabledSetting = "dlo/network/latency_enabled";

    private readonly MultiplayerPeer _inner;
    private readonly List<Held> _queue = [];
    private readonly RandomNumberGenerator _jitterSource = new();
    private readonly int _delayMs;
    private readonly int _jitterMs;

    private ulong _lastOrderedRelease;

    private LatencyPeer(MultiplayerPeer inner, int delayMs, int jitterMs)
    {
        _inner = inner;
        _delayMs = delayMs;
        _jitterMs = jitterMs;

        // Re-emitted rather than forwarded: SceneMultiplayer connects to the peer it was handed,
        // which is this object. Without these two lines nobody is ever told a peer joined, and
        // the session simply never starts - with no error to explain why.
        _inner.PeerConnected += id => EmitSignal(SignalName.PeerConnected, id);
        _inner.PeerDisconnected += id => EmitSignal(SignalName.PeerDisconnected, id);
    }

    /// <summary>
    /// Wraps <paramref name="peer"/> if this build is configured for it, and returns it
    /// untouched if not.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The flag is set in a build that is not a debug build.
    /// </exception>
    /// <remarks>
    /// <b>The guard is an assertion, not a convention</b> (E0-07): a shipping build that silently
    /// added 150 ms to every packet is indistinguishable from a bad connection, and nobody would
    /// think to look for a project setting. <see cref="OS.IsDebugBuild"/> is false only in a
    /// release export, so the check costs nothing in development and cannot be talked around.
    /// </remarks>
    public static MultiplayerPeer WrapIfConfigured(MultiplayerPeer peer)
    {
        if (!ProjectSettings.GetSetting(EnabledSetting, false).AsBool())
        {
            return peer;
        }

        if (Refuse(enabled: true, isDebugBuild: OS.IsDebugBuild()))
        {
            throw new System.InvalidOperationException(
                $"Project setting '{EnabledSetting}' is on in a release build. LatencyPeer is a "
                + "development tool (E0-07, arch §3.5) and shipping it would make every player "
                + "think they had a bad connection. Remove the setting from the export preset.");
        }

        var delay = ProjectSettings.GetSetting(DelaySetting, 0).AsInt32();
        var jitter = ProjectSettings.GetSetting(JitterSetting, 0).AsInt32();
        return new LatencyPeer(peer, delay, jitter);
    }

    /// <summary>
    /// Whether this build must refuse to wrap. Separate from <see cref="WrapIfConfigured"/> so the
    /// rule can be asserted directly — the case that matters only exists inside a release export,
    /// where no test can stand.
    /// </summary>
    public static bool Refuse(bool enabled, bool isDebugBuild) => enabled && !isDebugBuild;

    /// <summary>Wraps <paramref name="peer"/> with explicit figures, for tests.</summary>
    public static LatencyPeer Wrap(MultiplayerPeer peer, int delayMs, int jitterMs) =>
        new(peer, delayMs, jitterMs);

    /// <summary>How many packets are being held back but not yet delivered.</summary>
    public int InFlight => _queue.Count;

    /// <inheritdoc/>
    public override void _Poll()
    {
        _inner.Poll();

        while (_inner.GetAvailablePacketCount() > 0)
        {
            // Read the metadata BEFORE taking the packet. These accessors describe the next
            // packet on the inner peer, so calling GetPacket() first would attribute every
            // packet to whichever one came after it.
            var from = _inner.GetPacketPeer();
            var channel = _inner.GetPacketChannel();
            var mode = _inner.GetPacketMode();
            var bytes = _inner.GetPacket();

            var release = Time.GetTicksMsec() + (ulong)_delayMs + Jitter();

            if (mode != TransferModeEnum.Unreliable)
            {
                // Ordered traffic keeps its order: jitter may stretch the gap between two
                // packets but may never let the second overtake the first.
                release = System.Math.Max(release, _lastOrderedRelease);
                _lastOrderedRelease = release;
            }

            _queue.Add(new Held(bytes, from, channel, mode, release));
        }
    }

    /// <inheritdoc/>
    public override int _GetAvailablePacketCount()
    {
        var now = Time.GetTicksMsec();
        var ready = 0;
        foreach (var held in _queue)
        {
            if (held.ReleaseAt <= now)
            {
                ready++;
            }
        }

        return ready;
    }

    /// <inheritdoc/>
    public override byte[] _GetPacketScript()
    {
        var now = Time.GetTicksMsec();
        for (var i = 0; i < _queue.Count; i++)
        {
            if (_queue[i].ReleaseAt <= now)
            {
                var held = _queue[i];
                _queue.RemoveAt(i);
                _delivered = held;
                return held.Bytes;
            }
        }

        return [];
    }

    /// <inheritdoc/>
    public override Error _PutPacketScript(byte[] pBuffer) => _inner.PutPacket(pBuffer);

    /// <inheritdoc/>
    public override int _GetPacketPeer() => Ready()?.From ?? _delivered.From;

    /// <inheritdoc/>
    public override int _GetPacketChannel() => Ready()?.Channel ?? _delivered.Channel;

    /// <inheritdoc/>
    public override TransferModeEnum _GetPacketMode() => Ready()?.Mode ?? _delivered.Mode;

    /// <inheritdoc/>
    public override void _SetTargetPeer(int pPeer) => _inner.SetTargetPeer(pPeer);

    /// <inheritdoc/>
    public override void _SetTransferMode(TransferModeEnum pMode) => _inner.TransferMode = pMode;

    /// <inheritdoc/>
    public override TransferModeEnum _GetTransferMode() => _inner.TransferMode;

    /// <inheritdoc/>
    public override void _SetTransferChannel(int pChannel) => _inner.TransferChannel = pChannel;

    /// <inheritdoc/>
    public override int _GetTransferChannel() => _inner.TransferChannel;

    /// <inheritdoc/>
    /// <remarks>
    /// A constant, because <see cref="MultiplayerPeer"/> does not expose the inner peer's
    /// answer — <c>_get_max_packet_size</c> is a virtual an implementation provides, not a
    /// method a caller can ask. This is ENet's own figure, and the decorator never splits or
    /// buffers by size, so reporting anything smaller would invent a limit that is not there.
    /// </remarks>
    public override int _GetMaxPacketSize() => 1 << 24;

    /// <inheritdoc/>
    public override int _GetUniqueId() => _inner.GetUniqueId();

    /// <inheritdoc/>
    /// <remarks>
    /// Peer id 1 is the server — that is the definition rather than a convention, and it is
    /// how Godot's own peers answer this. <c>MultiplayerPeer</c> exposes no <c>IsServer</c> to
    /// forward to.
    /// </remarks>
    public override bool _IsServer() => _inner.GetUniqueId() == 1;

    /// <inheritdoc/>
    public override bool _IsServerRelaySupported() => _inner.IsServerRelaySupported();

    /// <inheritdoc/>
    public override ConnectionStatus _GetConnectionStatus() => _inner.GetConnectionStatus();

    /// <inheritdoc/>
    public override bool _IsRefusingNewConnections() => _inner.RefuseNewConnections;

    /// <inheritdoc/>
    public override void _SetRefuseNewConnections(bool pEnable) =>
        _inner.RefuseNewConnections = pEnable;

    /// <inheritdoc/>
    public override void _DisconnectPeer(int pPeer, bool pForce) =>
        _inner.DisconnectPeer(pPeer, pForce);

    /// <inheritdoc/>
    public override void _Close()
    {
        _queue.Clear();
        _inner.Close();
    }

    private Held? Ready()
    {
        var now = Time.GetTicksMsec();
        foreach (var held in _queue)
        {
            if (held.ReleaseAt <= now)
            {
                return held;
            }
        }

        return null;
    }

    private ulong Jitter() =>
        _jitterMs <= 0 ? 0UL : (ulong)_jitterSource.RandiRange(0, _jitterMs);

    /// <summary>The packet handed over most recently, for the accessors that describe it.</summary>
    /// <remarks>
    /// Godot's accessors describe "the next packet" before <c>get_packet</c> and "the packet you
    /// just took" after it, and SceneMultiplayer reads them in both orders. Keeping the last one
    /// delivered means the second reading is answered correctly instead of falling through to
    /// whatever happens to be at the head of the queue.
    /// </remarks>
    private Held _delivered = new([], 1, 0, TransferModeEnum.Reliable, 0);

    private readonly record struct Held(
        byte[] Bytes, int From, int Channel, TransferModeEnum Mode, ulong ReleaseAt);
}
