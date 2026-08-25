using System.Threading.Tasks;

using Dlo.Game.Carry;
using Dlo.Game.Net;

using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E1-05. The client acts before it is allowed to, and gives up gracefully when it turns out it
/// was not.
/// </summary>
/// <remarks>
/// The optimistic half is tested against a peer that <b>cannot possibly answer</b> — an ENet client
/// with no host, wrapped in <see cref="LatencyPeer"/> at 200 ms. That is the honest shape of "at any
/// latency": if the attach is there while no reply is even reachable, it did not wait for one.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class GrabPredictorTests
{
    private const int Latency = 200;

    private SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    /// <summary>
    /// The rig's own <see cref="MultiplayerApi"/>, so nothing here touches the tree's.
    /// </summary>
    /// <remarks>
    /// <b>A subtree API, not the root's, and that is not tidiness.</b> These tests install real
    /// ENet peers, and a peer left on the root's API poisons every suite that runs afterwards —
    /// once that API has been a <i>client</i>, detaching the peer does not make it a server again,
    /// so <c>SessionRootTests</c>' "no peer means server" assertion starts failing for reasons that
    /// live in this file. Measured 2026-08-25: three tests in two other suites went red.
    /// <para>
    /// <c>SceneTree.SetMultiplayer</c> scopes an API to a subtree, so <c>director.Multiplayer</c>
    /// resolves to this one and the root's is never written to at all.
    /// </para>
    /// </remarks>
    private MultiplayerApi? _api;

    [AfterTest]
    public void ReleasePeer()
    {
        if (_api?.MultiplayerPeer is { } peer and not OfflineMultiplayerPeer)
        {
            // Closed, not merely dropped: an ENet socket that outlives its test holds the port, and
            // the next test to bind it fails somewhere unrelated (E0-09 on flaky suites).
            peer.Close();
        }

        _api = null;
    }

    [TestCase]
    public async Task The_hands_and_the_load_attach_on_the_frame_the_button_goes_down()
    {
        var rig = Rig();

        try
        {
            Unanswerable(rig);

            var before = rig.Load.GlobalPosition;
            rig.Predictor.Press(rig.Load);

            // The hands have already moved, in the same call. No frame has passed and no reply
            // could have arrived - the host is not even reachable.
            AssertObject(rig.Predictor.Predicted).IsEqual(rig.Load);
            AssertBool(rig.Predictor.Confirmed).IsFalse();
            AssertFloat(rig.Arms.LeftTarget.DistanceTo(rig.Load.GlobalGrip(0)))
                .IsLessEqual(rig.Arms.ShoulderWidth + 0.001f);

            // The picture is already in the hands, in the same call, with no answer possible.
            AssertFloat(rig.Load.Visual.GlobalPosition.DistanceTo(rig.Body.Anchor.GlobalPosition))
                .IsLess(0.01f);

            await rig.Frame();

            // And the BODY has not been touched: it is still where it was, falling under its own
            // weight, because a client may not move what replication owns (arch §3.3). This is the
            // assertion that separates a visual-only attachment from a client writing the world.
            AssertFloat(rig.Load.GlobalPosition.DistanceTo(before)).IsLess(0.1f);
            AssertBool(rig.Predictor.Confirmed).IsFalse();
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_two_hundred_millisecond_link_creates_no_physics_joint_on_the_client()
    {
        var rig = Rig();

        try
        {
            Unanswerable(rig);

            rig.Predictor.Press(rig.Load);
            await rig.Settle(30);

            // The visual is attached and there is no constraint anywhere: the real joint exists
            // only on the host (arch §3.3). One process cannot prove a client had no OTHER source
            // for it - that half is E1-06's, at L3 - but it can prove this client made none.
            AssertObject(rig.Predictor.Predicted).IsEqual(rig.Load);
            AssertInt(Joints(rig.Root)).IsEqual(0);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_refusal_snaps_the_load_out_of_the_hands()
    {
        var rig = Rig();

        try
        {
            Unanswerable(rig);

            rig.Predictor.Press(rig.Load);
            await rig.Frame();

            var reached = rig.Load.Visual.Position.Length();
            AssertFloat(reached).IsGreater(0.1f);

            // The host says no. In a real session this is a GrabResolved for somebody else.
            rig.Director.GrabRefused(rig.Path, (int)GrabVerdict.NoSlotFree);

            // One frame in, the picture is on its way back but has NOT arrived: E1-06 requires the
            // loser to see it go rather than have it vanish from their hands.
            await rig.Frame();
            var midway = rig.Load.Visual.Position.Length();
            AssertFloat(midway).IsLess(reached);
            AssertFloat(midway).IsGreater(0.0f);

            await rig.Settle(GrabPredictor.SlipFrames + 2);

            // Home: no offset, no prediction, hands at rest, and the reason recorded not thrown.
            AssertObject(rig.Predictor.Predicted).IsNull();
            AssertObject(rig.Predictor.LastRefusal).IsEqual(GrabVerdict.NoSlotFree);
            AssertFloat(rig.Load.Visual.Position.Length()).IsEqualApprox(0.0f, 0.0001f);
            AssertFloat(rig.Arms.LeftTarget.DistanceTo(rig.Load.GlobalGrip(0)))
                .IsGreater(rig.Arms.ShoulderWidth);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_refusal_is_not_asked_again()
    {
        var rig = Rig();

        try
        {
            // Host mode, so a refusal comes straight back and a retry would be visible at once.
            Hosting(rig);

            var refusals = 0;
            rig.Director.Denied += (_, _) => refusals++;

            rig.Load.GlobalPosition = new Vector3(0, 1.0f, -30.0f);
            rig.Predictor.Press(rig.Load);
            await rig.Settle(60);

            // Exactly one. A predictor that re-asked on refusal would be a client hammering the
            // host for the rest of the shift, and a held button would make it every frame.
            AssertInt(refusals).IsEqual(1);
            AssertObject(rig.Predictor.Predicted).IsNull();
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task A_confirmation_changes_nothing_visible()
    {
        var rig = Rig();

        try
        {
            // Host mode: the grab is granted in the same call, so prediction and truth agree.
            Hosting(rig);
            rig.Predictor.Press(rig.Load);
            await rig.Settle(30);

            AssertBool(rig.Predictor.Confirmed).IsTrue();
            AssertObject(rig.Predictor.Predicted).IsEqual(rig.Load);

            // The host's joint has the body in the hands, so the picture is retired back onto it -
            // and because both describe the same place, nothing moves when it happens.
            AssertFloat(rig.Load.Visual.Position.Length()).IsEqualApprox(0.0f, 0.0001f);
            AssertFloat(rig.Load.GlobalPosition.DistanceTo(rig.Body.Anchor.GlobalPosition))
                .IsLess(0.3f);
        }
        finally
        {
            rig.Drop();
        }
    }

    [TestCase]
    public async Task Letting_go_needs_no_permission()
    {
        var rig = Rig();

        try
        {
            Hosting(rig);
            rig.Predictor.Press(rig.Load);
            await rig.Settle(10);

            rig.Predictor.Release();

            // Gone from the hands the instant it was asked for, with nothing able to refuse it.
            AssertObject(rig.Predictor.Predicted).IsNull();
            AssertBool(rig.Predictor.Confirmed).IsFalse();
        }
        finally
        {
            rig.Drop();
        }
    }

    /// <summary>
    /// Installs a real ENet <b>server</b> peer, so host mode is a fact rather than an inference.
    /// </summary>
    /// <remarks>
    /// A no-peer tree does report <c>IsServer() == true</c> (<c>SessionRootTests</c> asserts it),
    /// and relying on that here made two tests fail only when they ran after a client-peer test:
    /// once the API has been a client, detaching the peer does not make it a server again. Asking
    /// for a server outright removes the ordering dependence altogether.
    /// </remarks>
    private void Hosting(PredictorRig rig)
    {
        var peer = new ENetMultiplayerPeer();
        AssertInt((int)peer.CreateServer(EnetTransport.Port + 8, 3)).IsEqual((int)Error.Ok);

        _api!.MultiplayerPeer = peer;
        AssertBool(rig.Director.Multiplayer.IsServer()).IsTrue();
    }

    /// <summary>
    /// Installs a client peer with no host behind it, at 200 ms. Nothing it sends can be answered,
    /// which is the point.
    /// </summary>
    private void Unanswerable(PredictorRig rig)
    {
        var peer = new ENetMultiplayerPeer();
        AssertInt((int)peer.CreateClient("127.0.0.1", EnetTransport.Port + 7)).IsEqual((int)Error.Ok);

        _api!.MultiplayerPeer = LatencyPeer.Wrap(peer, Latency, jitterMs: 0);
        AssertBool(rig.Director.Multiplayer.IsServer()).IsFalse();
    }

    private static int Joints(Node node)
    {
        var found = node is Joint3D ? 1 : 0;
        foreach (var child in node.GetChildren())
        {
            found += Joints(child);
        }

        return found;
    }

    private PredictorRig Rig()
    {
        var root = new Node3D { Name = "PredictRig" };
        Tree.Root.AddChild(root);

        // Scoped before any child is added, so every node below resolves to this API.
        _api = MultiplayerApi.CreateDefaultInterface();
        Tree.SetMultiplayer(_api, root.GetPath());

        var floor = new StaticBody3D { Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(100, 1, 100) },
        });
        root.AddChild(floor);

        var director = new GrabDirector { Name = GrabDirector.NodeName };
        root.AddChild(director);

        var body = new PlayerCharacter { Name = "Carrier", Position = new Vector3(0, 1.0f, 0) };
        body.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.3f },
        });
        root.AddChild(body);

        var arms = new CarryArms { Name = "Arms" };
        body.AddChild(arms);

        var load = new Carryable { Name = "Load", Mass = 20.0f, Position = new Vector3(0, 1.2f, -1.0f) };
        load.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.6f, 0.6f, 0.6f) },
        });
        root.AddChild(load);

        var predictor = new GrabPredictor { Name = "Predictor" };
        root.AddChild(predictor);
        predictor.Bind(director, body, arms);

        director.RegisterCarrier(1, body);
        director.RegisterCarrier(director.Multiplayer.GetUniqueId(), body);

        return new PredictorRig(root, director, body, arms, load, predictor);
    }

    private sealed record PredictorRig(
        Node3D Root,
        GrabDirector Director,
        PlayerCharacter Body,
        CarryArms Arms,
        Carryable Load,
        GrabPredictor Predictor)
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
