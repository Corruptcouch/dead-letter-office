using System.Collections.Generic;

using Dlo.Game.Carry;
using Dlo.Game.Net;

using Godot;

namespace Dlo.Game.Facility;

/// <summary>
/// A belt (E4-01). A parcel it carries is <see cref="ReplicationClass.Railed"/>: kinematic on a
/// known spline at a known speed, so every peer computes where it is instead of being told
/// (arch §3.4).
/// </summary>
/// <remarks>
/// <b>The belt never stops</b> (vision §2). Parcels that reach the end stay on it and the ones
/// behind queue up against them, because accumulation is the design keystone rather than an
/// overflow condition to handle — a belt that despawned its backlog would delete the pressure
/// the whole game is about.
/// <para>
/// The host puts a parcel on with <see cref="Accept"/>; a client is never told to, and instead
/// finds parcels whose rail tuple already names this belt. That is what makes the tuple's single
/// send sufficient.
/// </para>
/// </remarks>
public partial class Conveyor : Node3D
{
    private readonly List<Rider> _riders = [];

    private Path3D _path = null!;

    /// <summary>Which belt this is, as it appears in a parcel's rail tuple. Never zero.</summary>
    [Export]
    public int BeltId { get; set; } = 1;

    /// <summary>Metres per second. The speed every peer extrapolates at.</summary>
    [Export]
    public float Speed { get; set; } = 1.2f;

    /// <summary>How many parcels ride abreast.</summary>
    [Export]
    public int Lanes { get; set; } = 2;

    /// <summary>Metres between lane centres.</summary>
    [Export]
    public float LaneWidth { get; set; } = 0.6f;

    /// <summary>Metres a parcel keeps behind the one in front once the belt backs up.</summary>
    [Export]
    public float Spacing { get; set; } = 0.7f;

    /// <summary>Length of the generated straight belt, when no <c>Path</c> child was authored.</summary>
    [Export]
    public float Length { get; set; } = 12.0f;

    /// <summary>How many parcels this belt is carrying, riding or backed up.</summary>
    public int Carrying => _riders.Count;

    /// <inheritdoc/>
    public override void _Ready()
    {
        _path = GetNodeOrNull<Path3D>("Path") ?? Straight();
    }

    /// <summary>
    /// Puts <paramref name="parcel"/> on this belt in <paramref name="lane"/>. Host side.
    /// </summary>
    /// <param name="parcel">The parcel to carry.</param>
    /// <param name="lane">Which lane, clamped into range.</param>
    /// <param name="distance">Where along the belt to start it, in metres.</param>
    /// <remarks>
    /// Writes the rail tuple exactly once. Everything after this is computed on every peer from
    /// that one write, which is what "~6 bytes, once" in arch §3.4 means in practice.
    /// </remarks>
    public void Accept(Carryable parcel, int lane = 0, float distance = 0.0f)
    {
        System.ArgumentNullException.ThrowIfNull(parcel);

        if (Find(parcel) is not null)
        {
            return;
        }

        var slot = Mathf.Clamp(lane, 0, Mathf.Max(0, Lanes - 1));
        parcel.Rail = new Vector3(BeltId, distance, slot);
        Ride(parcel, slot, distance);
    }

    /// <summary>
    /// Takes <paramref name="parcel"/> off this belt and leaves it loose, wherever it is.
    /// </summary>
    /// <remarks>
    /// The demotion to <see cref="ReplicationClass.Dynamic"/> is E2-05's, which owns what a
    /// parcel knocked off a belt costs. This clears the rail tuple and unfreezes the body, which
    /// is all a belt has an opinion about.
    /// </remarks>
    public void Release(Carryable parcel)
    {
        System.ArgumentNullException.ThrowIfNull(parcel);

        if (Find(parcel) is not { } rider)
        {
            return;
        }

        _riders.Remove(rider);
        parcel.Rail = Vector3.Zero;
        parcel.Freeze = false;
    }

    /// <inheritdoc/>
    public override void _PhysicsProcess(double delta)
    {
        Adopt();
        Advance((float)delta);
    }

