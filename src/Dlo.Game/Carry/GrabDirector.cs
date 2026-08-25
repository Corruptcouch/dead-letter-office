using System.Collections.Generic;
using System.Linq;

using Godot;

namespace Dlo.Game.Carry;

/// <summary>
/// Host-side grab authority (E1-04). It owns every physics joint in the build and is the only
/// thing that decides who is holding what.
/// </summary>
/// <remarks>
/// <b>A node under <c>SessionRoot</c>, not a fifth autoload</b> — arch §6.2 closes that list at
/// four and endorses exactly this shape. It needs an identical path on every peer, which being a
/// named child of an autoload gives it.
/// <para>
/// <b>The real joint exists only on the host</b> (arch §3.3). Clients keep a visual attachment and
/// a holder map, both derived; <see cref="Holders"/> is the decision the host broadcast, never a
/// per-frame stream.
/// </para>
/// </remarks>
// ponytail: loads are addressed over the wire by scene path.
// Ceiling: a path is not an identity - it breaks the moment a load is reparented or pooled, and it
// costs more bytes than the uint it will become. Pooling is E2-06 and would break this.
// Upgrade: E2-01's ParcelId, host-assigned, is the intended key; E2-04 already fixes the spawn
// payload shape. This is a Dictionary key change plus the RPC signatures.
public partial class GrabDirector : Node
{
    /// <summary>The node name this is registered under, on every peer.</summary>
    public const string NodeName = "GrabDirector";

    private readonly Dictionary<long, Node3D> _carriers = [];
    private readonly Dictionary<string, List<Hold>> _holds = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, List<long>> _holders = new(System.StringComparer.Ordinal);

    /// <summary>Raised on every peer when the host's decision arrives. Drives the IK, nothing else.</summary>
    public event System.Action? HoldsChanged;

    /// <summary>Raised on the peer whose grab was refused, with the reason.</summary>
    public event System.Action<string, GrabVerdict>? Denied;

    /// <summary>Who holds what, on every peer. The host is the only writer.</summary>
    public IReadOnlyDictionary<string, List<long>> Holders => _holders;

    /// <summary>
    /// Tells this director where a peer's carrier body is.
    /// </summary>
    /// <remarks>
    /// Nothing spawns a character per peer yet — E1 does not own that and no story has claimed it
    /// — so carriers announce themselves instead of being discovered. That keeps this class out of
    /// a spawning scheme that does not exist.
    /// </remarks>
    public void RegisterCarrier(long peer, Node3D carrier)
    {
        System.ArgumentNullException.ThrowIfNull(carrier);
        _carriers[peer] = carrier;
    }

    /// <summary>Forgets a peer's carrier and drops whatever it was holding.</summary>
    public void ForgetCarrier(long peer)
    {
        _carriers.Remove(peer);

        if (!Multiplayer.IsServer())
        {
            return;
        }

        // A held load must drop rather than stay frozen in a disconnected hand (E12-04).
        var mine = _holds.Where(e => e.Value.Any(h => h.Peer == peer)).Select(e => e.Key).ToArray();
        foreach (var path in mine)
        {
            ReleaseFor(peer, path);
        }
    }

    /// <summary>Who is holding the load at <paramref name="loadPath"/>, on any peer.</summary>
    public IReadOnlyList<long> HoldersOf(string loadPath) =>
        _holders.TryGetValue(loadPath, out var list) ? list : [];

    /// <summary>The load <paramref name="peer"/> is holding, or <c>null</c>.</summary>
    public string? HeldBy(long peer) =>
        _holders.FirstOrDefault(e => e.Value.Contains(peer)).Key;

    /// <summary>
    /// Asks the host for a grip. Call this on the grabbing peer; it routes to the host itself.
    /// </summary>
    public void Grab(Carryable load)
    {
        System.ArgumentNullException.ThrowIfNull(load);
        var path = load.GetPath().ToString();

        if (Multiplayer.IsServer())
        {
            GrabFor(Multiplayer.GetUniqueId(), path);
        }
        else
        {
            RpcId(1, MethodName.RequestGrab, path);
        }
    }

    /// <summary>Asks the host to let go. Always available (E1-07), never gated on a state machine.</summary>
    public void Release(Carryable load)
    {
        System.ArgumentNullException.ThrowIfNull(load);
        var path = load.GetPath().ToString();

        if (Multiplayer.IsServer())
        {
            ReleaseFor(Multiplayer.GetUniqueId(), path);
        }
        else
        {
            RpcId(1, MethodName.RequestRelease, path);
        }
    }

    /// <summary>Asks the host to let go and shove, along <paramref name="aim"/> (E1-07).</summary>
    public void Throw(Carryable load, Vector3 aim)
    {
        System.ArgumentNullException.ThrowIfNull(load);
        var path = load.GetPath().ToString();

        if (Multiplayer.IsServer())
        {
            Hurl(path, Multiplayer.GetUniqueId(), aim);
        }
        else
        {
            RpcId(1, MethodName.RequestThrow, path, aim);
        }
    }

