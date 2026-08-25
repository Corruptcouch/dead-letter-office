using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// The three replication classes of arch §3.4. A parcel is in exactly one at any moment.
/// </summary>
public enum ReplicationClass
{
    /// <summary>
    /// Riding a conveyor or in a tube. <c>(beltId, distanceAlong, lane)</c> once on entry, then
    /// nothing: clients extrapolate a kinematic body on a known spline at a known speed.
    /// </summary>
    Railed,

    /// <summary>Loose, thrown, held or falling. Transform at full rate, unreliably ordered.</summary>
    Dynamic,

    /// <summary>At rest, with Jolt reporting asleep. One final transform, then nothing at all.</summary>
    Sleeping,
}

/// <summary>
/// Configures a <see cref="MultiplayerSynchronizer"/> for a <see cref="ReplicationClass"/>
/// (E0-06). The mechanism only — parcels start using it in E2-05.
/// </summary>
/// <remarks>
/// Intervals are set per class, never globally (arch §3.4): one interval for the whole game
/// prices every sleeping parcel at the rate the thrown one needs.
/// </remarks>
public static class Replication
{
    /// <summary>
    /// <see cref="ReplicationClass.Dynamic"/>: 30 Hz. The only class that streams.
    /// </summary>
    public const double DynamicInterval = 1.0 / 30.0;

    /// <summary>
    /// <see cref="ReplicationClass.Railed"/>: zero, meaning "look every tick".
    /// </summary>
    /// <remarks>
    /// Counter-intuitive but correct. A railed parcel's rail tuple is watched rather than
    /// streamed, so this governs how soon a <i>change</i> is noticed, not how often anything is
    /// sent — and a parcel knocked off the belt is promoted on the next tick rather than late.
    /// </remarks>
    public const double RailedInterval = 0.0;

    /// <summary>
    /// <see cref="ReplicationClass.Sleeping"/>: an hour, which is a deliberate absurdity.
    /// </summary>
    /// <remarks>
    /// A sleeping parcel replicates nothing, so any interval would do. This one means that a
    /// property left on <c>Always</c> by mistake costs one stray packet an hour rather than a
    /// silent 30 Hz stream from every parcel at rest in the facility.
    /// </remarks>
    public const double SleepingInterval = 3600.0;

    /// <summary>The interval for a class. Three classes, three distinct values.</summary>
    public static double IntervalFor(ReplicationClass replicationClass) => replicationClass switch
    {
        ReplicationClass.Dynamic => DynamicInterval,
        ReplicationClass.Railed => RailedInterval,
        ReplicationClass.Sleeping => SleepingInterval,
        _ => throw new System.ArgumentOutOfRangeException(nameof(replicationClass)),
    };

    /// <summary>
    /// Puts <paramref name="synchronizer"/> into <paramref name="replicationClass"/>, in place.
    /// Reconfigured rather than respawned, because respawning would destroy the identity E2-02
    /// exists to preserve.
    /// </summary>
    /// <param name="synchronizer">The node's existing synchronizer. It is not replaced.</param>
    /// <param name="replicationClass">The class to move to.</param>
    /// <param name="transform">
    /// The property carrying position, replicated only in <see cref="ReplicationClass.Dynamic"/>.
    /// </param>
    /// <param name="rail">
    /// The property carrying <c>(beltId, distanceAlong, lane)</c>, watched in <b>every</b> class.
    /// </param>
    /// <remarks>
    /// The rail tuple is watched whatever the class, because a parcel <i>leaving</i> a belt is a
    /// change to it and nothing else on the wire says so — and in the cheap classes there is no
    /// transform stream to say it either. Watching costs nothing while it does not move, which is
    /// the whole of <c>OnChange</c>.
    /// </remarks>
    public static void Apply(
        MultiplayerSynchronizer synchronizer,
        ReplicationClass replicationClass,
        NodePath transform,
        NodePath rail)
    {
        System.ArgumentNullException.ThrowIfNull(synchronizer);

        var config = synchronizer.ReplicationConfig ?? new SceneReplicationConfig();

        Ensure(config, transform);
        Ensure(config, rail);

        // Always = streamed on the interval. OnChange = watched, sent only when it moves.
        // Never = the property is in the config but no bytes leave because of it.
        config.PropertySetReplicationMode(transform, replicationClass == ReplicationClass.Dynamic
            ? SceneReplicationConfig.ReplicationMode.Always
            : SceneReplicationConfig.ReplicationMode.Never);

        config.PropertySetReplicationMode(rail, SceneReplicationConfig.ReplicationMode.OnChange);

        synchronizer.ReplicationConfig = config;
        synchronizer.ReplicationInterval = (float)IntervalFor(replicationClass);

        // Watched properties have their own interval; leaving it at the streamed one would make
        // a promotion off the belt wait for the Sleeping hour to elapse.
        synchronizer.DeltaInterval = (float)RailedInterval;
    }

    private static void Ensure(SceneReplicationConfig config, NodePath property)
    {
        if (!config.HasProperty(property))
        {
            config.AddProperty(property);
        }
    }
}
