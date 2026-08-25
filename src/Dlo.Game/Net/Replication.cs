using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// The three replication classes of arch §3.4, and the only place their
/// <c>replication_interval</c> values are written down.
/// </summary>
/// <remarks>
/// A parcel is in exactly one of these at any moment. <see cref="Railed"/> is the one the
/// design depends on: the belt never stops (vision §2), so most parcels in a shift are railed
/// or asleep, and both cost nothing. Arch §3.4 is explicit that a belt parcel in
/// <see cref="Dynamic"/> is a bug against that section rather than a tuning problem.
/// </remarks>
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
/// <para>
/// <b>Per class, never globally</b> (arch §3.4). One interval for the whole game is the
/// straightforward thing to write and it is what makes the 200 KB/s arithmetic in that section
/// come true: it prices every sleeping parcel at the rate the thrown one needs.
/// </para>
/// <para>
/// <b>Promotion and demotion do not respawn anything.</b> Knock a parcel off the belt and it
/// goes <c>Railed → Dynamic</c>; let it settle and it goes <c>Dynamic → Sleeping</c>. Both are
/// a reconfiguration of the synchronizer already on the node, because respawning would destroy
/// the identity that E2-02 exists to preserve.
/// </para>
/// <para>
/// <b>This story introduces no RPC.</b> Arch §3.1's <c>TransferMode</c> / <c>CallLocal</c> rule
/// therefore has nothing to bite on: everything here rides
/// <see cref="MultiplayerSynchronizer"/>, whose traffic is unreliable by construction. Stated
/// rather than skipped — "no RPCs were added" and "nobody thought about the RPC defaults" look
/// the same in a diff, and the second one is how a positional stream ends up
/// <c>Reliable</c>.
/// </para>
/// </remarks>
public static class Replication
{
    /// <summary>
    /// <see cref="ReplicationClass.Dynamic"/>: 30 Hz. The only class that streams, and the one
    /// every budget number in arch §8 is really about.
    /// </summary>
    public const double DynamicInterval = 1.0 / 30.0;

    /// <summary>
    /// <see cref="ReplicationClass.Railed"/>: zero, meaning "look every tick".
    /// </summary>
    /// <remarks>
    /// Counter-intuitive but correct. A railed parcel's rail tuple is watched rather than
    /// streamed, so this interval governs how soon a <i>change</i> is noticed, not how often
    /// anything is sent. Nothing changes after entry, so the traffic is one message and then
    /// silence — and when the parcel does get knocked off the belt, the promotion is seen on
    /// the next tick rather than up to a frame late.
    /// </remarks>
    public const double RailedInterval = 0.0;

    /// <summary>
    /// <see cref="ReplicationClass.Sleeping"/>: an hour, which is a deliberate absurdity.
    /// </summary>
    /// <remarks>
    /// A sleeping parcel replicates nothing, so any interval would do. This one is chosen so
    /// that if a property is ever left on <c>Always</c> by mistake, the result is one stray
    /// packet an hour rather than a silent 30 Hz stream from every parcel at rest in the
    /// facility — which is precisely the bill arch §3.4 refuses to pay, arriving by accident.
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
    /// </summary>
    /// <param name="synchronizer">The node's existing synchronizer. It is not replaced.</param>
    /// <param name="replicationClass">The class to move to.</param>
    /// <param name="transform">
    /// The property carrying position, replicated only in <see cref="ReplicationClass.Dynamic"/>.
    /// </param>
    /// <param name="rail">
    /// The property carrying <c>(beltId, distanceAlong, lane)</c>, watched only in
    /// <see cref="ReplicationClass.Railed"/>.
    /// </param>
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

        // Always = streamed on the interval. OnChange = watched, and sent only when it moves.
        // Never = the property exists in the config but no bytes leave because of it.
        config.PropertySetReplicationMode(transform, replicationClass == ReplicationClass.Dynamic
            ? SceneReplicationConfig.ReplicationMode.Always
            : SceneReplicationConfig.ReplicationMode.Never);

        config.PropertySetReplicationMode(rail, replicationClass == ReplicationClass.Railed
            ? SceneReplicationConfig.ReplicationMode.OnChange
            : SceneReplicationConfig.ReplicationMode.Never);

        synchronizer.ReplicationConfig = config;
        synchronizer.ReplicationInterval = (float)IntervalFor(replicationClass);

        // Watched properties have their own interval, and leaving it at the streamed one would
        // make a promotion off the belt wait for the Sleeping hour to elapse.
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
