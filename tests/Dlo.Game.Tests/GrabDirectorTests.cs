using System.Linq;
using System.Threading.Tasks;

using Dlo.Domain;

using Dlo.Game.Carry;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E1-04, E1-07 and E1-08: the host's joint, what a throw costs, and why one person cannot carry
/// a two-person box.
/// </summary>
/// <remarks>
/// <b>With no peer at all, <c>Multiplayer.IsServer()</c> is true</b> (asserted in
/// <c>SessionRootTests</c>), so these run the host path. The other half — that a <i>client</i>
/// creates no joint — cannot be proved in one process, because one process has one physics world
/// (E0-08). That is E1-06's, at L3, and it is the same split E0-05 made.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class GrabDirectorTests
{
    private const long Host = 1;
    private const long Other = 2;
    private const float Mass = 50.0f;

    [TestCase]
    public async Task A_grab_in_reach_creates_exactly_one_joint_on_the_host()
    {
        var rig = Rig();

        try
        {
            AssertInt(Joints(rig.Director)).IsEqual(0);

            rig.Director.Grab(rig.Load);
            await rig.Frame();

            AssertInt(Joints(rig.Director)).IsEqual(1);
            AssertBool(rig.Director.HoldersOf(rig.Path).Contains(Host)).IsTrue();
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task The_grip_is_the_spring_E1_01_measured_and_never_a_pin()
    {
        var rig = Rig();

        try
        {
            rig.Director.Grab(rig.Load);
            await rig.Frame();

            var joint = Joint(rig.Director);

            // Not a PinJoint3D. Jolt does not implement its impulse_clamp - it logs that it is
            // ignoring the value - so a pin to a kinematic hand carries any mass weightlessly.
            AssertObject(joint).IsInstanceOf<Generic6DofJoint3D>();

            // The hard limit must be OFF, or it rather than the spring is what holds the load,
            // which is the same weightless case by another route.
            AssertBool(joint.GetFlagY(Generic6DofJoint3D.Flag.EnableLinearLimit)).IsFalse();
            AssertBool(joint.GetFlagY(Generic6DofJoint3D.Flag.EnableLinearSpring)).IsTrue();

            // E1-01's envelope, to the number: stiffness 100 x mass, damping at the floor of 100.
            AssertFloat(joint.GetParamY(Generic6DofJoint3D.Param.LinearSpringStiffness))
                .IsEqualApprox(GripSpring.StiffnessFor(Mass), 1.0f);
            AssertFloat(joint.GetParamY(Generic6DofJoint3D.Param.LinearSpringDamping))
                .IsEqualApprox(GripSpring.Damping, 1.0f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_grab_out_of_reach_is_refused_and_creates_no_joint()
    {
        var rig = Rig();

        try
        {
            GrabVerdict? refused = null;
            rig.Director.Denied += (_, verdict) => refused = verdict;

            rig.Load.GlobalPosition = new Vector3(0, 1.0f, -20.0f);
            rig.Director.Grab(rig.Load);
            await rig.Frame();

            AssertObject(refused).IsEqual(GrabVerdict.OutOfReach);
            AssertInt(Joints(rig.Director)).IsEqual(0);
            AssertInt(rig.Director.HoldersOf(rig.Path).Count).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_policy_locked_load_is_refused_and_creates_no_joint()
    {
        // Locked on the record, not on the node: the lock is a policy fact the host owns and
        // never publishes (arch §5.3), so a rig that set it on the body would be asserting
        // against a field that no longer exists anywhere real.
        var rig = Rig(locked: true);

        try
        {
            GrabVerdict? refused = null;
            rig.Director.Denied += (_, verdict) => refused = verdict;

            rig.Director.Grab(rig.Load);
            await rig.Frame();

            AssertObject(refused).IsEqual(GrabVerdict.Locked);
            AssertInt(Joints(rig.Director)).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_second_carrier_on_a_one_person_load_gets_nothing()
    {
        var rig = Rig();

        try
        {
            rig.Director.Grab(rig.Load);
            await rig.Frame();

            // A different peer reaches for the same box. Contention across processes is E1-06's;
            // this is the host-side rule that decides it.
            rig.Director.GrabFor(Other, rig.Path);
            await rig.Frame();

            AssertInt(Joints(rig.Director)).IsEqual(1);
            AssertBool(rig.Director.HoldersOf(rig.Path).Contains(Host)).IsTrue();
            AssertBool(rig.Director.HoldersOf(rig.Path).Contains(Other)).IsFalse();
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task Dropping_is_always_available()
    {
        var rig = Rig();

        try
        {
            rig.Director.Grab(rig.Load);
            await rig.Frame();

            rig.Director.Release(rig.Load);
            await rig.Frame();

            // No joint, no holder, and no state machine that could have said no. A player who
            // cannot let go is a player fighting the input (E1-07).
            AssertInt(Joints(rig.Director)).IsEqual(0);
            AssertInt(rig.Director.HoldersOf(rig.Path).Count).IsEqual(0);

            // And releasing again is not an error, because a double input never should be.
            rig.Director.Release(rig.Load);
            await rig.Frame();
            AssertInt(Joints(rig.Director)).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_heavier_parcel_is_a_worse_projectile_for_the_same_shove()
    {
        // Impulse over mass, so the same throw moves a heavy box less. No weight class, no lookup
        // table, no special case - which is the whole of E1-07's second criterion.
        var light = await ThrownSpeed(10.0f);
        var heavy = await ThrownSpeed(100.0f);

        AssertFloat(light).IsGreater(heavy * 2.0f);
        AssertFloat(heavy).IsGreater(0.0f);
    }

    [TestCase]
    public async Task One_carrier_cannot_lift_a_two_person_load_and_two_can()
    {
        // Measured from the floor up, which is the case the story describes: the box is on the
        // ground, you get a hand on it, and the question is whether it comes with you.
        var resting = await LiftedHeight(carriers: 2, grabbers: 0);
        var alone = await LiftedHeight(carriers: 2, grabbers: 1);
        var together = await LiftedHeight(carriers: 2, grabbers: 2);

        // One carrier gets a grip and the box does not leave the ground. Not a refused
        // interaction - they are holding it, it simply will not come.
        AssertFloat(alone).IsEqualApprox(resting, 0.02f);

        // The second carrier is what lifts it, on two host-owned joints (arch §3.3).
        AssertFloat(together).IsGreater(resting + 0.4f);
    }

    [TestCase]
    public async Task One_carrier_lifts_a_one_person_load_perfectly_well()
    {
        // The control. Without it, "it did not lift" would pass just as well if grabbing were
        // broken outright - which is exactly how a two-person carry test passes for free.
        var resting = await LiftedHeight(carriers: 1, grabbers: 0);
        var lifted = await LiftedHeight(carriers: 1, grabbers: 1);

        AssertFloat(lifted).IsGreater(resting + 0.4f);
    }

    [TestCase]
    public async Task When_one_of_two_carriers_lets_go_the_load_drops_rather_than_launching()
    {
        var rig = Rig(carriers: 2);

        try
        {
            rig.Director.Grab(rig.Load);
            rig.Director.GrabFor(Other, rig.Path);
            await rig.Settle(90);

            AssertInt(Joints(rig.Director)).IsEqual(2);

            rig.Director.ReleaseFor(Other, rig.Path);

            var fastest = 0.0f;
            for (var i = 0; i < 30; i++)
            {
                await rig.Frame();
                fastest = Mathf.Max(fastest, rig.Load.LinearVelocity.Length());
            }

            // E1-01 measured the difference: ~1.4 m/s at the reference stiffness is a drop, and
            // over 10 m/s in the over-stiff band is a launch. If this ever fails, the grip is too
            // stiff - it is not a bug in the release path.
            AssertFloat(fastest).IsLess(4.0f);

            // And it did not teleport to whoever is left holding it.
            AssertFloat(rig.Load.GlobalPosition.DistanceTo(rig.Carrier.GlobalPosition))
                .IsGreater(0.1f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_carrier_that_disconnects_drops_what_it_was_holding()
    {
        var rig = Rig();

        try
        {
            rig.Director.Grab(rig.Load);
            await rig.Frame();

            rig.Director.ForgetCarrier(Host);
            await rig.Frame();

            // Their held parcels drop; they do not stay frozen in a disconnected hand (E12-04).
            AssertInt(Joints(rig.Director)).IsEqual(0);
            AssertInt(rig.Director.HoldersOf(rig.Path).Count).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_granted_grab_tells_the_carrier_what_it_is_holding()
    {
        var rig = Rig();

        try
        {
            rig.Director.Grab(rig.Load);
            await rig.Frame();

            // Without this the weight never reaches the carrier: ApplyCarryPull returns on its
            // first line for a body that does not know it is holding anything (E1-07).
            AssertObject(rig.Carrier.Carried).IsSame(rig.Load);

            rig.Director.Release(rig.Load);
            await rig.Frame();

            AssertObject(rig.Carrier.Carried).IsNull();
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_peer_that_is_not_holding_a_load_cannot_throw_it()
    {
        var rig = Rig();

        try
        {
            await rig.Settle(2);
            var before = rig.Load.GlobalPosition;

            // No grab first. RequestThrow is an AnyPeer RPC, so this is the shape of a packet
            // any peer can send about any parcel it can name, and the release inside Hurl is a
            // no-op for a peer holding nothing - which used to leave the shove behind.
            rig.Director.Throw(rig.Load, new Vector3(0, 0, -200));
            await rig.Frame();

            // It may fall. It may not travel.
            AssertFloat(rig.Load.GlobalPosition.Z).IsEqualApprox(before.Z, 0.01f);
            AssertFloat(rig.Load.LinearVelocity.Z).IsEqualApprox(0.0f, 0.01f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_shove_past_the_cap_lands_at_the_cap()
    {
        var capped = await ThrownSpeed(Mass, GrabDirector.MaxThrowImpulse);
        var absurd = await ThrownSpeed(Mass, GrabDirector.MaxThrowImpulse * 1000.0f);

        // The same speed, not merely a bounded one: the magnitude arrives from a peer, and a cap
        // that only mostly held would still put a parcel through the far wall of the facility.
        // The tolerance is a frame of gravity and a released spring, not slack in the cap - a
        // thousand times the impulse is a thousand times the speed if this ever comes out.
        AssertFloat(absurd).IsEqualApprox(capped, 0.5f);
        AssertFloat(capped).IsGreater(0.0f);
    }

    [TestCase]
    public async Task A_throw_that_is_not_a_number_is_refused_rather_than_clamped()
    {
        var rig = Rig();

        try
        {
            rig.Director.Grab(rig.Load);
            await rig.Frame();

            rig.Director.Throw(rig.Load, new Vector3(0, 0, float.NaN));
            await rig.Settle(5);

            // Not a shove that was too big. A NaN impulse takes the body out of the simulation
            // on every peer, and nothing anywhere reports it - so the whole call is refused,
            // release included, rather than half-applied.
            AssertBool(rig.Load.GlobalPosition.IsFinite()).IsTrue();
            AssertBool(rig.Load.LinearVelocity.IsFinite()).IsTrue();
            AssertInt(rig.Director.HoldersOf(rig.Path).Count).IsEqual(1);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_refused_grab_leaves_no_bookkeeping_behind()
    {
        var rig = Rig();

        try
        {
            // A granted grab first, so the zero below is a real absence rather than a property
            // that reads zero whatever happens (standards §8: a test that cannot fail).
            rig.Director.Grab(rig.Load);
            await rig.Frame();
            AssertInt(rig.Director.Tracked).IsEqual(1);

            rig.Director.Release(rig.Load);
            await rig.Frame();
            AssertInt(rig.Director.Tracked).IsEqual(0);

            rig.Load.GlobalPosition = new Vector3(0, 1.0f, -20.0f);

            for (var i = 0; i < 5; i++)
            {
                rig.Director.Grab(rig.Load);
                await rig.Frame();
            }

            // A missed grab is the common case at a belt that never stops, and an entry made
            // before the verdict is one nothing ever removes.
            AssertInt(rig.Director.Tracked).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task Ending_a_session_frees_every_grip_and_forgets_every_carrier()
    {
        var rig = Rig();

        try
        {
            rig.Director.Grab(rig.Load);
            await rig.Frame();
            AssertInt(Joints(rig.Director)).IsEqual(1);

            rig.Director.ResetSession();
            await rig.Frame();

            // This node is a child of an autoload and outlives the session that filled it, so
            // without this a rehost starts holding the last session's parcel on a live joint.
            AssertInt(Joints(rig.Director)).IsEqual(0);
            AssertInt(rig.Director.Tracked).IsEqual(0);
            AssertInt(rig.Director.HoldersOf(rig.Path).Count).IsEqual(0);
            AssertObject(rig.Director.Parcels).IsNull();

            // The carriers go too: a grab afterwards finds nobody to attach to, rather than
            // attaching to a body the session before it registered.
            rig.Director.GrabFor(Host, rig.Path);
            await rig.Frame();
            AssertInt(Joints(rig.Director)).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    private static async Task<float> ThrownSpeed(float mass, float impulse = 200.0f)
    {
        var rig = Rig(mass: mass);

        try
        {
            rig.Director.Grab(rig.Load);
            await rig.Frame();

            rig.Director.Throw(rig.Load, new Vector3(0, 0, -impulse));
            await rig.Frame();

            return rig.Load.LinearVelocity.Length();
        }
        finally
        {
            rig.Drop();
        }
    }

    /// <summary>
    /// Rests a load on the floor, lets <paramref name="grabbers"/> carriers take hold, and reports
    /// how high it ends up.
    /// </summary>
    private static async Task<float> LiftedHeight(int carriers, int grabbers)
    {
        var rig = Rig(carriers: carriers, restingOnFloor: true);

        try
        {
            // Settled first, so "it did not rise" is measured against a box genuinely at rest
            // rather than one still falling.
            await rig.Settle(60);

            if (grabbers > 0)
            {
                rig.Director.Grab(rig.Load);
            }

            if (grabbers > 1)
            {
                rig.Director.GrabFor(Other, rig.Path);
            }

            await rig.Settle(120);
            return rig.Load.GlobalPosition.Y;
        }
        finally
        {
            rig.Drop();
        }
    }

    private static int Joints(Node parent)
    {
        var found = 0;
        foreach (var child in parent.GetChildren())
        {
            // A released grip is QueueFree'd, so it lingers in the child list until the end of
            // the frame. Counting it would make every release look like it did nothing.
            if (child is Generic6DofJoint3D joint && !joint.IsQueuedForDeletion())
            {
                found++;
            }
        }

        return found;
    }

    private static Generic6DofJoint3D Joint(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Generic6DofJoint3D joint)
            {
                return joint;
            }
        }

        throw new System.InvalidOperationException("No joint was created.");
    }

    private static GrabRig Rig(
        int carriers = 1, float mass = Mass, bool restingOnFloor = false, bool locked = false)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "GrabRig" };
        tree.Root.AddChild(root);

        var floor = new StaticBody3D { Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(50, 1, 50) },
        });
        root.AddChild(floor);

        var director = new GrabDirector { Name = GrabDirector.NodeName };
        root.AddChild(director);

        var carrier = Carrier(root, "Carrier", new Vector3(-0.6f, 1.0f, 0));
        var second = Carrier(root, "Second", new Vector3(0.6f, 1.0f, 0));
        director.RegisterCarrier(Host, carrier);
        director.RegisterCarrier(Other, second);

        // The host resolves capacity and the lock out of the registry (E2-01), so the rig is
        // shaped like a real host session: a record that owns both, and a node that views it.
        var parcels = new ParcelRegistry();
        var record = parcels.Register(
            archetype: 0,
            size: carriers >= 2 ? ParcelRecord.TwoPersonSize : (byte)1,
            condition: 0,
            isLocked: locked);
        director.Parcels = parcels;

        var load = new Carryable
        {
            Name = "Load",
            Mass = mass,
            Id = record.Id,
            Size = record.Size,
            Position = new Vector3(0, restingOnFloor ? 0.41f : 1.2f, 0),
        };
        load.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.2f, 0.8f, 0.8f) },
        });
        root.AddChild(load);

        return new GrabRig(root, director, carrier, load);
    }

    private static PlayerCharacter Carrier(Node parent, string name, Vector3 at)
    {
        var body = new PlayerCharacter { Name = name, Position = at };
        body.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.3f },
        });
        parent.AddChild(body);
        return body;
    }

    private sealed record GrabRig(
        Node3D Root, GrabDirector Director, PlayerCharacter Carrier, Carryable Load)
    {
        public string Path => Load.GetPath().ToString();

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
