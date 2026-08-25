using System.Threading.Tasks;

using Dlo.Game.Net;
using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// E0-02. Proves the ENet transport actually moves a connection, and that the Steam stub
/// refuses rather than pretending.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TransportTests
{
    private const int PollAttempts = 200;

    [TestCase]
    public async Task Enet_host_and_client_connect_locally()
    {
        var transport = new EnetTransport();
        var host = transport.CreateHost(maxPeers: 3);
        var client = transport.CreateClient("127.0.0.1");

        try
        {
            // Both ends have to be pumped: the host has to accept before the client's status
            // leaves Connecting, so polling only the client hangs until the timeout.
            for (var i = 0; i < PollAttempts && ClientIsPending(client); i++)
            {
                host.Poll();
                client.Poll();
                await Task.Delay(5);
            }

            AssertThat(client.GetConnectionStatus())
                .IsEqual(MultiplayerPeer.ConnectionStatus.Connected);
        }
        finally
        {
            client.Close();
            host.Close();
        }
    }

    [TestCase]
    public void Steam_transport_refuses_rather_than_falling_back_to_enet()
    {
        var steam = new SteamTransport();

        // The anti-assertion that matters (standards §8). A stub that quietly returned an
        // ENet peer would pass every other test in this file and ship a build that cannot
        // see a Steam friend. This test exists to make that failure loud.
        AssertThrown(() => steam.CreateHost(maxPeers: 3))
            .IsInstanceOf<System.NotSupportedException>();
        AssertThrown(() => steam.CreateClient("76561197960287930"))
            .IsInstanceOf<System.NotSupportedException>();
    }

    [TestCase]
    public void Unset_configuration_selects_enet_for_development()
    {
        // Arch §3.5: ENet is the development and test default. Nothing sets the project
        // setting in this project, so this asserts the fallback rather than a stored value.
        AssertThat(GameTransport.ForCurrentBuild()).IsInstanceOf<EnetTransport>();
    }

    [TestCase]
    public void Unset_configuration_selects_the_public_test_app_id()
    {
        // Same shape as the transport default above, and the same reason: nothing in this
        // project sets the app id, so this asserts the fallback rather than a stored value.
        AssertThat(SteamTransport.AppId).IsEqual(SteamTransport.TestAppId);

        // Asserted separately, and not as a nicety. The setting is read through a Variant, so
        // a mistyped setting name or a conversion that quietly failed would both hand back 0 -
        // and 0 is a valid-looking app id that makes SteamAPI_Init fail with a message about
        // Steam not running, which is the wrong thing to go and debug (standards §8).
        AssertThat(SteamTransport.AppId).IsGreater(0u);
    }

    private static bool ClientIsPending(MultiplayerPeer client) =>
        client.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connecting;
}
