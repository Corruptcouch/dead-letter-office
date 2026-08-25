using Dlo.Game.Net;

using Godot;

namespace Dlo.Game.Facility;

/// <summary>
/// A door (E4-04). The host owns whether it is open; every peer works out where the leaf is from
/// that one bit.
/// </summary>
/// <remarks>
/// <b>One class, configured as data.</b> Where it travels, how fast, and how wide the opening is
/// are all exports, so E9 can add, move or remove a door without a new type (arch §4.1) — which
/// is the same rule that makes a parcel archetype a content file.
/// <para>
/// A door replicates a <see cref="bool"/> on change and never a transform, for the reason
/// arch §3.4 gives about parcels: the leaf's position is derivable, so sending it would be
/// paying every frame for something both ends can compute.
/// </para>
/// </remarks>
public partial class Door : AnimatableBody3D
{
    private Area3D _threshold = null!;
    private Vector3 _shut;

    /// <summary>Where the leaf sits when open, relative to shut. Up, by default.</summary>
    [Export]
    public Vector3 Travel { get; set; } = new(0, 2.2f, 0);

    /// <summary>Seconds from shut to open. Zero or less is instant, and not an error.</summary>
    [Export]
    public float Seconds { get; set; } = 0.6f;

    /// <summary>The doorway a body can be caught in, used when no <c>Threshold</c> was authored.</summary>
    [Export]
    public Vector3 Opening { get; set; } = new(1.2f, 2.1f, 0.8f);

    /// <summary>
    /// Whether the host says this door is open. <b>The only replicated fact about a door.</b>
    /// </summary>
    /// <remarks>
    /// Settable because a synchronizer has to write it on a client, and because E9's mutations
    /// author a starting state. Gameplay goes through <see cref="Open"/> and <see cref="Shut"/>,
    /// which are host-only.
    /// </remarks>
    [Export]
    public bool IsOpen { get; set; }

    /// <summary>How far along <see cref="Travel"/> the leaf is, 0 shut to 1 open.</summary>
    public float Openness { get; private set; }

    /// <summary>Whether a body other than the leaf itself is standing in the doorway.</summary>
    /// <remarks>
    /// The leaf is a physics body sitting in its own doorway, so it overlaps the threshold at
    /// rest and <c>HasOverlappingBodies</c> is always true. Excluding it here rather than by
    /// collision layers keeps the check correct whatever layers a mutation gives the door.
    /// </remarks>
    public bool Obstructed
    {
        get
        {
            foreach (var body in _threshold.GetOverlappingBodies())
            {
                if (body != this)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>This door's synchronizer, carrying <see cref="IsOpen"/> and nothing else.</summary>
    public MultiplayerSynchronizer Synchronizer { get; private set; } = null!;

    /// <summary>Where <see cref="IsOpen"/> lives, for a replication config.</summary>
    public static NodePath OpenProperty => new($".:{PropertyName.IsOpen}");

    /// <inheritdoc/>
    public override void _Ready()
    {
        _shut = Position;
        _threshold = GetNodeOrNull<Area3D>("Threshold") ?? Doorway();
        Synchronizer = GetNodeOrNull<MultiplayerSynchronizer>("Sync") ?? Sync();
        Openness = IsOpen ? 1.0f : 0.0f;
    }

    /// <summary>Opens the door. Host only; a no-op anywhere else.</summary>
    public void Open()
    {
        if (Multiplayer.IsServer())
        {
            IsOpen = true;
        }
    }

    /// <summary>Shuts the door. Host only; a no-op anywhere else.</summary>
    /// <remarks>
    /// Allowed even when something is in the doorway. It does not close on them: the host
    /// reopens it on the next frame, so a shut order given at the wrong moment resolves itself
    /// rather than needing to be got right by the caller.
    /// </remarks>
    public void Shut()
    {
        if (Multiplayer.IsServer())
        {
            IsOpen = false;
        }
    }

    /// <inheritdoc/>
    public override void _PhysicsProcess(double delta)
    {
        // Standards §10, and E4-04's third criterion: a door may not trap anyone. The host is
        // the only peer that decides, so the recovery is a state change that replicates like any
        // other rather than four machines each rescuing their own copy of the player.
        if (Multiplayer.IsServer() && !IsOpen && Obstructed)
        {
            IsOpen = true;
        }

        var target = IsOpen ? 1.0f : 0.0f;
        var step = Seconds > 0.0f ? (float)delta / Seconds : 1.0f;

        // MoveToward rather than an accumulator, so a NaN or an out-of-range Openness written by
        // anything at all is walked back into the valid range instead of sticking there forever.
        Openness = float.IsFinite(Openness) ? Mathf.MoveToward(Openness, target, step) : target;
        Position = _shut + (Travel * Mathf.Clamp(Openness, 0.0f, 1.0f));
    }

    private Area3D Doorway()
    {
        var threshold = new Area3D { Name = "Threshold", Monitoring = true };
        threshold.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Opening } });
        AddChild(threshold);

        // The doorway stays where the shut leaf is, so it keeps watching the gap rather than
        // riding up with the door and reporting the ceiling.
        threshold.TopLevel = true;
        threshold.GlobalPosition = GlobalPosition;
        return threshold;
    }

    private MultiplayerSynchronizer Sync()
    {
        var sync = new MultiplayerSynchronizer { Name = "Sync" };
        AddChild(sync);

        var config = new SceneReplicationConfig();
        config.AddProperty(OpenProperty);
        config.PropertySetReplicationMode(OpenProperty, SceneReplicationConfig.ReplicationMode.OnChange);

        sync.ReplicationConfig = config;

        // Watched, so the interval that matters is the delta one. A door that took the streamed
        // interval to be noticed would be a door you walk into.
        sync.ReplicationInterval = (float)Replication.SleepingInterval;
        sync.DeltaInterval = (float)Replication.RailedInterval;
        return sync;
    }
}
