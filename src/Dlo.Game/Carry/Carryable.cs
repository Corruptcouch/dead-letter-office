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

        // A conveyor finds the parcels it should be carrying by looking here (E4-01). Joining
        // in _Ready rather than on entry means a parcel spawned already railed - which is what
        // a client gets - is found without a second message.
        AddToGroup(Group);
    }

    /// <summary>Where carrier <paramref name="slot"/> holds, in this body's local space.</summary>
    public Vector3 LocalGrip(int slot) => CarriersRequired <= 1
        ? Vector3.Zero
        : new Vector3(slot == 0 ? -GripHalfWidth : GripHalfWidth, 0, 0);

    /// <summary>Where carrier <paramref name="slot"/> holds, in global space.</summary>
    public Vector3 GlobalGrip(int slot) => ToGlobal(LocalGrip(slot));

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

        // Dynamic until something says otherwise: a loose parcel is the class that has to be
        // right by default, because the cheap classes are only correct when they are earned.
        Net.Replication.Apply(sync, Net.ReplicationClass.Dynamic, TransformProperty, RailProperty);
        return sync;
    }
}
