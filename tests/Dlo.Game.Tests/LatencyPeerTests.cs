using System.Threading.Tasks;

using Dlo.Game.Net;
using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E0-07. The lag harness holds packets back for a configured delay, and refuses to exist in a
/// shipping build.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LatencyPeerTests
{
    private const int Port = 27391;
    private const int DelayMs = 150;
    private const int PollAttempts = 200;

    /// <summary>Polls both ends for a while, the way a running game would every frame.</summary>
    private static async Task Pump(MultiplayerPeer host, MultiplayerPeer client, int ms)
    {
        for (var waited = 0; waited < ms; waited += 5)
        {
            host.Poll();
            client.Poll();
            await Task.Delay(5);
        }
    }

    [TestCase]
    public void The_shipping_guard_is_a_rule_rather_than_a_convention()
    {
        // The case that matters only exists inside a release export, which no test can stand
        // in - so the rule is asserted directly instead. All four rows, because "off in a
        // release build" and "on in a debug build" are the two that must NOT throw, and a
        // guard that refuses everything would pass a one-row test.
        AssertBool(LatencyPeer.Refuse(enabled: true, isDebugBuild: false)).IsTrue();
        AssertBool(LatencyPeer.Refuse(enabled: true, isDebugBuild: true)).IsFalse();
        AssertBool(LatencyPeer.Refuse(enabled: false, isDebugBuild: false)).IsFalse();
        AssertBool(LatencyPeer.Refuse(enabled: false, isDebugBuild: true)).IsFalse();
    }

    [TestCase]
    public void An_unconfigured_build_is_not_wrapped_at_all()
    {
        // Nothing in this project sets the flag, so this asserts the default rather than a
        // stored value - the same shape as the transport and app-id defaults. The identity
        // check is the point: not "a working peer came back" but "the very same object did",
        // so a development tool cannot cost anything in a build that did not ask for it.
        var peer = new ENetMultiplayerPeer();

        AssertObject(LatencyPeer.WrapIfConfigured(peer)).IsSame(peer);
    }

    [TestCase]
    public async Task A_wrapped_peer_holds_a_packet_back_and_then_delivers_it()
    {
        var host = new ENetMultiplayerPeer();
        AssertInt((int)host.CreateServer(Port, 1)).IsEqual((int)Error.Ok);

        var inner = new ENetMultiplayerPeer();
        AssertInt((int)inner.CreateClient("127.0.0.1", Port)).IsEqual((int)Error.Ok);

        var client = LatencyPeer.Wrap(inner, DelayMs, jitterMs: 0);

        try
        {
            for (var i = 0; i < PollAttempts
                && client.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected; i++)
            {
                host.Poll();
                client.Poll();
                await Task.Delay(5);
            }

            AssertThat(client.GetConnectionStatus())
                .IsEqual(MultiplayerPeer.ConnectionStatus.Connected);

            // The client knows it is connected before the host has finished registering it, so
            // a broadcast sent the instant the loop above exits goes to nobody. Settling both
            // ends first is the difference between this test and a flaky one.
            await Pump(host, client, 100);

            // The handshake is NOT delayed, and that is worth knowing rather than worth fixing:
            // ENet completes a connection inside its own poll, below the packet API this class
            // decorates. Only application traffic goes through the queue.
            byte[] sent = [7, 11, 13];
            host.SetTargetPeer(0);
            host.PutPacket(sent);

            // Well inside the delay: the packet has reached the inner peer and is being held.
            await Pump(host, client, 40);

            AssertInt(client.GetAvailablePacketCount()).IsEqual(0);
            AssertInt(client.InFlight).IsEqual(1);

            // Comfortably past it.
            await Pump(host, client, DelayMs + 80);

            AssertInt(client.GetAvailablePacketCount()).IsEqual(1);
            AssertArray(client.GetPacket()).ContainsExactly(sent);
            AssertInt(client.InFlight).IsEqual(0);
        }
        finally
        {
            client.Close();
            host.Close();
        }
    }
}
