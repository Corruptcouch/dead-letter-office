using System.Threading.Tasks;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E1-02. Move, look, jump and crouch land on the frame they are asked for, and nothing is
/// damped.
/// </summary>
/// <remarks>
/// <para>
/// <b>"0 frames of network wait" is measured here, not assumed</b> (arch §8). Every test in
/// this file runs with no multiplayer peer at all and asserts the result of a <i>single</i>
/// <see cref="PlayerCharacter.Step"/> or <see cref="PlayerCharacter.Look"/> call. If any of
/// these ever needed a second frame, or a peer, that is the regression.
/// </para>
/// <para>
/// <b>The damping tests are the load-bearing ones.</b> Arch §6.1 bans input damping outright
/// and names it as the specific mistake that makes this game read as broken rather than funny.
/// It is also the easiest thing in the build to add back by accident, because smoothing feels
/// like polish while it is doing the damage.
/// </para>
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class PlayerCharacterTests
{
    private const double Frame = 1.0 / 60.0;

    [TestCase]
    public void Look_applies_its_whole_delta_on_the_frame_it_arrives()
    {
        var (root, body) = Rig();

        try
        {
            var once = new Vector2(100, 0);

            body.Look(once);
            var afterOne = body.Yaw;

            body.Look(once);
            var afterTwo = body.Yaw;

            // Exactly twice as far, which is what "no smoothing" means as an assertion. Any
            // lerp, spring or acceleration curve makes the second call move less than the
            // first, and this is the number that catches it.
            AssertFloat(afterOne).IsEqualApprox(-100 * body.LookSensitivity, 0.0001f);
            AssertFloat(afterTwo).IsEqualApprox(2 * afterOne, 0.0001f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void Look_cannot_be_pitched_past_the_vertical()
    {
        var (root, body) = Rig();

        try
        {
            // Far past straight up. Exactly ±90° degenerates the forward vector and the symptom
            // is a camera that flips when a player looks straight down at the parcel they are
            // carrying - which is most of this game.
            body.Look(new Vector2(0, -100000));
            AssertFloat(body.Pitch).IsLess(Mathf.Pi / 2.0f);

            body.Look(new Vector2(0, 200000));
            AssertFloat(body.Pitch).IsGreater(-Mathf.Pi / 2.0f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task Movement_reaches_full_speed_on_the_first_frame()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);

            body.Step(Frame, new MoveIntent(Vector2.Up, Jump: false, Crouch: false));

            // Full speed immediately. An acceleration ramp here is the banned damping wearing
            // a physics costume: the parcel is supposed to be the awkward thing, never the
            // controller (arch §6.1).
            var speed = new Vector2(body.Velocity.X, body.Velocity.Z).Length();
            AssertFloat(speed).IsEqualApprox(body.Speed, 0.01f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task Releasing_the_key_stops_the_body_on_the_same_frame()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);
            body.Step(Frame, new MoveIntent(Vector2.Up, Jump: false, Crouch: false));

            body.Step(Frame, new MoveIntent(Vector2.Zero, Jump: false, Crouch: false));

            // The other half of "no damping", and the half that gets forgotten: a body that
            // accelerates instantly but glides to a halt is still damped, and still reads as
            // the controller arguing with the player.
            var speed = new Vector2(body.Velocity.X, body.Velocity.Z).Length();
            AssertFloat(speed).IsEqualApprox(0.0f, 0.01f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task Jump_leaves_the_floor_on_the_frame_the_button_goes_down()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);
            AssertBool(body.IsOnFloor()).IsTrue();

            body.Step(Frame, new MoveIntent(Vector2.Zero, Jump: true, Crouch: false));

            // One call, no peer, no second frame. This is arch §8's "grab → visible motion, 0
            // frames of network wait" applied to the verb that is easiest to measure.
            AssertFloat(body.Velocity.Y).IsGreater(0.0f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task Crouch_moves_the_head_in_one_step_and_back_again()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);
            AssertFloat(body.Head.Position.Y).IsEqualApprox(body.StandHeight, 0.001f);

            body.Step(Frame, new MoveIntent(Vector2.Zero, Jump: false, Crouch: true));
            AssertBool(body.IsCrouched).IsTrue();
            AssertFloat(body.Head.Position.Y).IsEqualApprox(body.CrouchHeight, 0.001f);

            // Both directions. A crouch that drops instantly and rises over a quarter second is
            // still damped input, and crouch-spam is a movement verb here rather than an
            // animation to be smoothed over.
            body.Step(Frame, new MoveIntent(Vector2.Zero, Jump: false, Crouch: false));
            AssertBool(body.IsCrouched).IsFalse();
            AssertFloat(body.Head.Position.Y).IsEqualApprox(body.StandHeight, 0.001f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task A_body_this_peer_does_not_own_is_not_driven_locally()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);

            // Gap 1's ruling, asserted: the owning peer drives its own body and nobody else
            // touches it. A peer that ran _PhysicsProcess on someone else's character would
            // have four machines fighting over one position, and the replicated transform
            // would lose to whichever one wrote last.
            body.SetMultiplayerAuthority(2);
            AssertBool(body.IsMultiplayerAuthority()).IsFalse();

            // Given a velocity first, and that is load bearing. With no keys held, a Step that
            // DID run would write back almost the velocity the body already had - so asserting
            // "nothing changed" from rest passes whether the guard is there or not. Verified by
            // deleting the guard and watching this test stay green. A sideways velocity is
            // something only a Step that ran would clear.
            body.Velocity = new Vector3(3, 0, 3);
            body._PhysicsProcess(Frame);

            AssertFloat(body.Velocity.X).IsEqual(3.0f);
            AssertFloat(body.Velocity.Z).IsEqual(3.0f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void A_mouse_motion_event_reaches_the_look()
    {
        var (root, body) = Rig();

        try
        {
            var motion = new InputEventMouseMotion { Relative = new Vector2(100, 0) };

            // Look is where the maths is asserted; this is the wire that carries a mouse to it.
            // Without it the tests above pass a body no player can turn.
            body._UnhandledInput(motion);

            AssertFloat(body.Yaw).IsEqualApprox(-100 * body.LookSensitivity, 0.0001f);
        }
        finally
        {
            Drop(root);
        }
    }

    private static (Node3D Root, PlayerCharacter Body) Rig()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "PlayerRig" };
        tree.Root.AddChild(root);

        var floor = new StaticBody3D { Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(50, 1, 50) },
        });
        root.AddChild(floor);

        var body = new PlayerCharacter { Name = "Player", Position = new Vector3(0, 1.0f, 0) };
        body.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.3f },
        });
        root.AddChild(body);

        return (root, body);
    }

    /// <summary>Drops the body onto the floor so <c>IsOnFloor</c> means something.</summary>
    private static async Task Ground(Node root, PlayerCharacter body)
    {
        var tree = root.GetTree();
        for (var i = 0; i < 60 && !body.IsOnFloor(); i++)
        {
            body.Step(Frame, new MoveIntent(Vector2.Zero, Jump: false, Crouch: false));
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }
    }

    private static void Drop(Node root)
    {
        root.GetParent().RemoveChild(root);
        root.QueueFree();
    }
}