    /// <summary>A client asking for a grip. The decision is made here and nowhere else.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestGrab(string loadPath) =>
        GrabFor(Multiplayer.GetRemoteSenderId(), loadPath);

    /// <summary>A client letting go.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestRelease(string loadPath) =>
        ReleaseFor(Multiplayer.GetRemoteSenderId(), loadPath);

    /// <summary>A client throwing.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestThrow(string loadPath, Vector3 aim) =>
        Hurl(loadPath, Multiplayer.GetRemoteSenderId(), aim);

    /// <summary>
    /// The host's decision, broadcast. <c>Reliable</c> because it is a decision, not a stream
    /// (E1-04) — a dropped grab would leave one peer's hands permanently wrong.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void GrabResolved(string loadPath, long holder)
    {
        if (!_holders.TryGetValue(loadPath, out var list))
        {
            list = [];
            _holders[loadPath] = list;
        }

        if (!list.Contains(holder))
        {
            list.Add(holder);
        }

        HoldsChanged?.Invoke();
    }

    /// <summary>The host's decision that a carrier no longer holds this load.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReleaseResolved(string loadPath, long holder)
    {
        if (_holders.TryGetValue(loadPath, out var list) && list.Remove(holder) && list.Count == 0)
        {
            _holders.Remove(loadPath);
        }

        HoldsChanged?.Invoke();
    }

    /// <summary>
    /// A refusal, sent only to the peer that asked. Nobody else's business, and broadcasting it
    /// would spend bandwidth telling three peers about a grab that did not happen.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void GrabRefused(string loadPath, int verdict) =>
        Denied?.Invoke(loadPath, (GrabVerdict)verdict);

    /// <summary>
    /// Resolves a grab for <paramref name="peer"/>. Host only; a no-op anywhere else.
    /// </summary>
    /// <remarks>
    /// Public rather than private because the suite needs to stage a second carrier, and one
    /// process cannot produce a second real peer (standards §6: if a test needs it, make it public
    /// and say why). It is also the honest shape — the host resolves for a peer, and whether that
    /// peer asked over a socket or is the host itself is not this method's business.
    /// </remarks>
    public void GrabFor(long peer, string loadPath)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        var load = GetNodeOrNull<Carryable>(loadPath);
        if (load is null)
        {
            Refuse(peer, loadPath, GrabVerdict.NoSlotFree);
            return;
        }

        if (!_holds.TryGetValue(loadPath, out var held))
        {
            held = [];
            _holds[loadPath] = held;
        }

        var slot = held.Count;
        var distance = _carriers.TryGetValue(peer, out var carrier)
            ? carrier.GlobalPosition.DistanceTo(load.GlobalGrip(slot))
            : float.MaxValue;

        var verdict = GrabRules.Evaluate(
            distance,
            held.Count,
            load.CarriersRequired,
            load.Locked,
            alreadyHolding: held.Exists(h => h.Peer == peer));

        if (verdict != GrabVerdict.Granted)
        {
            Refuse(peer, loadPath, verdict);
            return;
        }

        // The one place a physics joint is created in the whole build (arch §3.3).
        var hand = Anchor(carrier!);
        held.Add(new Hold(peer, slot, hand));
        Crew(load, held);

