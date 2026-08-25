using System.Threading.Tasks;

using Dlo.Domain;
using Dlo.Game.Carry;
using Dlo.Game.Facility;
using Dlo.Game.Net;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E2-05. A parcel moves between arch §3.4's three replication classes on its own, in place, and
/// the two cheap ones cost what that section says they cost.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ParcelClassTests
{
    /// <summary>Physics frames a body is given to come to rest and for Jolt to say so.</summary>
    /// <remarks>
    /// Godot's default is half a second below threshold before a body sleeps, and the drop takes
    /// a moment before that. Generous, because the cost of a tight number here is a flaky suite.
    /// </remarks>
    private const int SettleFrames = 240;

    [TestCase]
    public async Task A_parcel_entering_a_belt_is_railed_and_leaving_it_is_promoted_back()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel(above: 4.0f);
            await rig.Frame();

            rig.Belt.Accept(parcel);
            AssertThat(parcel.Class).IsEqual(ReplicationClass.Railed);

            // Knocked off. Both directions are asserted because only one of them is the one a
            // belt is likely to get wrong: a parcel that never leaves Railed keeps a transform
            // nobody is sending, and it hangs in the air on every peer but the host.
            rig.Belt.Release(parcel);
            AssertThat(parcel.Class).IsEqual(ReplicationClass.Dynamic);
            AssertBool(parcel.Freeze).IsFalse();
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_parcel_left_to_settle_is_demoted_to_sleeping()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel(above: 0.4f);

            // Jolt's own verdict, not a stand-in for it. The demotion is defined by the physics
            // engine reporting rest (arch §3.4), so a test that set Sleeping itself would assert
            // that this class can read a boolean.
            await rig.Rest(parcel);

            AssertBool(parcel.Sleeping)
                .OverrideFailureMessage($"The parcel never came to rest at {parcel.GlobalPosition}.")
                .IsTrue();
            AssertThat(parcel.Class).IsEqual(ReplicationClass.Sleeping);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_sleeping_parcel_sends_nothing_at_all()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel(above: 0.4f);
            await rig.Rest(parcel);

            // Metered from after the demotion, so the one final transform it owes is already
            // behind us and what is left is the "then nothing at all" half of arch §3.4.
            var meter = new ReplicationMeter(parcel.Synchronizer);
            for (var i = 0; i < 30; i++)
            {
                await rig.Frame();
                meter.Sample();
            }

            AssertInt(meter.Streaming).OverrideFailureMessage(meter.ToString()).IsEqual(0);
            AssertInt(meter.Changes).OverrideFailureMessage(meter.ToString()).IsEqual(0);
            AssertInt(meter.Bytes).OverrideFailureMessage(meter.ToString()).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_sleeping_parcel_that_is_disturbed_is_promoted_again()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel(above: 0.4f);
            await rig.Rest(parcel);

            parcel.ApplyCentralImpulse(new Vector3(0, 6, 0));
            await rig.Settle(4);

            // The demotion is worthless without this: a parcel that stayed in Sleeping after
            // being kicked would move on the host and on nobody else's screen.
            AssertBool(parcel.Sleeping).IsFalse();
            AssertThat(parcel.Class).IsEqual(ReplicationClass.Dynamic);
            AssertThat(parcel.Synchronizer.ReplicationConfig
                    .PropertyGetReplicationMode(Carryable.TransformProperty))
                .IsEqual(SceneReplicationConfig.ReplicationMode.Always);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_belt_parcel_is_never_dynamic()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel(above: 4.0f);
            await rig.Frame();
            rig.Belt.Accept(parcel);

            // Arch §3.4 calls a belt parcel in Dynamic a bug against the section rather than a
            // tuning problem, so it earns an assertion rather than a code review — and it is
            // checked every frame, because the way this regresses is one frame of promotion
            // between a belt writing the tuple and something else reading it.
            for (var i = 0; i < 60; i++)
            {
                await rig.Frame();
                AssertThat(parcel.Class)
                    .OverrideFailureMessage($"Frame {i}: a belt parcel was in {parcel.Class}.")
                    .IsEqual(ReplicationClass.Railed);
            }
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_parcel_this_peer_does_not_own_neither_simulates_nor_reclassifies()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel(above: 4.0f);
            parcel.SetMultiplayerAuthority(2);
            await rig.Settle(4);

            // Two halves of one rule (arch §3.1). A body that both integrates gravity and takes
            // the authority's transform fights itself, and a peer that reconfigured its own
            // synchronizer would be decoding the host's packets against a different property
            // list — which is silent, and looks like a parcel that stopped moving.
            AssertBool(parcel.Freeze).IsTrue();
            AssertThat(parcel.Class).IsEqual(ReplicationClass.Dynamic);

            parcel.Rail = new Vector3(rig.Belt.BeltId, 1.0f, 0.0f);
            await rig.Settle(4);

            AssertThat(parcel.Class)
                .OverrideFailureMessage("A client decided its own class from a replicated tuple.")
                .IsEqual(ReplicationClass.Dynamic);
            AssertThat(parcel.Synchronizer.ReplicationConfig
                    .PropertyGetReplicationMode(Carryable.TransformProperty))
                .IsEqual(SceneReplicationConfig.ReplicationMode.Always);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public void The_rail_tuple_is_watched_in_every_class()
    {
        var sync = AutoFree(new MultiplayerSynchronizer())!;

        // The tuple going to zero is the only thing a client is told when a parcel leaves a belt,
        // and in the cheap classes there is no transform stream to say it either. A tuple that
        // stopped being watched the instant the parcel was promoted would leave every other peer
        // riding a parcel the host has already dropped.
        foreach (var replicationClass in System.Enum.GetValues<ReplicationClass>())
        {
            Replication.Apply(
                sync, replicationClass, Carryable.TransformProperty, Carryable.RailProperty);

            AssertThat(sync.ReplicationConfig.PropertyGetReplicationMode(Carryable.RailProperty))
                .OverrideFailureMessage($"{replicationClass} stopped watching the rail tuple.")
                .IsEqual(SceneReplicationConfig.ReplicationMode.OnChange);
        }
    }

    private static ClassRig Rig()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "ClassRig" };
        tree.Root.AddChild(root);

        var floor = new StaticBody3D { Name = "Floor", Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40, 1, 40) } });
        root.AddChild(floor);

        var belt = new Conveyor { Name = "Belt", BeltId = 3, Speed = 1.2f, Lanes = 2, Length = 12.0f };
        belt.Position = new Vector3(0, 4, 0);
        root.AddChild(belt);

        return new ClassRig(root, belt);
    }

    private sealed record ClassRig(Node3D Root, Conveyor Belt)
    {
        private int _made;

        public Carryable Parcel(float above)
        {
            var parcel = new Carryable
            {
                Name = $"Parcel{++_made}",
                Id = new ParcelId((uint)_made),
                Position = new Vector3(0, above, 0),
            };
            parcel.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * 0.4f } });
            Root.AddChild(parcel);
            return parcel;
        }

        public async Task Frame()
        {
            var tree = Root.GetTree();
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }

        public async Task Settle(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                await Frame();
            }
        }

        /// <summary>Runs until <paramref name="parcel"/> is asleep and its class has followed.</summary>
        public async Task Rest(Carryable parcel)
        {
            for (var i = 0; i < SettleFrames; i++)
            {
                await Frame();

                if (parcel.Class == ReplicationClass.Sleeping)
                {
                    return;
                }
            }
        }

        public void Drop()
        {
            Root.GetParent().RemoveChild(Root);
            Root.Free();
        }
    }
}