    /// <summary>Where a parcel <paramref name="distance"/> along lane <paramref name="lane"/> sits.</summary>
    public Transform3D Placement(float distance, int lane)
    {
        var curve = _path.Curve;
        var along = Mathf.Clamp(distance, 0.0f, curve.GetBakedLength());
        var frame = _path.GlobalTransform * curve.SampleBakedWithRotation(along);

        return frame.TranslatedLocal(new Vector3(Offset(lane), 0, 0));
    }

    /// <summary>How far along the belt <paramref name="parcel"/> is, or <c>null</c>.</summary>
    public float? DistanceOf(Carryable parcel) => Find(parcel)?.Distance;

    // ponytail: railed parcels extrapolate from a constant belt speed (§3.4).
    // Ceiling: a client shows a railed parcel in the wrong place for one RTT after any
    // belt speed change, and nothing currently changes belt speed.
    // Upgrade: send speed with the rail packet and interpolate — the field already
    // exists on BeltState, so this is a serialisation change and not a design one.
    private void Advance(float delta)
    {
        var length = _path.Curve.GetBakedLength();

        // Front of the belt first, so each parcel knows where the one ahead of it stopped. This
        // is what makes a backed-up belt queue rather than overlap, and the belt keeps running
        // underneath it either way.
        _riders.Sort(static (a, b) => b.Distance.CompareTo(a.Distance));

        var lanes = new Dictionary<int, float>();

        foreach (var rider in _riders)
        {
            if (!GodotObject.IsInstanceValid(rider.Parcel))
            {
                continue;
            }

            var ceiling = lanes.TryGetValue(rider.Lane, out var ahead)
                ? Mathf.Min(length, ahead - Spacing)
                : length;

            rider.Distance = Mathf.Clamp(rider.Distance + (Speed * delta), 0.0f, Mathf.Max(0.0f, ceiling));
            lanes[rider.Lane] = rider.Distance;

            rider.Parcel.GlobalTransform = Placement(rider.Distance, rider.Lane);
        }

        _riders.RemoveAll(static r => !GodotObject.IsInstanceValid(r.Parcel));
    }

    // ponytail: a client finds its railed parcels by scanning the parcel group each frame.
    // Ceiling: O(belts x parcels) per physics frame. At arch §8's 80 parcels and the handful of
    // belts Layer 2 has, that is a few hundred integer compares - real, and far below anything
    // the budget notices.
    // Upgrade: the rail tuple's arrival is a synchronizer signal, so a parcel can announce
    // itself once instead. E2-05 owns that path, because it owns promotion and demotion.
    private void Adopt()
    {
        foreach (var node in GetTree().GetNodesInGroup(Carryable.Group))
        {
            if (node is not Carryable parcel
                || !Mathf.IsEqualApprox(parcel.Rail.X, BeltId)
                || Find(parcel) is not null)
            {
                continue;
            }

            Ride(parcel, Mathf.RoundToInt(parcel.Rail.Z), parcel.Rail.Y);
        }
    }

    private void Ride(Carryable parcel, int lane, float distance)
    {
        // Kinematic, not simulated: the spline is the authority on where a railed parcel is, and
        // a body still falling would fight it (arch §3.4).
        parcel.Freeze = true;
        parcel.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        parcel.LinearVelocity = Vector3.Zero;
        parcel.AngularVelocity = Vector3.Zero;

        Replication.Apply(
            parcel.Synchronizer,
            ReplicationClass.Railed,
            Carryable.TransformProperty,
            Carryable.RailProperty);

        _riders.Add(new Rider(parcel, lane, distance));
        parcel.GlobalTransform = Placement(distance, lane);
    }

    private Rider? Find(Carryable parcel) => _riders.Find(r => r.Parcel == parcel);

    private float Offset(int lane) => (lane - ((Lanes - 1) / 2.0f)) * LaneWidth;

    private Path3D Straight()
    {
        var curve = new Curve3D();
        curve.AddPoint(Vector3.Zero);
        curve.AddPoint(new Vector3(0, 0, -Length));

        var path = new Path3D { Name = "Path", Curve = curve };
        AddChild(path);
        return path;
    }

    private sealed class Rider(Carryable parcel, int lane, float distance)
    {
        public Carryable Parcel { get; } = parcel;

        public int Lane { get; } = lane;

        public float Distance { get; set; } = distance;
    }
}
