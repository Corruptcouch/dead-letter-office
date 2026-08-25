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

    /// <inheritdoc/>
    public override void _Ready()
    {
        Visual = GetNodeOrNull<Node3D>("Visual") ?? Attach();
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
}
