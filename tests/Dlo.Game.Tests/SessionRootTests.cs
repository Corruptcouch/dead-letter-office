using Dlo.Game.Net;
using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E0-04. Host, leave and teardown over <see cref="IGameTransport"/>, and the construction
/// seam behaving on the host and on nobody else.
/// </summary>
/// <remarks>
/// Join across two real peers is L3 (E0-09) — it needs a second process, and a host and client
/// sharing one <c>SceneTree</c> share one <c>MultiplayerAPI</c>, so a single-process join test
/// would assert the harness rather than the code.
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class SessionRootTests
{
    private SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    [AfterTest]
    public void ResetMultiplayer()
    {
        // The peer lives on the tree, not on the node, so a leaked one from a failed test
        // poisons every test after it. That presents as flakiness, which is how a suite loses
        // its credibility (E0-09).
        Tree.Root.Multiplayer.MultiplayerPeer = null;
    }

    private SessionRoot NewSessionRoot()
    {
        var root = AutoFree(new SessionRoot())!;
        root.Transport = new EnetTransport();
        Tree.Root.AddChild(root);
        return root;
    }

    [TestCase]
    public void Godot_reports_this_peer_as_server_when_there_is_no_peer_at_all()
    {
        // This is why SessionRoot diverges from arch §3.2 and does NOT construct in _Ready.
        // An autoload's _Ready runs at boot, and at boot this is true on every machine - so
        // the literal snippet would build a HostSession on all four clients. If Godot ever
        // changes this, this test goes red and the divergence can be reverted.
        AssertBool(Tree.Root.Multiplayer.MultiplayerPeer is null or OfflineMultiplayerPeer)
            .IsTrue();
        AssertBool(Tree.Root.Multiplayer.IsServer()).IsTrue();
    }

    [TestCase]
    public void Hosting_builds_the_domain_systems()
    {
        var root = NewSessionRoot();

        AssertObject(root.Session).IsNull();
        root.Host(maxPeers: 3);

        AssertObject(root.Session).IsNotNull();
        AssertObject(root.Session!.Director).IsNotNull();
        AssertObject(root.Session!.Ledger).IsNotNull();

        root.Leave();
    }

    [TestCase]
    public void Hosting_counts_the_host_itself_as_connected()
    {
        var root = NewSessionRoot();
        root.Host(maxPeers: 3);

        // Godot gives the host peer id 1 (arch §3.1). The host plays too, so a crew count that
        // omits it would be wrong by one everywhere it is used.
        AssertArray(root.Session!.ConnectedPeers).Contains(new Dlo.Domain.PeerId(1));

        root.Leave();
    }

    [TestCase]
    public void Leaving_drops_the_peer_and_the_domain_systems()
    {
        var root = NewSessionRoot();
        root.Host(maxPeers: 3);

        root.Leave();

        AssertBool(root.IsInSession).IsFalse();

        // The systems go with the session. A ShiftDirector outliving its shift is a second
        // source of truth waiting for the next one to start (arch §3.2).
        AssertObject(root.Session).IsNull();
    }

    [TestCase]
    public void Leaving_twice_is_not_an_error()
    {
        var root = NewSessionRoot();
        root.Host(maxPeers: 3);

        root.Leave();
        root.Leave();

        AssertBool(root.IsInSession).IsFalse();
    }

    [TestCase]
    public void Leaving_without_ever_hosting_is_not_an_error()
    {
        var root = NewSessionRoot();

        // Every error path ends here. Teardown that throws when there is nothing to tear down
        // turns one failure into two (standards §10).
        root.Leave();

        AssertBool(root.IsInSession).IsFalse();
    }
}
