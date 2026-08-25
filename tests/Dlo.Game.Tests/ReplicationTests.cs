using Dlo.Game.Net;
using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E0-06. Three replication classes with distinct intervals, and a node that moves between them
/// without being respawned.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ReplicationTests
{
    /// <summary>
    /// Godot rounds <c>replication_interval</c> on the way in — 1/30 is stored as 0.033333, not
    /// as the nearest float — so comparing it exactly asserts Godot's rounding rather than the
    /// class under test.
    /// </summary>
    private const float Tolerance = 0.0001f;

    private static readonly NodePath _transform = new(".:position");
    private static readonly NodePath _rail = new(".:rotation");

    [TestCase]
    public void The_three_classes_have_three_distinct_intervals()
    {
        // Distinctness is the observable proof that the interval is per class rather than one
        // global number (arch §3.4) - which is the setting that makes that section's 200 KB/s
        // arithmetic come true, by pricing a sleeping parcel at the rate a thrown one needs.
        AssertFloat(Replication.IntervalFor(ReplicationClass.Dynamic))
            .IsEqual(Replication.DynamicInterval);
        AssertFloat(Replication.IntervalFor(ReplicationClass.Railed))
            .IsEqual(Replication.RailedInterval);
        AssertFloat(Replication.IntervalFor(ReplicationClass.Sleeping))
            .IsEqual(Replication.SleepingInterval);

        AssertBool(Replication.DynamicInterval != Replication.RailedInterval).IsTrue();
        AssertBool(Replication.RailedInterval != Replication.SleepingInterval).IsTrue();
        AssertBool(Replication.DynamicInterval != Replication.SleepingInterval).IsTrue();
    }

    [TestCase]
    public void Only_dynamic_streams_a_transform()
    {
        var sync = AutoFree(new MultiplayerSynchronizer())!;

        Replication.Apply(sync, ReplicationClass.Dynamic, _transform, _rail);
        AssertThat(sync.ReplicationConfig.PropertyGetReplicationMode(_transform))
            .IsEqual(SceneReplicationConfig.ReplicationMode.Always);

        // The two cheap classes must send no transform at all. A railed parcel sitting in
        // Dynamic is a bug against arch §3.4 rather than a tuning problem, and this is the
        // assertion that says so.
        Replication.Apply(sync, ReplicationClass.Railed, _transform, _rail);
        AssertThat(sync.ReplicationConfig.PropertyGetReplicationMode(_transform))
            .IsEqual(SceneReplicationConfig.ReplicationMode.Never);

        Replication.Apply(sync, ReplicationClass.Sleeping, _transform, _rail);
        AssertThat(sync.ReplicationConfig.PropertyGetReplicationMode(_transform))
            .IsEqual(SceneReplicationConfig.ReplicationMode.Never);
    }

    [TestCase]
    public void Only_railed_watches_the_rail_tuple()
    {
        var sync = AutoFree(new MultiplayerSynchronizer())!;

        // OnChange, not Always: the tuple is sent once when the parcel joins the belt and then
        // never again, because nothing about it changes. That is the "~6 bytes, once" of arch
        // §3.4, and it is the whole reason the design survives its own keystone.
        Replication.Apply(sync, ReplicationClass.Railed, _transform, _rail);
        AssertThat(sync.ReplicationConfig.PropertyGetReplicationMode(_rail))
            .IsEqual(SceneReplicationConfig.ReplicationMode.OnChange);

        Replication.Apply(sync, ReplicationClass.Dynamic, _transform, _rail);
        AssertThat(sync.ReplicationConfig.PropertyGetReplicationMode(_rail))
            .IsEqual(SceneReplicationConfig.ReplicationMode.Never);

        Replication.Apply(sync, ReplicationClass.Sleeping, _transform, _rail);
        AssertThat(sync.ReplicationConfig.PropertyGetReplicationMode(_rail))
            .IsEqual(SceneReplicationConfig.ReplicationMode.Never);
    }

    [TestCase]
    public void A_node_is_promoted_and_demoted_without_being_respawned()
    {
        var node = AutoFree(new Node3D { Name = "Parcel" })!;
        var sync = new MultiplayerSynchronizer { Name = "Sync" };
        node.AddChild(sync);

        var identity = node.GetInstanceId();
        var syncIdentity = sync.GetInstanceId();

        // Knocked off the belt, then settling: Railed -> Dynamic -> Sleeping, both directions
        // asserted (E2-05 asserts them again over the wire). Respawning instead would destroy
        // the ParcelId that E2-02 exists to preserve, so the identity checks are the real
        // assertion here and the intervals are the evidence the class actually changed.
        Replication.Apply(sync, ReplicationClass.Railed, _transform, _rail);
        AssertFloat(sync.ReplicationInterval).IsEqualApprox((float)Replication.RailedInterval, Tolerance);

        Replication.Apply(sync, ReplicationClass.Dynamic, _transform, _rail);
        AssertFloat(sync.ReplicationInterval).IsEqualApprox((float)Replication.DynamicInterval, Tolerance);

        Replication.Apply(sync, ReplicationClass.Sleeping, _transform, _rail);
        AssertFloat(sync.ReplicationInterval).IsEqualApprox((float)Replication.SleepingInterval, Tolerance);

        Replication.Apply(sync, ReplicationClass.Railed, _transform, _rail);
        AssertFloat(sync.ReplicationInterval).IsEqualApprox((float)Replication.RailedInterval, Tolerance);

        AssertBool(node.GetInstanceId() == identity).IsTrue();
        AssertBool(sync.GetInstanceId() == syncIdentity).IsTrue();
    }

    [TestCase]
    public void A_promotion_off_the_belt_is_noticed_on_the_next_tick()
    {
        var sync = AutoFree(new MultiplayerSynchronizer())!;

        // The watched-property interval is its own knob, and leaving it at the streamed one
        // would make a parcel knocked off a belt while Sleeping wait out that hour before
        // anyone heard about it.
        Replication.Apply(sync, ReplicationClass.Sleeping, _transform, _rail);

        AssertFloat(sync.DeltaInterval).IsEqualApprox((float)Replication.RailedInterval, Tolerance);
    }
}
