using Dlo.Domain;

using Godot;

namespace Dlo.Game.Carry;

/// <summary>
/// The body of a parcel: a mass, a shape, and one grip point per carrier it needs. A view of a
/// <see cref="ParcelRecord"/> and never the owner of one (arch §5.1).
/// </summary>
/// <remarks>
/// Everything below is either derived from <see cref="Size"/> or arrived in
/// <see cref="ParcelSpawnArgs"/>, so a recycled or respawned node can hold nothing that outlives
/// it — which is what E2-02 and E2-06 depend on. Policy state is deliberately absent: a lock is
/// the host's to know (arch §5.3).
/// </remarks>
public partial class Carryable : RigidBody3D
{
    /// <summary>The group every parcel body joins, so a conveyor can find one (E4-01).</summary>
    public const string Group = "parcels";

    /// <summary>
    /// Frames the final transform of <see cref="Net.ReplicationClass.Sleeping"/> is given to leave.
    /// </summary>
    /// <remarks>
    /// Two, not one: a synchronizer sends during the multiplayer poll, and nothing here knows
    /// whether that poll runs before or after this node's callback. One frame would land the send
    /// half the time, which is the worst of the three available numbers.
    /// </remarks>
    private const int FlushFrames = 2;

    private int _flush;

    /// <summary>
    /// Which parcel this node is showing. <c>default</c> means it is not a parcel at all — a
    /// prop, or a test fixture — and the host treats it as unlocked and one-person.
    /// </summary>
    public ParcelId Id { get; set; }

    /// <summary>Which authored kind of parcel this is (arch §4.1). From spawn args.</summary>
    public byte Archetype { get; set; }

    /// <summary>How big it is. From spawn args, and what decides
    /// <see cref="CarriersRequired"/>.</summary>
    public byte Size { get; set; }

    /// <summary>How battered it looks. From spawn args, so every peer sees the same dents.</summary>
    public byte Condition { get; set; }

    /// <summary>
    /// How many carriers this load needs at once. Two means one player cannot lift it (E1-08).
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="Size"/> through <see cref="ParcelRecord.CarriersRequiredFor"/>,
    /// which is the host's own rule — so this is a view of a domain fact rather than a second
    /// copy of one, and a client reaches the same answer with nothing extra on the wire.
    /// </remarks>
    public int CarriersRequired => ParcelRecord.CarriersRequiredFor(Size);

    /// <summary>
    /// Half the distance between carrier slots, along the body's local X. Only meaningful when
    /// <see cref="CarriersRequired"/> is above one.
    /// </summary>
    [Export]
    public float GripHalfWidth { get; set; } = 0.5f;

    /// <summary>
    /// Arch §3.4's rail tuple: <c>(beltId, distanceAlong, lane)</c>, or zero when this parcel is
    /// not riding anything.
    /// </summary>
    /// <remarks>
    /// <b>Written once, when the parcel joins a belt, and never again while it rides.</b> The
    /// running distance lives on the conveyor, not here, because this property is watched: moving
    /// it every frame would turn the cheapest replication class into the most expensive one.
    /// <para>
    /// Twelve bytes against arch §3.4's "~6 bytes", and it is sent once per parcel rather than
    /// per frame, so it does not touch the streaming budget its arithmetic is about. Packing
    /// belt, lane and decimetres into one int is the upgrade if E4-10 ever finds it matters.
    /// </para>
    /// </remarks>
    [Export]
    public Vector3 Rail { get; set; }

    /// <summary>
    /// This parcel's synchronizer, which <see cref="Net.Replication.Apply"/> reconfigures as the
    /// parcel changes class. Built here so a parcel always has exactly one.
    /// </summary>
    public MultiplayerSynchronizer Synchronizer { get; private set; } = null!;

    /// <summary>
    /// Which of arch §3.4's three classes this parcel is replicating in (E2-05).
    /// </summary>
    /// <remarks>
    /// <b>A decision about what to send, so only the authority makes it.</b> A peer that does not
    /// own the body keeps the one configuration that can receive all three and reports
    /// <see cref="Net.ReplicationClass.Dynamic"/> whatever the host is doing.
    /// </remarks>
    public Net.ReplicationClass Class { get; private set; } = Net.ReplicationClass.Dynamic;

    /// <summary>
    /// The node a client may move to fake holding this (arch §3.3). Meshes hang under it.
    /// </summary>
    /// <remarks>
    /// <b>The point of it is that it is not the body.</b> A client that predicts a grab by moving
    /// the body writes the same transform replication owns, so the parcel flips between the
    /// predicted hand and the authority's position on every packet — about a metre, several times a
    /// second, measured across the L3 harness. Offsetting a child instead is what "visual-only"
    /// actually means: the body stays exactly where the host says it is, and only the picture is
    /// early. Nothing here is ever replicated.
    /// </remarks>
    public Node3D Visual { get; private set; } = null!;