        Rpc(MethodName.GrabResolved, loadPath, peer);
    }

    private void Refuse(long peer, string loadPath, GrabVerdict verdict)
    {
        if (peer == Multiplayer.GetUniqueId())
        {
            GrabRefused(loadPath, (int)verdict);
        }
        else
        {
            RpcId(peer, MethodName.GrabRefused, loadPath, (int)verdict);
        }
    }

    /// <summary>Releases <paramref name="peer"/>'s grip. Host only; a no-op anywhere else.</summary>
    /// <remarks>Public for the same reason as <see cref="GrabFor"/>.</remarks>
    public void ReleaseFor(long peer, string loadPath)
    {
        if (!Multiplayer.IsServer() || !_holds.TryGetValue(loadPath, out var held))
        {
            return;
        }

        var mine = held.Find(h => h.Peer == peer);
        if (mine is null)
        {
            return;
        }

        held.Remove(mine);

        // Freed here rather than inside Crew, because Crew only walks the holds that REMAIN - this
        // one is already out of the list, and leaving it would keep a spring on the load forever.
        mine.Joint?.QueueFree();
        mine.Joint = null;

        var load = GetNodeOrNull<Carryable>(loadPath);
        if (load is not null)
        {
            // Every remaining grip is rebuilt, because a 6DOF spring's equilibrium is the offset it
            // was BUILT with - so a joint left over from a two-person carry would keep pulling
            // toward a hand that is no longer there. Rebuilding also thaws it, which is what makes
            // a two-person load whose second carrier walks off FALL rather than hang from one hand
            // (E1-08). The remaining spring sags away at drop speed rather than launching.
            Crew(load, held);
        }

        if (held.Count == 0)
        {
            _holds.Remove(loadPath);
        }

        Rpc(MethodName.ReleaseResolved, loadPath, peer);
    }

    private void Hurl(string loadPath, long peer, Vector3 aim)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        var load = GetNodeOrNull<Carryable>(loadPath);
        ReleaseFor(peer, loadPath);

        // Impulse, not velocity: the same shove moves a heavy parcel less, so a bad projectile is
        // a consequence of its mass rather than a special case (E1-07).
        load?.ApplyCentralImpulse(aim);
    }

    private static AnimatableBody3D Anchor(Node3D carrier) =>
        carrier.GetNodeOrNull<AnimatableBody3D>(PlayerCharacter.HandAnchorName)
        ?? throw new System.InvalidOperationException(
            $"Carrier '{carrier.Name}' has no '{PlayerCharacter.HandAnchorName}'. A grip needs a "
            + "kinematic anchor to joint to (E1-01 measured the carry against one).");

    /// <summary>
    /// Rebuilds every grip on <paramref name="load"/> for the carriers currently holding it, and
    /// lifts it into the carry pose once its last slot is filled.
    /// </summary>
    /// <remarks>
    /// <b>The lift is explicit, and it has to be.</b> A <c>Generic6DofJoint3D</c> linear spring
    /// drives the relative offset its two bodies had <i>at attach time</i> toward equilibrium — so
    /// a spring built while the box is on the floor holds it on the floor, however stiff it is.
    /// E1-01 measured a box already in the air and therefore never met this: the finding is about
    /// holding, and lifting is a separate step. Measured 2026-08-25.
    /// <para>
    /// <b>An under-crewed load is frozen rather than weakly sprung</b>, and that is measured too. A
    /// weaker grip cannot refuse a lift, because a linear spring's force grows without bound with
    /// stretch — halving the stiffness only buys more sag, and a lone carrier still lifted a
    /// two-person 50 kg box to within 21 cm of their hand. Jolt offers no force cap to fix it
    /// (<c>PARAM_LINEAR_SPRING</c> has stiffness, damping and equilibrium and nothing else, and
    /// E1-01 already found <c>impulse_clamp</c> unimplemented). So it is a flag, and it says so —
    /// and the co-op fiction falls out of it for free: the box comes up on the second grip.
    /// </para>
    /// </remarks>
    // ponytail: a granted grab snaps the load into the carry pose in one frame.
    // Ceiling: it is a visible teleport of up to the carrier's reach - measured at ~1.9 m in the L3
    // harness, where the carrier stands 1 m away. Every peer sees it, not just the grabber, and it
    // is the one thing in the carry that does not look physical.
    // Upgrade: move the HAND to the load's grip on grant, joint there, then animate the hand back to
    // the carry pose - the spring then drags the load up and the lift is simulated rather than
    // asserted. It costs an anchor animation and nothing else; the joint recipe does not change.
    // Deliberately not done before Gate 0 (E1-10), because the feel session is what should decide
    // whether a snap reads as awkward or as broken, and that is exactly the word it collects.
    private void Crew(Carryable load, List<Hold> held)
    {
        foreach (var hold in held)
        {
            hold.Joint?.QueueFree();
            hold.Joint = null;
        }

        if (held.Count == 0)
        {
            load.Freeze = false;
            return;
        }

        if (held.Count < System.Math.Max(1, load.CarriersRequired))
        {
            // Held, but it does not come. They are not refused - the box simply will not move.
            load.Freeze = true;
            return;
        }

        load.Freeze = false;
        load.GlobalTransform = CarryPose(load, held);

        // Every grip is E1-01's reference configuration exactly: one spring per carrier at
        // 100 x mass. The spike measured a two-person carry as TWO such joints, so dividing the
        // stiffness between carriers would put the build outside the envelope its own regression
        // test guards.
        foreach (var hold in held)
        {
            hold.Joint = GripSpring.Attach(
                this, hold.Hand, load, load.GlobalGrip(hold.Slot));
        }
    }

    /// <summary>Where a fully crewed load sits: in its carriers' hands, squared up between them.</summary>
    private static Transform3D CarryPose(Carryable load, List<Hold> held)
    {
        if (held.Count == 1)
        {
            return new Transform3D(load.GlobalBasis, held[0].Hand.GlobalPosition);
        }

        var first = held[0].Hand.GlobalPosition;
        var last = held[^1].Hand.GlobalPosition;
        var across = last - first;

        if (across.LengthSquared() < 0.0001f)
        {
            return new Transform3D(load.GlobalBasis, (first + last) * 0.5f);
        }

        // Local X runs carrier-to-carrier, so the grip slots land in the hands holding them.
        var x = across.Normalized();
        var up = Vector3.Up;
        var z = x.Cross(up).Normalized();

        return new Transform3D(new Basis(x, z.Cross(x).Normalized(), z), (first + last) * 0.5f);
    }

    private sealed record Hold(long Peer, int Slot, AnimatableBody3D Hand)
    {
        /// <summary>This carrier's spring, or <c>null</c> while the load is under-crewed.</summary>
        public Generic6DofJoint3D? Joint { get; set; }
    }
}
