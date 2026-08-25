using System.Threading.Tasks;

using Dlo.Game.Carry;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E1-07's first criterion: a bulky load obstructs vision and movement <b>through its own geometry
/// and mass</b>, never through an input modifier.
/// </summary>
/// <remarks>
/// The distinction is the whole story. A carried box that quietly halved <c>Speed</c> would produce
/// the same slower player and would be the exact mistake arch §6.1 bans — so these tests assert
/// that the cost arrives as force on the body while <c>Speed</c> is left alone.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class CarryWeightTests
{
    private const double Frame = 1.0 / 60.0;

    [TestCase]
    public void A_load_resting_in_the_hands_costs_its_carrier_nothing()
    {
        // No lag, no pull. Standing still holding a piano is free, which is correct: it is the
        // accelerating and the turning that a heavy box makes hard.
        var pull = PlayerCharacter.CarryPull(
            gripTarget: new Vector3(0, 1.4f, -0.5f),
            loadAt: new Vector3(0, 1.4f, -0.5f),
            loadMass: 200.0f,
            carrierMass: 80.0f);

        AssertFloat(pull.Length()).IsEqualApprox(0.0f, 0.0001f);
    }

    [TestCase]
    public void Gravity_sag_is_not_charged_as_a_pull()
    {
        // E1-01 measured ~5 cm of constant sag at the reference stiffness. That is the load hanging,
        // not the load dragging, and billing the carrier for it would be a permanent downward tug.
        var pull = PlayerCharacter.CarryPull(
            gripTarget: new Vector3(0, 1.4f, -0.5f),
            loadAt: new Vector3(0, 1.35f, -0.5f),
            loadMass: 200.0f,
            carrierMass: 80.0f);

        AssertFloat(pull.Length()).IsEqualApprox(0.0f, 0.0001f);
    }

    [TestCase]
    public void A_load_left_behind_pulls_its_carrier_back()
    {
        // The box lags 20 cm behind the hand, which is what accelerating away from it looks like.
        var pull = PlayerCharacter.CarryPull(
            gripTarget: new Vector3(0, 1.4f, -0.5f),
            loadAt: new Vector3(0, 1.4f, -0.3f),
            loadMass: 50.0f,
            carrierMass: 80.0f);

        // Backwards, along the lag, and nowhere else.
        AssertFloat(pull.Z).IsGreater(0.0f);
        AssertFloat(pull.X).IsEqualApprox(0.0f, 0.0001f);
        AssertFloat(pull.Y).IsEqualApprox(0.0f, 0.0001f);
    }

    [TestCase]
    public void A_heavier_load_pulls_harder_for_exactly_the_same_lag()
    {
        var grip = new Vector3(0, 1.4f, -0.5f);
        var lagging = new Vector3(0, 1.4f, -0.3f);

        var light = PlayerCharacter.CarryPull(grip, lagging, loadMass: 10.0f, carrierMass: 80.0f);
        var heavy = PlayerCharacter.CarryPull(grip, lagging, loadMass: 100.0f, carrierMass: 80.0f);

        // Ten times the mass, ten times the pull, because the grip stiffness is 100 x mass. No
        // weight class and no lookup table - a heavy parcel is hard to carry for the same reason
        // it is a bad projectile (E1-07).
        AssertFloat(heavy.Length()).IsEqualApprox(light.Length() * 10.0f, 0.01f);
    }

    [TestCase]
    public async Task A_carried_load_slows_a_sprint_without_touching_the_walk_speed()
    {
        var (root, body, load) = Rig();

        try
        {
            var free = await Travelled(body, load: null);
            var laden = await Travelled(body, load);

            // The load costs real ground.
            AssertFloat(laden).IsLess(free);

            // And Speed is untouched, which is the criterion that separates this from the banned
            // input modifier. If a future change makes the box cheaper by editing this number, this
            // is the assertion that should stop it.
            AssertFloat(body.Speed).IsEqualApprox(4.5f, 0.0001f);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task A_bulky_load_is_in_the_way_of_its_carriers_own_eyes()
    {
        var (root, body, load) = Rig();

        try
        {
            // Put the box where a carried box goes, and look forward from the head.
            load.GlobalPosition = body.Anchor.GlobalPosition;
            await Physics(root);

            var head = body.Head.GlobalPosition;
            var query = PhysicsRayQueryParameters3D.Create(head, head + (body.Head.GlobalBasis.Z * -2.0f));
            query.Exclude = [body.GetRid()];

            var hit = body.GetWorld3D().DirectSpaceState.IntersectRay(query);

            // Obstruction by geometry: the box is simply there, in front of the eyes. Nothing
            // fades the screen, nothing narrows the FOV, nothing is subtracted from look input.
            AssertBool(hit.Count > 0).IsTrue();
            AssertObject(hit["collider"].As<Node>()).IsEqual(load);
        }
        finally
        {
            Drop(root);
        }
    }

    private static async Task<float> Travelled(PlayerCharacter body, Carryable? load)
    {
        var root = body.GetParent();
        body.Carried = load;
        body.GlobalPosition = new Vector3(0, 1.0f, 0);
        body.Velocity = Vector3.Zero;

        // Parked where a carried box hangs, then held there while the carrier runs off - which
        // is exactly the lag a spring turns into a pull.
        Park(load, body.Anchor.GlobalPosition);

        var from = body.GlobalPosition;
        for (var i = 0; i < 30; i++)
        {
            body.ApplyCarryPull(Frame);
            body.Step(Frame, new MoveIntent(new Vector2(0, 1), Jump: false, Crouch: false));
            await Physics(root);
        }

        body.Carried = null;
        return body.GlobalPosition.DistanceTo(from);
    }

    /// <remarks>
    /// A guard clause rather than an <c>if</c> around the assignment: <c>dotnet format</c> reports
    /// IDE0031 on the wrapped form and then "fixes" it to <c>load?.GlobalPosition = ...</c>, which
    /// does not compile — a property cannot be assigned through a null-conditional.
    /// </remarks>
    private static void Park(Carryable? load, Vector3 at)
    {
        if (load is null)
        {
            return;
        }

        load.GlobalPosition = at;
    }

    private static async Task Physics(Node any)
    {
        var tree = any.GetTree();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
    }

    private static (Node3D Root, PlayerCharacter Body, Carryable Load) Rig()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "CarryRig" };
        tree.Root.AddChild(root);

        var floor = new StaticBody3D { Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(200, 1, 200) },
        });
        root.AddChild(floor);

        var body = new PlayerCharacter { Name = "Carrier", Position = new Vector3(0, 1.0f, 0) };
        body.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.3f },
        });
        root.AddChild(body);

        var load = new Carryable { Name = "Load", Mass = 80.0f, Position = new Vector3(0, 1.2f, -2) };
        load.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.2f, 0.8f, 0.8f) },
        });
        root.AddChild(load);

        return (root, body, load);
    }

    private static void Drop(Node root)
    {
        root.GetParent().RemoveChild(root);
        root.Free();
    }
}
