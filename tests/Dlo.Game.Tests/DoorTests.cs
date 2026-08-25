using System.Threading.Tasks;

using Dlo.Game.Facility;
using Dlo.Game.Net;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E4-04. One bit of host-owned state, one class configured as data, and a door that cannot shut
/// someone in.
/// </summary>
/// <remarks>
/// <b>With no peer at all, <c>Multiplayer.IsServer()</c> is true</c></b> (asserted in
/// <c>SessionRootTests</c>), so these run the host path — the same split E1-04's suite makes.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class DoorTests
{
    [TestCase]
    public async Task A_door_travels_to_open_and_back_and_the_host_is_what_decides()
    {
        var rig = Rig();

        try
        {
            AssertFloat(rig.Door.Openness).IsEqual(0.0f);

            rig.Door.Open();
            await rig.Settle(60);

            AssertBool(rig.Door.IsOpen).IsTrue();
            AssertFloat(rig.Door.Openness).IsEqualApprox(1.0f, 0.001f);
            AssertVector(rig.Door.Position).IsEqualApprox(rig.Door.Travel, Vector3.One * 0.01f);

            rig.Door.Shut();
            await rig.Settle(60);

            AssertBool(rig.Door.IsOpen).IsFalse();
            AssertFloat(rig.Door.Openness).IsEqualApprox(0.0f, 0.001f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_door_replicates_one_bit_and_never_its_transform()
    {
        var rig = Rig();

        try
        {
            var meter = new ReplicationMeter(rig.Door.Synchronizer);

            rig.Door.Open();
            for (var i = 0; i < 40; i++)
            {
                await rig.Frame();
                meter.Sample();
            }

            // The leaf moved the whole way, and the wire carried a single bool. Sending the
            // transform instead would be arch §3.4's mistake in a different room: a position
            // both ends can derive, paid for every frame.
            AssertFloat(rig.Door.Openness).IsEqualApprox(1.0f, 0.001f);
            AssertInt(meter.Streaming).OverrideFailureMessage(meter.ToString()).IsEqual(0);
            AssertInt(meter.Changes).OverrideFailureMessage(meter.ToString()).IsEqual(1);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_door_will_not_shut_on_somebody_standing_in_it()
    {
        var rig = Rig();

        try
        {
            rig.Door.Open();
            await rig.Settle(60);

            var body = rig.Standing();
            await rig.Settle(5);

            AssertBool(rig.Door.Obstructed)
                .OverrideFailureMessage("The rig never got a body into the doorway.")
                .IsTrue();

            rig.Door.Shut();
            await rig.Settle(30);

            // Standards §10: invalid state recovers rather than sticking. The host reopens
            // rather than crushing, so a shut order given at the wrong moment costs a second,
            // not a player who cannot move for the rest of the shift.
            AssertBool(rig.Door.IsOpen).IsTrue();
            AssertFloat(rig.Door.Openness).IsGreater(0.9f);

            body.QueueFree();
            await rig.Settle(5);

            rig.Door.Shut();
            await rig.Settle(60);

            // And once they step out it shuts normally — the recovery is not a latch.
            AssertBool(rig.Door.IsOpen).IsFalse();
            AssertFloat(rig.Door.Openness).IsEqualApprox(0.0f, 0.001f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task An_openness_that_went_bad_walks_itself_back_into_range()
    {
        var rig = Rig();

        try
        {
            // Nothing writes NaN today. The point is that if something ever does, the door
            // recovers on the next frame instead of being a node nobody can open again
            // (standards §10) — which is the failure mode this criterion is really about.
            rig.Door.Set(Door.PropertyName.Position, new Vector3(0, float.NaN, 0));
            rig.Door.Open();
            await rig.Settle(60);

            AssertBool(float.IsFinite(rig.Door.Openness)).IsTrue();
            AssertFloat(rig.Door.Openness).IsEqualApprox(1.0f, 0.001f);
            AssertVector(rig.Door.Position).IsEqualApprox(rig.Door.Travel, Vector3.One * 0.01f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task Two_doors_that_behave_differently_are_the_same_class()
    {
        var rig = Rig();

        try
        {
            // E9 adds, moves and removes doors as mutations (arch §4.1). A sliding hatch and a
            // rising shutter differing by a subclass would make that a code change every time.
            var hatch = rig.Add("Hatch", new Vector3(1.4f, 0, 0), seconds: 0.2f);
            var shutter = rig.Add("Shutter", new Vector3(0, 3.0f, 0), seconds: 1.2f);

            hatch.Open();
            shutter.Open();
            await rig.Settle(30);

            AssertFloat(hatch.Openness).IsEqualApprox(1.0f, 0.001f);
            AssertFloat(shutter.Openness).IsLess(0.6f);
            AssertBool(hatch.GetType() == shutter.GetType()).IsTrue();
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_door_removed_mid_travel_takes_nothing_with_it()
    {
        var rig = Rig();

        try
        {
            var doomed = rig.Add("Doomed", new Vector3(0, 2.0f, 0), seconds: 1.0f);
            doomed.Open();
            await rig.Settle(10);

            // Removing one is what E9's mutation does, and it happens while the facility is
            // running rather than at a quiet moment.
            doomed.QueueFree();
            await rig.Settle(10);

            AssertBool(GodotObject.IsInstanceValid(doomed)).IsFalse();

            rig.Door.Open();
            await rig.Settle(60);
            AssertFloat(rig.Door.Openness).IsEqualApprox(1.0f, 0.001f);
        }
        finally
        {
            rig.Drop();
        }
    }

    private static DoorRig Rig()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "DoorRig" };
        tree.Root.AddChild(root);

        var rig = new DoorRig(root);
        return rig with { Door = rig.Add("Door", new Vector3(0, 2.2f, 0), seconds: 0.4f) };
    }

    private sealed record DoorRig(Node3D Root)
    {
        public Door Door { get; init; } = null!;

        public Door Add(string name, Vector3 travel, float seconds)
        {
            var door = new Door
            {
                Name = name,
                Travel = travel,
                Seconds = seconds,
                Opening = new Vector3(1.2f, 2.1f, 0.8f),
            };

            door.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(1.2f, 2.1f, 0.15f) },
            });

            Root.AddChild(door);
            return door;
        }

        public RigidBody3D Standing()
        {
            var body = new RigidBody3D { Name = "Standing", Freeze = true, Position = Vector3.Zero };
            body.AddChild(new CollisionShape3D
            {
                Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.3f },
            });

            Root.AddChild(body);
            return body;
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
