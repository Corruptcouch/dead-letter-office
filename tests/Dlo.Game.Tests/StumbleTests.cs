using System.Threading.Tasks;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E1-09. A stagger comes from the world, and it never takes the controls away.
/// </summary>
/// <remarks>
/// <b>The second criterion is the load-bearing one.</b> "Recovery is immediate and controllable" is
/// easy to satisfy on paper and easy to break in practice, because the obvious way to write a
/// stumble — a timer that ignores input while it runs — is unresponsive input in a costume, which
/// is the exact failure vision §3.1 names. So the assertions here are mostly about what a stagger
/// is <i>not</i> allowed to do.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class StumbleTests
{
    private const double Frame = 1.0 / 60.0;

    [TestCase]
    public async Task Nothing_staggers_a_body_the_world_leaves_alone()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);

            // Two seconds of walking across a flat floor. A random timer or a periodic "trip"
            // would show up here, and this is the assertion that says there is not one.
            for (var i = 0; i < 120; i++)
            {
                body.Step(Frame, new MoveIntent(new Vector2(0, 1), Jump: false, Crouch: false));
                await Physics(root);
                AssertFloat(body.Push.Length()).IsEqualApprox(0.0f, 0.0001f);
            }
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task Running_into_a_wall_staggers_the_body()
    {
        var (root, body) = Rig();

        try
        {
            Wall(root, at: new Vector3(0, 1, -2));
            await Ground(root, body);

            var staggered = false;
            for (var i = 0; i < 90 && !staggered; i++)
            {
                body.Step(Frame, new MoveIntent(new Vector2(0, 1), Jump: false, Crouch: false));
                await Physics(root);
                staggered = body.Push.Length() > 0.0f;
            }

            // Caused by the world: a wall took the speed, so the wall is what shoved back.
            AssertBool(staggered).IsTrue();

            // And backwards, away from what was hit - not sideways and not into it.
            AssertFloat(body.Push.Z).IsGreater(0.0f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task A_stagger_never_costs_the_player_any_authority()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);

            body.Stumble(new Vector3(3.0f, 0, 0));
            var push = body.Push;

            body.Step(Frame, new MoveIntent(new Vector2(0, 1), Jump: false, Crouch: false));

            // Full walk speed forward, PLUS the shove sideways. The input contribution is exactly
            // what it would be with no stagger at all: added to, never scaled by. A stumble that
            // multiplied input by 0.5 would fail here, and so would one that ignored it entirely.
            AssertFloat(body.Velocity.Z).IsEqualApprox(-body.Speed, 0.001f);
            AssertFloat(body.Velocity.X).IsEqualApprox(push.X, 0.001f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task A_stagger_can_be_steered_against_on_the_very_next_frame()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);
            body.Stumble(new Vector3(4.0f, 0, 0));

            // Pushing straight into the shove, immediately. There is no window to wait out.
            body.Step(Frame, new MoveIntent(new Vector2(-1, 0), Jump: false, Crouch: false));

            // The shove was 4 m/s and the walk is 4.5, so leaning against it wins on frame one.
            AssertFloat(body.Velocity.X).IsLess(0.0f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task A_stagger_is_spent_without_the_player_waiting_for_it()
    {
        var (root, body) = Rig();

        try
        {
            await Ground(root, body);
            body.Stumble(new Vector3(5.0f, 0, 0));

            // A quarter second, which is a stumble rather than a cutscene.
            for (var i = 0; i < 15; i++)
            {
                body.Step(Frame, new MoveIntent(Vector2.Zero, Jump: false, Crouch: false));
                await Physics(root);
            }

            AssertFloat(body.Push.Length()).IsLess(0.5f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void A_stagger_cannot_stagger_itself()
    {
        // A fresh impact staggers.
        AssertBool(PlayerCharacter.IsImpact(speedLost: 3.0f, threshold: 2.0f, pushInPlay: 0.0f))
            .IsTrue();

        // The same loss while a stagger is still being spent does not. Without this the body shoves
        // itself into the wall it just hit, loses speed to it again, and staggers off its own
        // stagger - a feedback loop that reads as the player being thrown across the room.
        AssertBool(PlayerCharacter.IsImpact(speedLost: 3.0f, threshold: 2.0f, pushInPlay: 2.5f))
            .IsFalse();

        // And an ordinary brush against a doorframe is not a trip at all.
        AssertBool(PlayerCharacter.IsImpact(speedLost: 0.4f, threshold: 2.0f, pushInPlay: 0.0f))
            .IsFalse();
    }

    private static async Task Ground(Node root, PlayerCharacter body)
    {
        for (var i = 0; i < 60 && !body.IsOnFloor(); i++)
        {
            body.Step(Frame, new MoveIntent(Vector2.Zero, Jump: false, Crouch: false));
            await Physics(root);
        }
    }

    private static async Task Physics(Node any)
    {
        var tree = any.GetTree();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static void Wall(Node parent, Vector3 at)
    {
        var wall = new StaticBody3D { Position = at };
        wall.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(10, 4, 0.5f) },
        });
        parent.AddChild(wall);
    }

    private static (Node3D Root, PlayerCharacter Body) Rig()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "StumbleRig" };
        tree.Root.AddChild(root);

        var floor = new StaticBody3D { Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(200, 1, 200) },
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

    private static void Drop(Node root)
    {
        root.GetParent().RemoveChild(root);
        root.Free();
    }
}
