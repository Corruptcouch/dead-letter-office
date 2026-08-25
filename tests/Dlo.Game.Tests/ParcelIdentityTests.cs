using System.Threading.Tasks;

using Dlo.Domain;
using Dlo.Game.Carry;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E2-02. Arch §5.1's promise, taken literally: the node is a view, so destroying it destroys a
/// view. This is the test tube transit and pooling both rest on.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ParcelIdentityTests
{
    [TestCase]
    public async Task A_freed_node_leaves_its_record_intact_and_addressable()
    {
        var registry = new ParcelRegistry();
        var record = registry.Register(archetype: 2, size: 4, condition: 7, isLocked: true);
        var (root, node) = Spawn(record);

        try
        {
            node.QueueFree();
            await Frame(root);
            await Frame(root);

            // The load-bearing half. Without this the test passes for the wrong reason forever:
            // a node merely hidden would keep every field it had, and nothing below would fail
            // even if the record had been thrown away with it.
            AssertBool(GodotObject.IsInstanceValid(node)).IsFalse();

            var found = registry.Find(record.Id);

            AssertObject(found).IsNotNull();
            AssertInt(found!.Archetype).IsEqual(2);
            AssertInt(found.Size).IsEqual(4);
            AssertInt(found.Condition).IsEqual(7);
            AssertBool(found.IsLocked).IsTrue();
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task Respawning_from_the_id_restores_the_parcel_the_record_describes()
    {
        var registry = new ParcelRegistry();
        AssertBool(Address.TryParse("NORTHGATE-4-118", out var address)).IsTrue();
        var record = registry.Register(
            archetype: 9,
            size: ParcelRecord.TwoPersonSize,
            condition: 3,
            manifest: new Manifest(address, Weight: 2.5f, Fragility: 10, new ContentsCode("STATIONERY")));

        var (root, first) = Spawn(record);

        try
        {
            first.QueueFree();
            await Frame(root);
            await Frame(root);
            AssertBool(GodotObject.IsInstanceValid(first)).IsFalse();

            // Three rooms away, a new node, the same parcel (arch §5.1). Everything it is built
            // from comes out of the registry, which never saw the node die.
            var again = registry.Find(record.Id)!;
            var second = (Carryable)ParcelSpawn.Build(ParcelSpawn.ToPayload(ParcelSpawnArgs.From(again)));
            root.AddChild(second);

            AssertBool(second.Id == record.Id).IsTrue();
            AssertInt(second.Archetype).IsEqual(9);
            AssertInt(second.CarriersRequired).IsEqual(2);

            // The manifest survives too, and it survives WHERE IT LIVES. It is not asserted on
            // the node, because a node that carried one would be a node that could show it to a
            // client that has not scanned the box (arch §5.3): the registry is what outlived the
            // death, and the paperwork with it.
            AssertObject(again.Manifest).IsNotNull();
            AssertString(again.Manifest!.Destination.Value).IsEqual("NORTHGATE-4");

            // ponytail: the criterion says manifest, tamper state and culpability, and two of
            // those three are still missing.
            // Ceiling: E2-07's tamper state and arch §4.6's ActorRef do not exist on
            // ParcelRecord, so there is nothing more to restore. E3-04 added the manifest above.
            // Upgrade: each of those stories adds its field to this assertion; if one lands
            // without doing so, this test still passes and that is the gap to watch.
        }
        finally
        {
            Drop(root);
        }
    }

    [TestCase]
    public async Task A_second_node_for_the_same_id_does_not_mint_a_second_parcel()
    {
        var registry = new ParcelRegistry();
        var record = registry.Register(archetype: 1, size: 1, condition: 0);
        var (root, first) = Spawn(record);

        try
        {
            var second = (Carryable)ParcelSpawn.Build(ParcelSpawn.ToPayload(ParcelSpawnArgs.From(record)));
            root.AddChild(second);
            await Frame(root);

            // Two views of one parcel is the normal case — the host's node and a client's are
            // exactly that. What must not happen is the registry growing a parcel per view.
            AssertBool(first.Id == second.Id).IsTrue();
            AssertInt(registry.Count).IsEqual(1);
        }
        finally
        {
            Drop(root);
        }
    }

    private static (Node3D Root, Carryable Node) Spawn(ParcelRecord record)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = new Node3D { Name = "ParcelIdentityRig" };
        tree.Root.AddChild(root);

        var node = (Carryable)ParcelSpawn.Build(ParcelSpawn.ToPayload(ParcelSpawnArgs.From(record)));
        root.AddChild(node);
        return (root, node);
    }

    private static async Task Frame(Node node)
    {
        var tree = node.GetTree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void Drop(Node root)
    {
        root.GetParent().RemoveChild(root);
        root.Free();
    }
}
