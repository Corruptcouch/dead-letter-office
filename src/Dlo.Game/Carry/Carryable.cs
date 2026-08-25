using Godot;

namespace Dlo.Game.Carry;

/// <summary>
/// Something a player can pick up: a mass, a shape, and one grip point per carrier it needs.
/// </summary>
/// <remarks>
/// <b>A placeholder for E2's parcel, and deliberately not called one.</b> E1-08 needs an object
/// the domain marks as over one-person capacity. E2-01 has since defined <c>ParcelRecord</c>,
/// which owns both facts below; they stay here until E2-04 gives a node the id to look one up by.
/// </remarks>
// ponytail: capacity and the policy lock are [Export] fields on the body.
// Ceiling: E2-01 put both on ParcelRecord, so they are now authored in two places and the grab
// reads the node's copy - nothing can query "every two-person load in the shift", and the lock
// still has no source, because E3-05's PolicyState does not exist.
// Upgrade: E2-04 gives the node its ParcelId at spawn and these become a registry lookup; E2-02
// is the test that the record outlives the node. E3-05 gives Locked a reason to change mid-shift.
public partial class Carryable : RigidBody3D
{
    /// <summary>
    /// How many carriers this load needs at once. Two means one player cannot lift it (E1-08).
    /// </summary>
    [Export]
    public int CarriersRequired { get; set; } = 1;

    /// <summary>
    /// Half the distance between carrier slots, along the body's local X. Only meaningful when
    /// <see cref="CarriersRequired"/> is above one.
    /// </summary>
    [Export]
    public float GripHalfWidth { get; set; } = 0.5f;

    /// <summary>
    /// Whether policy currently forbids picking this up. Validated by the host on every grab.
    /// </summary>
    /// <remarks>
    /// Nothing sets this yet — E3-05's <c>PolicyState</c> is what will. It exists now because
    /// E1-04 requires the host to validate a policy lock, and a validation path with no input is
    /// still a validation path; adding the check later means auditing every grab call site again.
    /// </remarks>
    [Export]
    public bool Locked { get; set; }

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
