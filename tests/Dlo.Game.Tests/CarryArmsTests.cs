using Dlo.Game.Carry;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E1-03. The arms are Godot's own IK, they reach the grip point, and no byte of hand pose ever
/// goes on the wire.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CarryArmsTests
{
    [TestCase]
    public void The_solver_is_Godots_own_two_bone_ik_rather_than_hand_written_arm_code()
    {
        var (root, arms, _) = Rig();

        try
        {
            // `AGENTS.md` rung 3: a native feature covers this, so no procedural elbow solve gets
            // written. If this ever becomes our own maths, this is the assertion that should have
            // stopped it.
            AssertObject(arms.Solver).IsNotNull();
            AssertObject(arms.Solver).IsInstanceOf<TwoBoneIK3D>();
            AssertObject(arms.Solver).IsInstanceOf<SkeletonModifier3D>();

            // Two settings, one per arm, on one modifier.
            AssertInt(arms.Solver.SettingCount).IsEqual(2);

            // A SkeletonModifier3D only runs as a child of the skeleton it drives. Parented
            // anywhere else it silently does nothing at all.
            AssertObject(arms.Solver.GetParent()).IsInstanceOf<Skeleton3D>();
            AssertObject(arms.Solver.GetSkeleton()).IsNotNull();
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void Both_hands_reach_the_grip_point_of_a_held_load()
    {
        var (root, arms, load) = Rig();

        try
        {
            arms.Reach(load, 0);

            var grip = load.GlobalGrip(0);

            // Straddling the grip, not buried in it: two hands at the same point read as one hand.
            AssertFloat(arms.LeftTarget.DistanceTo(grip)).IsLessEqual(arms.ShoulderWidth + 0.001f);
            AssertFloat(arms.RightTarget.DistanceTo(grip)).IsLessEqual(arms.ShoulderWidth + 0.001f);
            AssertFloat(arms.LeftTarget.DistanceTo(arms.RightTarget))
                .IsEqualApprox(arms.ShoulderWidth * 2, 0.001f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void Hand_targets_follow_the_load_rather_than_being_told_where_to_go()
    {
        var (root, arms, load) = Rig();

        try
        {
            arms.Reach(load, 0);
            var before = arms.LeftTarget;

            // The load moves. Nothing tells the arms anything; they are re-derived from it.
            load.GlobalPosition += new Vector3(1.5f, 0.4f, 0);
            arms.Reach(load, 0);

            AssertFloat(arms.LeftTarget.DistanceTo(before)).IsGreater(1.0f);
            AssertFloat(arms.LeftTarget.DistanceTo(load.GlobalGrip(0)))
                .IsLessEqual(arms.ShoulderWidth + 0.001f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void Letting_go_returns_the_hands_to_a_rest_pose()
    {
        var (root, arms, load) = Rig();

        try
        {
            arms.Reach(load, 0);
            var held = arms.LeftTarget;

            arms.Reach(null, 0);

            // Back to a pose relative to the BODY, not left hanging where the parcel was. A stuck
            // hand is what E1-05's rollback looks like when it goes wrong.
            AssertFloat(arms.LeftTarget.DistanceTo(held)).IsGreater(0.2f);

            // The exact contract, rather than a distance that happens to be far enough: the rest
            // pose is arms-relative, so it must land there whatever the load was doing.
            var rest = arms.ToGlobal(arms.RestPose + new Vector3(-arms.ShoulderWidth, 0, 0));
            AssertFloat(arms.LeftTarget.DistanceTo(rest)).IsLess(0.001f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void The_two_carrier_slots_of_a_shared_load_are_different_places()
    {
        var (root, arms, load) = Rig();

        try
        {
            load.CarriersRequired = 2;

            arms.Reach(load, 0);
            var first = arms.LeftTarget;

            arms.Reach(load, 1);
            var second = arms.LeftTarget;

            // Two carriers on one box hold opposite ends (E1-08). If both slots resolved to the
            // same point, a co-op carry would put two players inside each other.
            AssertFloat(first.DistanceTo(second)).IsEqualApprox(load.GripHalfWidth * 2, 0.01f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void Nothing_in_the_arms_replicates_anything()
    {
        var (root, arms, _) = Rig();

        try
        {
            // The load-bearing negative assertion of E1-03. Hands move every frame, so a
            // synchronizer added here would be a per-frame stream from four peers that nobody
            // would notice until the budget in arch §8 was already gone. Pose is DERIVED - every
            // peer runs Reach from the holder map and the load's transform, and sends nothing.
            AssertInt(Synchronizers(arms)).IsEqual(0);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void Two_peers_arms_reach_the_same_place_from_the_same_facts()
    {
        var (root, arms, load) = Rig();

        try
        {
            // A second set of arms standing somewhere else entirely, as a second peer's copy of the
            // holder would be.
            var other = new CarryArms { Name = "Arms2", Position = new Vector3(9, 3, -7) };
            root.AddChild(other);

            arms.Reach(load, 0);
            other.Reach(load, 0);

            // E1-03's "on every peer, not only the holder's", as an assertion rather than an
            // inference. Hand pose is DERIVED from the load and the slot, so two peers holding the
            // same two facts must land in the same place - and the holder map they read the slot
            // from is proved identical across four processes by E1-06's contention run. Any local
            // state creeping into the pose (the owning body, a camera, the peer id) breaks this.
            AssertFloat(arms.LeftTarget.DistanceTo(other.LeftTarget)).IsLess(0.0001f);
            AssertFloat(arms.RightTarget.DistanceTo(other.RightTarget)).IsLess(0.0001f);
        }
        finally
        {
            Drop(root);
        }
    }

    private static int Synchronizers(Node node)
    {
        var found = node is MultiplayerSynchronizer ? 1 : 0;
        foreach (var child in node.GetChildren())
        {
            found += Synchronizers(child);
        }

        return found;
    }

    private static (Node3D Root, CarryArms Arms, Carryable Load) Rig()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "ArmsRig" };
        tree.Root.AddChild(root);

        var arms = new CarryArms { Name = "Arms", Position = new Vector3(0, 1.4f, 0) };
        root.AddChild(arms);

        var load = new Carryable { Name = "Load", Position = new Vector3(0, 1.2f, -0.6f) };
        load.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.2f, 0.8f, 0.8f) },
        });
        root.AddChild(load);

        return (root, arms, load);
    }

    /// <remarks>
    /// <c>Free</c> rather than <c>QueueFree</c>: a queued free happens at the end of a frame, and a
    /// synchronous test ends first — so GdUnit4 counts the whole subtree as orphaned and the
    /// warning drowns out a real leak if one ever appears.
    /// </remarks>
    private static void Drop(Node root)
    {
        root.GetParent().RemoveChild(root);
        root.Free();
    }
}
