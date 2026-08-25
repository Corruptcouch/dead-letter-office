using Dlo.Game.Net;
using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E0-05. The spawner takes a small explicit payload, builds a node from it alone, and learns
/// new kinds without being edited.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NetworkSpawnerTests
{
    private const string Crate = "crate";
    private const string Pallet = "pallet";

    [TestCase]
    public void A_node_is_built_from_its_spawn_arguments_and_nothing_else()
    {
        var (root, spawner) = Rig();

        try
        {
            // The builder closes over nothing. Whatever it produces came out of the payload,
            // which is the property a client depends on: it has never seen this object before
            // and gets no second round trip to ask about it (arch §5.2).
            spawner.Register(Crate, args => new Node3D
            {
                Name = "Crate",
                Scale = Vector3.One * args.AsGodotArray()[0].AsSingle(),
            });

            var spawned = (Node3D)spawner.Spawn(Crate, new Godot.Collections.Array { 2.5f });

            AssertThat(spawned.Scale.X).IsEqual(2.5f);
            AssertBool(spawned.IsInsideTree()).IsTrue();
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void A_second_spawnable_kind_needs_no_change_to_the_wrapper()
    {
        var (root, spawner) = Rig();

        try
        {
            spawner.Register(Crate, _ => new Node3D { Name = "Crate" });
            spawner.Register(Pallet, _ => new Node3D { Name = "Pallet" });

            // Two kinds, one wrapper, no edit between them. This is what makes E13-01's parcel
            // archetypes content rather than code (arch §4.1).
            AssertString(spawner.Spawn(Crate, new Godot.Collections.Array()).Name)
                .IsEqual("Crate");
            AssertString(spawner.Spawn(Pallet, new Godot.Collections.Array()).Name)
                .IsEqual("Pallet");
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public void The_payload_is_the_key_and_the_arguments_and_nothing_more()
    {
        // Two entries, not three, and not a record. The manifest is not in here and E2-04
        // inspects the serialised bytes to prove it stays out; this is the shape that makes
        // that assertion possible in the first place (arch §5.2, §5.3).
        var payload = NetworkSpawner.Payload(Crate, new Godot.Collections.Array { 1, 2 });

        AssertInt(payload.Count).IsEqual(2);
        AssertString(payload[0].AsString()).IsEqual(Crate);
        AssertInt(payload[1].AsGodotArray().Count).IsEqual(2);
    }

    [TestCase]
    public void Spawning_an_unregistered_kind_fails_loudly()
    {
        var (root, spawner) = Rig();

        try
        {
            spawner.Register(Crate, _ => new Node3D());

            // A typo fails identically on all four machines, so the only symptom would be
            // nothing appearing anywhere. The message names what IS registered, because at
            // that point the question is always "what did I actually call it".
            AssertThrown(() => spawner.Spawn("crat", new Godot.Collections.Array()))
                .IsInstanceOf<System.InvalidOperationException>()
                .HasMessage("Nothing is registered to spawn 'crat'. Registered: crate.");
        }
        finally
        {
            Drop(root);
        }
    }

    private static (Node3D Root, NetworkSpawner Spawner) Rig()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "SpawnRig" };
        tree.Root.AddChild(root);

        var spawner = new NetworkSpawner { Name = "NetworkSpawner", SpawnRoot = new NodePath("..") };
        root.AddChild(spawner);
        return (root, spawner);
    }

    private static void Drop(Node root)
    {
        root.GetParent().RemoveChild(root);
        root.QueueFree();
    }
}
