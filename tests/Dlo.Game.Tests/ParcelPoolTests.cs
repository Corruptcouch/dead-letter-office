using Dlo.Domain;
using Dlo.Game.Carry;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E2-06. Parcels are the highest-churn object in the game and the belt never stops (vision §2),
/// so bodies are recycled — which is only safe because identity is not on them (arch §5.1).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ParcelPoolTests
{
    [TestCase]
    public void A_recycled_body_carries_nothing_over_from_the_parcel_it_was_showing()
    {
        var (root, pool) = Rig();

        try
        {
            var first = pool.Acquire(new ParcelSpawnArgs(Id: 1, Archetype: 9, Size: 4, Condition: 200));

            // Every mutable field, mutated, before release — the criterion is explicit that this
            // is proved by dirtying the node rather than by reading Clear and being satisfied.
            first.GripHalfWidth = 3.5f;
            first.LinearVelocity = new Vector3(7, 7, 7);
            first.AngularVelocity = new Vector3(2, 2, 2);
            first.GlobalPosition = new Vector3(40, 40, 40);
            first.Freeze = true;
            first.Visual.Position = new Vector3(5, 5, 5);

            pool.Release(first);
            var second = pool.Acquire(new ParcelSpawnArgs(Id: 2, Archetype: 0, Size: 1, Condition: 0));

            // The same body — that is the point of a pool — showing none of the last parcel.
            AssertBool(ReferenceEquals(first, second)).IsTrue();
            AssertInt((int)second.Id.Value).IsEqual(2);
            AssertInt(second.Archetype).IsEqual(0);
            AssertInt(second.Size).IsEqual(1);
            AssertInt(second.Condition).IsEqual(0);
            AssertInt(second.CarriersRequired).IsEqual(1);
            AssertFloat(second.GripHalfWidth).IsEqual(0.5f);
            AssertVector(second.LinearVelocity).IsEqual(Vector3.Zero);
            AssertVector(second.AngularVelocity).IsEqual(Vector3.Zero);
            AssertVector(second.GlobalPosition).IsEqual(Vector3.Zero);
            AssertBool(second.Freeze).IsFalse();
            AssertVector(second.Visual.Position).IsEqual(Vector3.Zero);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void A_released_body_is_parked_and_not_freed()
    {
        var (root, pool) = Rig();

        try
        {
            var parcel = pool.Acquire(new ParcelSpawnArgs(1, 0, 1, 0));
            pool.Release(parcel);

            // Standards §10: pooled, never QueueFree'd. A freed body is the churn this exists to
            // avoid, and it would take the next frame's allocation with it.
            AssertBool(GodotObject.IsInstanceValid(parcel)).IsTrue();
            AssertInt(pool.Idle).IsEqual(1);
            AssertInt(pool.Made).IsEqual(1);
            AssertBool(parcel.Visible).IsFalse();
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void Releasing_the_same_body_twice_does_not_hand_it_out_twice()
    {
        var (root, pool) = Rig();

        try
        {
            var parcel = pool.Acquire(new ParcelSpawnArgs(1, 0, 1, 0));
            pool.Release(parcel);
            pool.Release(parcel);

            AssertInt(pool.Idle).IsEqual(1);

            var a = pool.Acquire(new ParcelSpawnArgs(2, 0, 1, 0));
            var b = pool.Acquire(new ParcelSpawnArgs(3, 0, 1, 0));

            // One parcel in two places is the failure mode, and a stack that held the same body
            // twice would produce it silently.
            AssertBool(ReferenceEquals(a, b)).IsFalse();
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void A_body_released_from_the_world_is_taken_back_under_the_pool()
    {
        var (root, pool) = Rig();

        try
        {
            var parcel = pool.Acquire(new ParcelSpawnArgs(1, 0, 1, 0));
            parcel.Reparent(root);
            AssertBool(parcel.GetParent() == pool).IsFalse();

            pool.Release(parcel);

            AssertBool(parcel.GetParent() == pool).IsTrue();
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void The_pool_stops_at_its_cap_rather_than_growing_quietly()
    {
        var (root, pool) = Rig();

        try
        {
            pool.MaxParcels = 2;
            pool.Acquire(new ParcelSpawnArgs(1, 0, 1, 0));
            pool.Acquire(new ParcelSpawnArgs(2, 0, 1, 0));

            // Bounded, loudly. Growth is also printed as it happens, which is the half of the
            // criterion a test cannot see; the cap is the half it can.
            AssertThrown(() => pool.Acquire(new ParcelSpawnArgs(3, 0, 1, 0)))
                .IsInstanceOf<System.InvalidOperationException>();

            AssertInt(pool.Made).IsEqual(2);
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void Recycling_beats_growing()
    {
        var (root, pool) = Rig();

        try
        {
            for (var i = 1; i <= 20; i++)
            {
                pool.Release(pool.Acquire(new ParcelSpawnArgs((uint)i, 0, 1, 0)));
            }

            // Twenty parcels through one body. If this ever reads 20, the pool has stopped being
            // one and the belt is allocating per box.
            AssertInt(pool.Made).IsEqual(1);
        }
        finally
        {
            Drop(root);
        }
    }

    private static (Node3D Root, ParcelPool Pool) Rig()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "ParcelPoolRig" };
        tree.Root.AddChild(root);

        var pool = new ParcelPool { Name = "ParcelPool" };
        root.AddChild(pool);
        return (root, pool);
    }

    private static void Drop(Node root)
    {
        root.GetParent().RemoveChild(root);
        root.Free();
    }
}
