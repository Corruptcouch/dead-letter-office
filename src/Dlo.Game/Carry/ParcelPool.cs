using System.Collections.Generic;

using Dlo.Domain;

using Godot;

namespace Dlo.Game.Carry;

/// <summary>
/// Recycles parcel bodies instead of freeing them (E2-06, standards §10). Parcels are the
/// highest-churn object in the game, because the belt never stops (vision §2).
/// </summary>
/// <remarks>
/// Safe only because identity lives in <see cref="ParcelRecord"/> rather than on the node
/// (arch §5.1): a recycled body carries nothing of the parcel it used to show, and the record of
/// that parcel is untouched by any of this.
/// </remarks>
public partial class ParcelPool : Node
{
    private readonly Stack<Carryable> _idle = new();

    private int _made;

    /// <summary>
    /// The most bodies this pool will ever have in existence at once.
    /// </summary>
    /// <remarks>
    /// A ceiling rather than a target: nothing authored approaches it, and reaching it means
    /// something is spawning without bound.
    /// </remarks>
    [Export]
    public int MaxParcels { get; set; } = 256;

    /// <summary>How many bodies exist, idle and in use together.</summary>
    public int Made => _made;

    /// <summary>How many are parked and ready to be handed out again.</summary>
    public int Idle => _idle.Count;

    /// <summary>
    /// Hands out a body showing the parcel <paramref name="args"/> describes — a recycled one if
    /// there is one, otherwise a new one.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The pool is at <see cref="MaxParcels"/> with nothing idle.
    /// </exception>
    // ponytail: the cap is hard, and Acquire throws at it.
    // Ceiling: a facility that genuinely needs more live bodies than MaxParcels stops spawning
    // with an exception rather than degrading. Deliberate while nothing authored comes close -
    // E4-01's accumulating belt is the first thing that could, and it is the story that has to
    // choose between raising the cap and giving the belt an end. Growing silently is the one
    // option this refuses, because "the pool never stops" is exactly how that would present.
    // Upgrade: E4-01 measures the real ceiling with a full belt and decides which it wants.
    public Carryable Acquire(ParcelSpawnArgs args)
    {
        var parcel = _idle.Count > 0 ? _idle.Pop() : Grow();

        Clear(parcel);
        ParcelSpawn.Configure(parcel, args);
        parcel.ProcessMode = ProcessModeEnum.Inherit;
        parcel.Visible = true;
        return parcel;
    }

    /// <summary>
    /// Takes <paramref name="parcel"/> out of play and parks it. It is not freed, and the record
    /// it was showing is not touched.
    /// </summary>
    public void Release(Carryable parcel)
    {
        System.ArgumentNullException.ThrowIfNull(parcel);

        if (_idle.Contains(parcel))
        {
            // Idempotent rather than corrupting: a double release would otherwise hand the same
            // body to two callers, which presents as one parcel in two places.
            return;
        }

        // Taken back under the pool first. A body parked while still parented into the world
        // would be handed out again from there, which presents as one parcel in two places.
        var parent = parcel.GetParent();
        if (parent is null)
        {
            AddChild(parcel);
        }
        else if (parent != this)
        {
            parcel.Reparent(this, keepGlobalTransform: false);
        }

        Clear(parcel);
        parcel.ProcessMode = ProcessModeEnum.Disabled;
        parcel.Visible = false;
        _idle.Push(parcel);
    }

    /// <summary>
    /// Strips a body back to what a fresh one is. Everything mutable, every time — a field
    /// remembered across a recycle is the bug this class exists to not have.
    /// </summary>
    private static void Clear(Carryable parcel)
    {
        parcel.Id = default;
        parcel.Archetype = 0;
        parcel.Size = 0;
        parcel.Condition = 0;
        parcel.GripHalfWidth = 0.5f;

        parcel.Rail = Vector3.Zero;
        parcel.Freeze = false;
        parcel.Sleeping = false;
        parcel.LinearVelocity = Vector3.Zero;
        parcel.AngularVelocity = Vector3.Zero;
        parcel.GlobalTransform = Transform3D.Identity;

        if (parcel.Visual is not null)
        {
            parcel.Visual.Position = Vector3.Zero;
            parcel.Visual.Basis = Basis.Identity;
        }

        // Including its replication class, which is derived from the two lines above and would
        // otherwise be the last parcel's for a frame — a recycled body silently not streaming
        // its transform because the parcel before it was on a belt (E2-05).
        parcel.Reclassify();
    }

    private Carryable Grow()
    {
        if (_made >= MaxParcels)
        {
            throw new System.InvalidOperationException(
                $"The parcel pool is at its cap of {MaxParcels} with none idle. Something is "
                + "spawning without releasing.");
        }

        var parcel = new Carryable();
        AddChild(parcel);
        _made++;

        // Logged, so growth is visible while it is happening rather than inferred afterwards
        // from a frame time. The belt never stopping must not quietly become the pool never
        // stopping (E2-06).
        GD.Print($"Parcel pool grew to {_made} of {MaxParcels}.");
        return parcel;
    }
}
