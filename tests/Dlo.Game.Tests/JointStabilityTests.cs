using System.Threading.Tasks;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E1-01's regression scene. The spike measured where a two-person carry stays stable; this is
/// the check that keeps that measurement true.
/// </summary>
/// <remarks>
/// <b>What the spike found, in one line:</b> nothing exploded, jittered or tunnelled at any mass to
/// 500 kg at Jolt's default solver settings — but a <i>rigid</i> joint to a kinematic hand expresses
/// no weight at all, because the hand is immovable and the box simply follows it. Weight has to come
/// from joint compliance, and the only compliance Jolt honours is a
/// <see cref="Generic6DofJoint3D"/> linear spring: <c>PinJoint3D.impulse_clamp</c> is unimplemented
/// and logged as ignored. The full finding, with the envelope and the release-speed consequence for
/// E1-08, is in arch §11.
/// <para>
/// <b>The solver settings are asserted here</b> because every number in that finding is relative to
/// <c>velocity_steps = 10</c>, <c>position_steps = 2</c> at 60 Hz. Nothing sets them, so this
/// asserts Jolt's defaults — if somebody tunes the solver to fix an unrelated problem, the envelope
/// stops being the envelope, and that is the change this file exists to catch.
/// </para>
/// <para>
/// A physics-behaviour check, so it costs seconds rather than milliseconds (standards §8 budgets
/// seconds for L2): the failure it looks for takes frames to develop.
/// </para>
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class JointStabilityTests
{
    /// <summary>The reference carry: a two-person parcel, held at the stiffness the spike chose.</summary>
    /// <remarks>
    /// 50 kg at stiffness 5000 sits in the middle of the measured usable band — heavy enough to
    /// need two carriers, soft enough to visibly sag, stiff enough not to oscillate. The spike's
    /// rule of thumb is <c>stiffness ≈ 100 × mass</c>; this is that rule at its reference point.
    /// </remarks>
    private const float Mass = 50.0f;
    private const float Stiffness = 5000.0f;
    private const float Damping = 100.0f;

    private const float Grip = 0.6f;
    private const float StartY = 1.2f;

    // 90, matching the spike exactly. At this stiffness the parcel rings at about 1.6 Hz with
    // a damping ratio near 0.1, so it is still visibly decaying at frame 60 - measured 0.012 m
    // peak-to-peak there against 0.000 m at frame 90. A shorter settle would measure the
    // transient rather than the convergence, and would not be comparable to the finding.
    private const int SettleFrames = 90;
    private const int CarryFrames = 60;

    [TestCase]
    public void Jolt_solver_defaults_are_what_the_carry_envelope_was_measured_against()
    {
        // One assertion per fact (standards §8): these fail for four different reasons, and
        // "the physics engine changed" and "somebody raised the iteration count" want very
        // different fixes.
        AssertString(ProjectSettings.GetSetting("physics/3d/physics_engine").AsString())
            .IsEqual("Jolt Physics");

        AssertInt(ProjectSettings
            .GetSetting("physics/jolt_physics_3d/simulation/velocity_steps").AsInt32())
            .IsEqual(10);

        AssertInt(ProjectSettings
            .GetSetting("physics/jolt_physics_3d/simulation/position_steps").AsInt32())
            .IsEqual(2);

        AssertInt(Engine.PhysicsTicksPerSecond).IsEqual(60);
    }

    [TestCase]
    public async Task Two_joints_hold_a_heavy_parcel_without_exploding_jittering_or_tunnelling()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var scene = AutoFree(new Node3D())!;
        tree.Root.AddChild(scene);

        try
        {
            scene.AddChild(Floor());

            var box = Parcel();
            scene.AddChild(box);

            var left = Hand(new Vector3(-Grip, StartY, 0));
            var right = Hand(new Vector3(Grip, StartY, 0));
            scene.AddChild(left);
            scene.AddChild(right);

            Grasp(scene, left, box, new Vector3(-Grip, StartY, 0));
            Grasp(scene, right, box, new Vector3(Grip, StartY, 0));

            // Phase 1 — settle, with the carriers standing still.
            //
            // Jitter is measured HERE and not during the walk, which is the same place the
            // spike measured it. With nothing driving the parcel, any movement left in it is
            // the solver failing to converge. During a walk there is a real 1.6 Hz sway of
            // about 2 cm at this stiffness — the parcel bobbing as it is carried, which is the
            // weight the design wants rather than a fault, and measuring jitter through it
            // would turn this assertion into a test of that sway.
            var low = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var high = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var settled = 0.0f;

            for (var i = 0; i < SettleFrames; i++)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                settled = Mathf.Max(settled, box.LinearVelocity.Length());

                // The last half-second only, which is the window the spike sampled. Starting
                // earlier catches the tail of the decay rather than the converged state, and
                // the threshold below is quoted against the spike's number.
                if (i < SettleFrames - 30)
                {
                    continue;
                }

                var offset = box.GlobalPosition - Centre(left, right);
                low = low.Min(offset);
                high = high.Max(offset);
            }

            // Phase 2 — walk. Both carriers move together at walking pace, which is the case a
            // playtest actually produces and the one an unstable solver diverges in.
            for (var i = 0; i < CarryFrames; i++)
            {
                left.Position += new Vector3(1.5f / 60.0f, 0, 0);
                right.Position += new Vector3(1.5f / 60.0f, 0, 0);
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                settled = Mathf.Max(settled, box.LinearVelocity.Length());
            }

            var carried = box.GlobalPosition;
            var centre = Centre(left, right);

            // Explosion. Jolt clamps at 500 m/s, so a genuine blow-up shows up as a speed far
            // outside anything a 1.5 m/s carry could produce, not as a NaN.
            AssertFloat(settled).IsLess(20.0f);
            AssertBool(float.IsFinite(carried.X) && float.IsFinite(carried.Y)).IsTrue();

            // Tunnelling. The floor's top face is y = 0 and the box is 0.8 tall, so anything
            // below 0.2 has gone through something.
            AssertFloat(carried.Y).IsGreater(0.2f);

            // Still held. Measured settle sag at this configuration was 0.047 m; 0.25 m is far
            // enough above it to ignore normal variation and far below "it fell out of their
            // hands", which is what a broken constraint looks like.
            AssertFloat(carried.DistanceTo(centre)).IsLess(0.25f);

            // Suspended, not resting. A parcel that fell settles at y = 0.394 on this floor,
            // and a carry that walks back over it would still satisfy the distance check above.
            // This is the assertion that caught exactly that while the joints were unconnected.
            AssertFloat(carried.Y).IsGreater(0.9f);

            // Jitter, at rest. The spike measured 0.0000 m peak-to-peak at this configuration
            // and drew the line at 0.01 m; the softer settings it rejected produced 0.014 to
            // 0.24 m. 0.005 m sits well above the measurement and well below anything rejected.
            AssertFloat((high - low).Length()).IsLess(0.005f);
        }
        finally
        {
            tree.Root.RemoveChild(scene);
        }
    }

    private static Vector3 Centre(Node3D a, Node3D b) =>
        (a.GlobalPosition + b.GlobalPosition) * 0.5f;

    private static StaticBody3D Floor()
    {
        var floor = new StaticBody3D { Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(50, 1, 50) } });
        return floor;
    }

    private static RigidBody3D Parcel()
    {
        var box = new RigidBody3D { Mass = Mass, Position = new Vector3(0, StartY, 0) };
        box.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.2f, 0.8f, 0.8f) },
        });
        return box;
    }

    /// <summary>
    /// A carrier's hand: kinematic, because a character drives it rather than physics.
    /// </summary>
    private static AnimatableBody3D Hand(Vector3 at)
    {
        var hand = new AnimatableBody3D { Position = at, SyncToPhysics = false };
        hand.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.05f } });
        return hand;
    }

    /// <summary>
    /// One carrier's grip: a sprung 6DOF joint, which is the only compliance Jolt honours.
    /// </summary>
    /// <remarks>
    /// <b>The joint is put in the tree before its ends are named, and that ordering is load
    /// bearing.</b> <c>NodeA</c> and <c>NodeB</c> are <see cref="NodePath"/>s resolved relative
    /// to the joint, so computing them while the joint is still detached yields paths that
    /// resolve to nothing — Jolt then holds neither body and the parcel simply falls. It costs
    /// no error and no warning; the only symptom is a parcel on the floor.
    /// </remarks>
    private static void Grasp(Node parent, Node3D hand, Node3D box, Vector3 at)
    {
        var joint = new Generic6DofJoint3D { Position = at };
        parent.AddChild(joint);

        // The hard limit has to go, or it rather than the spring is what holds the parcel —
        // and a hard limit is exactly the weightless case the spike ruled out.
        joint.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearLimit, false);
        joint.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearLimit, false);
        joint.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearLimit, false);

        joint.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearSpring, true);
        joint.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearSpring, true);
        joint.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearSpring, true);

        foreach (var param in new[]
        {
            (Generic6DofJoint3D.Param.LinearSpringStiffness, Stiffness),
            (Generic6DofJoint3D.Param.LinearSpringDamping, Damping),
            (Generic6DofJoint3D.Param.LinearSpringEquilibriumPoint, 0.0f),
        })
        {
            joint.SetParamX(param.Item1, param.Item2);
            joint.SetParamY(param.Item1, param.Item2);
            joint.SetParamZ(param.Item1, param.Item2);
        }

        joint.NodeA = joint.GetPathTo(hand);
        joint.NodeB = joint.GetPathTo(box);
    }
}