    /// <summary>Where the rail tuple lives, for a synchronizer's replication config.</summary>
    public static NodePath RailProperty => new($".:{PropertyName.Rail}");

    /// <summary>Where the transform lives, for a synchronizer's replication config.</summary>
    public static NodePath TransformProperty => new($".:{PropertyName.Position}");

    /// <inheritdoc/>
    public override void _Ready()
    {
        Visual = GetNodeOrNull<Node3D>("Visual") ?? Attach();
        Synchronizer = GetNodeOrNull<MultiplayerSynchronizer>("Sync") ?? Sync();

        // Dynamic until something says otherwise: a loose parcel is the class that has to be
        // right by default, because the cheap classes are only correct when they are earned. The
        // configuration is applied even to a synchronizer somebody else built, because every peer
        // has to agree on the property list or a sync packet decodes against the wrong one.
        Net.Replication.Apply(Synchronizer, Net.ReplicationClass.Dynamic, TransformProperty, RailProperty);

        // A conveyor finds the parcels it should be carrying by looking here (E4-01). Joining
        // in _Ready rather than on entry means a parcel spawned already railed - which is what
        // a client gets - is found without a second message.
        AddToGroup(Group);
    }

    // ponytail: every parcel polls its own class once per frame.
    // Ceiling: one enum compare and two property reads per parcel per frame - at arch §8's 200
    // live records that is noise beside the conveyor's own per-frame scan, and it is measured
    // by E2-10 rather than assumed.
    // Upgrade: drive it from the two things that actually change - the Rail setter and
    // RigidBody3D's sleeping_state_changed signal - once a profile says the poll is worth
    // removing. The flush below would still need a frame to land on.
    /// <inheritdoc/>
    public override void _Process(double delta) => Reclassify();

    /// <summary>
    /// Puts this parcel in the class its own state calls for, promoting and demoting in place
    /// (E2-05, arch §3.4). Cheap, idempotent, and safe to call from anything that changes the
    /// rail tuple rather than waiting for the next frame.
    /// </summary>
    public void Reclassify()
    {
        if (!IsNodeReady())
        {
            // No synchronizer to configure yet. A pooled body is cleared before it is ever
            // handed out, and the pool may not be in the tree when that happens.
            return;
        }

        if (!IsMultiplayerAuthority())
        {
            Follow();
            return;
        }

        var next = Rail != Vector3.Zero ? Net.ReplicationClass.Railed
            : Sleeping ? Net.ReplicationClass.Sleeping
            : Net.ReplicationClass.Dynamic;

        if (_flush > 0)
        {
            // Mid-flush. Finish it on whatever the parcel has since decided to be: a box that
            // woke up again just gets its Dynamic interval put back.
            if (--_flush == 0)
            {
                Enter(next);
            }

            return;
        }

        if (next == Class)
        {
            return;
        }

        if (next == Net.ReplicationClass.Sleeping)
        {
            // Arch §3.4 owes one final transform before the silence, and a synchronizer has no
            // flush: the send is bought by holding Dynamic open with the interval at zero for a
            // frame. Jolt has already stopped the body, so what goes out is the resting pose.
            _flush = FlushFrames;
            Synchronizer.ReplicationInterval = 0.0f;
            return;
        }

        Enter(next);
    }

    /// <summary>Where carrier <paramref name="slot"/> holds, in this body's local space.</summary>
    public Vector3 LocalGrip(int slot) => CarriersRequired <= 1
        ? Vector3.Zero
        : new Vector3(slot == 0 ? -GripHalfWidth : GripHalfWidth, 0, 0);

    /// <summary>Where carrier <paramref name="slot"/> holds, in global space.</summary>
    public Vector3 GlobalGrip(int slot) => ToGlobal(LocalGrip(slot));

    private void Enter(Net.ReplicationClass next)
    {
        Class = next;
        Net.Replication.Apply(Synchronizer, next, TransformProperty, RailProperty);
    }

    private void Follow()
    {
        // A peer that does not own this body does not simulate it either (arch §3.1). A body that
        // both integrates gravity and takes the authority's transform fights itself and settles
        // about 25 cm low - measured in the L3 harness, 2026-08-25, where it presented as two
        // intermittent assertion failures rather than as anything that looked like a bug.
        if (Freeze)
        {
            return;
        }

        Freeze = true;
        FreezeMode = FreezeModeEnum.Kinematic;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
    }

    private Node3D Attach()
    {
        var visual = new Node3D { Name = "Visual" };
        AddChild(visual);
        return visual;
    }

    private MultiplayerSynchronizer Sync()
    {
        var sync = new MultiplayerSynchronizer { Name = "Sync" };
        AddChild(sync);
        return sync;
    }
}
