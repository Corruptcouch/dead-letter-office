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
/// E4-01. The mechanism that makes "the belt never stops" affordable: a parcel on a belt is a
/// spline, a speed and a lane, sent once, and everything after that is arithmetic every peer can
/// do for itself (arch §3.4).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConveyorTests
{
    [TestCase]
    public async Task A_parcel_entering_a_belt_is_railed_and_its_tuple_is_sent_once()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel();
            await rig.Frame();

            rig.Belt.Accept(parcel, lane: 1, distance: 2.0f);
            await rig.Frame();

            // The tuple itself, in arch §3.4's order.
            AssertFloat(parcel.Rail.X).IsEqual(rig.Belt.BeltId);
            AssertFloat(parcel.Rail.Y).IsEqual(2.0f);
            AssertFloat(parcel.Rail.Z).IsEqual(1.0f);

            // And the class it put the parcel into: no transform stream at all.
            AssertThat(parcel.Synchronizer.ReplicationConfig
                    .PropertyGetReplicationMode(Carryable.TransformProperty))
                .IsEqual(SceneReplicationConfig.ReplicationMode.Never);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_railed_parcel_moves_every_frame_and_sends_nothing_while_it_does()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel();
            await rig.Frame();
            rig.Belt.Accept(parcel);
            await rig.Frame();

            // Metered from here, so the one send that put it on the belt is already behind us.
            var meter = new ReplicationMeter(parcel.Synchronizer);
            var start = parcel.GlobalPosition;

            for (var i = 0; i < 30; i++)
            {
                await rig.Frame();
                meter.Sample();
            }

            // Measured, not read off the configuration (E4-01). The parcel demonstrably moved,
            // and the synchronizer demonstrably had nothing to say about it — which is the whole
            // of arch §3.4's claim, and the reason the 200 KB/s arithmetic never happens.
            AssertBool(parcel.GlobalPosition.DistanceTo(start) > 0.1f)
                .OverrideFailureMessage($"The belt did not move the parcel: {meter}")
                .IsTrue();

            AssertInt(meter.Changes).OverrideFailureMessage(meter.ToString()).IsEqual(0);
            AssertInt(meter.Bytes).OverrideFailureMessage(meter.ToString()).IsEqual(0);
            AssertInt(meter.Streaming).OverrideFailureMessage(meter.ToString()).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_loose_parcel_streams_its_transform_so_the_meter_is_measuring_something()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel();
            await rig.Frame();

            // The control. Without it, every zero above could equally mean the instrument is
            // broken — which is the way a traffic assertion usually goes quietly wrong.
            var meter = new ReplicationMeter(parcel.Synchronizer);
            for (var i = 0; i < 5; i++)
            {
                parcel.GlobalPosition += new Vector3(0, 0, -0.25f);
                await rig.Frame();
                meter.Sample();
            }

            AssertInt(meter.Streaming).OverrideFailureMessage(meter.ToString()).IsEqual(1);
            AssertInt(meter.Changes).OverrideFailureMessage(meter.ToString()).IsGreater(0);
            AssertInt(meter.Bytes).OverrideFailureMessage(meter.ToString()).IsGreater(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task Parcels_accumulate_at_the_end_and_the_belt_keeps_running()
    {
        // A fast belt, so the queue forms in three seconds of test rather than eight. Nothing
        // about accumulation depends on the speed; the wall clock does, and L2 is budgeted in
        // seconds (arch §10.1).
        var rig = Rig(speed: 6.0f);

        try
        {
            var parcels = new Carryable[4];
            for (var i = 0; i < parcels.Length; i++)
            {
                parcels[i] = rig.Parcel();
            }

            await rig.Frame();

            for (var i = 0; i < parcels.Length; i++)
            {
                rig.Belt.Accept(parcels[i], lane: 0, distance: i * 1.5f);
            }

            // Long enough that every one of them has reached the end and pressed up against the
            // one in front: the rearmost has 9.9 m to cover and 3 seconds to do it in.
            await rig.Settle(180);

            // Vision §2: the belt does not stop and does not despawn its backlog. Accumulation
            // is the keystone, so losing a parcel here would be deleting the pressure the game
            // is made of — not tidying up.
            AssertInt(rig.Belt.Carrying).IsEqual(4);

            var front = rig.Belt.DistanceOf(parcels[3])!.Value;
            AssertFloat(front).IsEqualApprox(rig.Belt.Length, 0.01f);

            // And they queue rather than pile into one another.
            for (var i = 0; i < 3; i++)
            {
                var behind = rig.Belt.DistanceOf(parcels[i])!.Value;
                var ahead = rig.Belt.DistanceOf(parcels[i + 1])!.Value;
                AssertFloat(ahead - behind).IsEqualApprox(rig.Belt.Spacing, 0.01f);
            }
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_parcel_that_never_boarded_is_picked_up_from_its_rail_tuple_alone()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel();
            await rig.Frame();

            // What a client has: a parcel built from spawn args whose rail tuple arrived once,
            // and no instruction of any kind. It rides because the tuple says which belt.
            parcel.Rail = new Vector3(rig.Belt.BeltId, 3.0f, 1.0f);
            await rig.Frame();

            AssertInt(rig.Belt.Carrying).IsEqual(1);
            AssertFloat(rig.Belt.DistanceOf(parcel)!.Value).IsGreater(2.9f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_parcel_belonging_to_another_belt_is_left_alone()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel();
            await rig.Frame();

            parcel.Rail = new Vector3(rig.Belt.BeltId + 7, 3.0f, 0.0f);
            await rig.Frame();

            // Belts are told apart by the tuple's first field and nothing else, so this is the
            // assertion that keeps two belts in one room from stealing each other's parcels.
            AssertInt(rig.Belt.Carrying).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task Lanes_are_different_places_across_the_belt()
    {
        var rig = Rig();

        try
        {
            var left = rig.Parcel();
            var right = rig.Parcel();
            await rig.Frame();

            rig.Belt.Accept(left, lane: 0, distance: 4.0f);
            rig.Belt.Accept(right, lane: 1, distance: 4.0f);
            await rig.Frame();

            // Two parcels the same distance along are side by side, not inside one another.
            AssertFloat(left.GlobalPosition.DistanceTo(right.GlobalPosition))
                .IsEqualApprox(rig.Belt.LaneWidth, 0.01f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task Releasing_a_parcel_hands_it_back_to_physics()
    {
        var rig = Rig();

        try
        {
            var parcel = rig.Parcel();
            await rig.Frame();
            rig.Belt.Accept(parcel);
            await rig.Frame();

            AssertBool(parcel.Freeze).IsTrue();

            rig.Belt.Release(parcel);
            await rig.Frame();

            // Off the belt is loose, and loose is simulated. A parcel left frozen would hang in
            // the air, which is E2-05's promotion seen from the belt's side.
            AssertInt(rig.Belt.Carrying).IsEqual(0);
            AssertBool(parcel.Freeze).IsFalse();
            AssertVector(parcel.Rail).IsEqual(Vector3.Zero);
        }
        finally
        {
            rig.Drop();
        }
    }

    private static BeltRig Rig(float speed = 1.2f)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "BeltRig" };
        tree.Root.AddChild(root);

        var belt = new Conveyor { Name = "Belt", BeltId = 3, Speed = speed, Lanes = 2, Length = 12.0f };
        root.AddChild(belt);
        return new BeltRig(root, belt);
    }

    private sealed record BeltRig(Node3D Root, Conveyor Belt)
    {
        private int _made;

        public Carryable Parcel()
        {
            var parcel = new Carryable { Name = $"Parcel{++_made}", Id = new ParcelId((uint)_made) };
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

        public void Drop()
        {
            Root.GetParent().RemoveChild(Root);
            Root.Free();
        }
    }
}
