using System.Linq;

using Xunit;
using Xunit.Abstractions;

namespace Dlo.Net.Tests;

/// <summary>
/// E0-10, first criterion. A client disconnects mid-session; the host and the remaining
/// clients keep running, with nothing orphaned and nothing thrown.
/// </summary>
/// <remarks>
/// <para>
/// <b>"Still works" is the assertion, not "did not crash."</b> The story is explicit about
/// that, and it is why the host publishes a <i>second</i> value — <see cref="Beacon.Aftermath"/>
/// — and publishes it only once the leaver is actually gone. A survivor holding that value
/// held it after the departure, and echoed it back over a socket that had just lost a peer.
/// A test that only checked the survivors were still <i>running</i> would pass against a
/// session that had silently stopped replicating, which is the interesting way for this to
/// break.
/// </para>
/// <para>
/// The leaver is <see cref="Scenario.Leaver"/>. It reports the first value, waits long enough
/// for the host to be holding that report, and then calls <c>SessionRoot.Leave</c> — a
/// deliberate disconnect rather than a killed process, because a killed process is E0-09's
/// orphan check and this is the graceful path.
/// </para>
/// </remarks>
public class DepartureTests(DepartureRun run, ITestOutputHelper output)
    : IClassFixture<DepartureRun>
{
    private PeerOutcome Host => run.Host!;

    private PeerOutcome Leaver => run.Clients.Single(peer => peer.Role == Scenario.Leaver);

    private PeerOutcome[] Survivors =>
        [.. run.Clients.Where(peer => peer.Role != Scenario.Leaver)];

    [Fact]
    public void The_run_started_at_all() =>
        Assert.True(run.SetupFailure is null, run.SetupFailure);

    [Fact]
    public void Every_peer_ended_on_its_own()
    {
        // Runs before Dispose can tidy up. A peer left holding port 27377 poisons the next
        // run, and that presents as flakiness rather than as the hang it actually is (E0-09).
        foreach (var peer in run.Peers)
        {
            Assert.True(peer.Exited,
                $"{peer.Role} was still running when the harness gave up.\n{run.Transcript}");
        }
    }

    [Fact]
    public void Every_peer_exited_cleanly()
    {
        foreach (var peer in run.Peers)
        {
            Assert.True(peer.ExitCode == 0,
                $"{peer.Role} exited {peer.ExitCode}.\n{run.Transcript}");
        }
    }

    [Fact]
    public void No_peer_logged_an_exception()
    {
        // "No exceptions" is E0-10's wording and the exit code cannot carry it: Godot prints
        // an exception thrown inside _Process and then runs the next frame, so a peer can
        // throw sixty times a second and still exit 0.
        foreach (var peer in run.Peers)
        {
            var errors = peer.Errors.ToArray();

            Assert.True(errors.Length == 0,
                $"{peer.Role} logged {errors.Length} error line(s), first: "
                + $"{errors.FirstOrDefault()}\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_leaver_left_deliberately()
    {
        Assert.True(Leaver.Field(PeerReport.Status) == PeerReport.Left,
            $"{Scenario.Leaver} ended as {Leaver.Field(PeerReport.Status)} rather than "
            + $"{PeerReport.Left}, so it never performed the departure under test."
            + $"\n{run.Transcript}");
    }

    [Fact]
    public void The_leavers_own_session_ended_clean()
    {
        Assert.True(Leaver.Field(PeerReport.Teardown) == PeerReport.Clean,
            $"{Scenario.Leaver} reported teardown={Leaver.Field(PeerReport.Teardown)}; its "
            + $"SessionRoot still held a peer or a HostSession after Leave().\n{run.Transcript}");
    }

    [Fact]
    public void The_host_survived_the_departure_and_counted_it()
    {
        Assert.True(Host.Field(PeerReport.Status) == PeerReport.Ok,
            $"the host ended as {Host.Field(PeerReport.Status)}.\n{run.Transcript}");

        // Three, not four: the host plus the two survivors. This is HostSession.PeerLeft
        // having actually run, asserted from another process. A crew still reading four means
        // the host never learned about the disconnect and would keep addressing a dead peer.
        Assert.True(Host.Number(PeerReport.Crew) == FourPeerSession.ClientRoles.Length,
            $"the host counted a crew of {Host.Field(PeerReport.Crew)} after the departure, "
            + $"expected {FourPeerSession.ClientRoles.Length}.\n{run.Transcript}");
    }

    [Fact]
    public void Both_survivors_converged_on_a_value_published_after_the_departure()
    {
        foreach (var survivor in Survivors)
        {
            Assert.True(survivor.Number(PeerReport.Beat) == Beacon.Aftermath,
                $"{survivor.Role} held beat={survivor.Field(PeerReport.Beat)}, expected "
                + $"{Beacon.Aftermath} — replication to it stopped when the other client left."
                + $"\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_host_heard_the_later_value_back_from_both_survivors()
    {
        var heard = Host.Heard;
        var echoes = heard.Where(report => report.Value == Beacon.Aftermath).ToArray();

        // Distinct ids by construction — `heard` is keyed by peer id, so two entries is two
        // peers and not one survivor reporting twice while the other went quiet.
        Assert.True(echoes.Length == Survivors.Length,
            $"the host heard the later value from {echoes.Length} peers, expected "
            + $"{Survivors.Length}.\n{run.Transcript}");

        foreach (var survivor in Survivors)
        {
            var id = survivor.Number(PeerReport.Id);

            Assert.True(echoes.Any(echo => echo.Key == id),
                $"the host never heard the later value from {survivor.Role} (id {id})."
                + $"\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_leaver_never_saw_the_later_value()
    {
        // The anti-assertion, and the one that stops this suite passing for the wrong reason.
        // If the host published the second value before the departure rather than after it,
        // every other test here would still be green and none of them would be about
        // surviving a disconnect any more.
        Assert.True(Leaver.Number(PeerReport.Beat) == Beacon.Sentinel,
            $"{Scenario.Leaver} left holding beat={Leaver.Field(PeerReport.Beat)}, so the host "
            + $"published {Beacon.Aftermath} before the departure and this run proves nothing."
            + $"\n{run.Transcript}");

        Assert.True(Host.Heard.TryGetValue(Leaver.Number(PeerReport.Id), out var reported)
            && reported == Beacon.Sentinel,
            $"the host's record of {Scenario.Leaver} is not the first value.\n{run.Transcript}");
    }

    [Fact]
    public void No_peer_left_a_session_behind()
    {
        // `live` is a fine answer here — the host never tore down, and a survivor that
        // finished on its flush window rather than on the host's exit did not either. `dirty`
        // is the failure: Leave() returned and something was still attached.
        foreach (var peer in run.Peers)
        {
            Assert.True(peer.Field(PeerReport.Teardown) != PeerReport.Dirty,
                $"{peer.Role} reported teardown=dirty.\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_run_stays_inside_its_wall_clock_budget()
    {
        output.WriteLine($"departure run: {run.Duration.TotalSeconds:F2}s");

        Assert.True(run.Duration.TotalSeconds < TeardownBudget.WallClockSeconds,
            $"the run took {run.Duration.TotalSeconds:F2}s, budget "
            + $"{TeardownBudget.WallClockSeconds:F0}s.\n{run.Transcript}");
    }
}

/// <summary>
/// E0-10, second criterion. The host tears the session down; every client ends its own session
/// cleanly rather than sitting there until its deadline.
/// </summary>
/// <remarks>
/// <para>
/// <b>This story stops at "the session ended cleanly."</b> The player-facing message and the
/// return to the lobby are E12-05, which is also explicit that there is no host migration — so
/// nothing here re-forms a session afterwards. What is asserted is that every client learned
/// the host had gone, dropped its peer and its <c>HostSession</c>, and exited under its own
/// power.
/// </para>
/// <para>
/// <b>Promptly is part of it.</b> A client that only noticed by waiting out ENet's connection
/// timeout would satisfy every other assertion here while being, to a player, a frozen game.
/// Measured 2026-08-25: every client noticed within one frame of the host closing its peer, so
/// <see cref="TeardownBudget.NoticeSeconds"/> is set far above that and far below the timeout
/// it is there to catch.
/// </para>
/// </remarks>
public class HostLossTests(HostLossRun run, ITestOutputHelper output)
    : IClassFixture<HostLossRun>
{
    private PeerOutcome Host => run.Host!;

    [Fact]
    public void The_run_started_at_all() =>
        Assert.True(run.SetupFailure is null, run.SetupFailure);

    [Fact]
    public void Every_peer_ended_on_its_own()
    {
        foreach (var peer in run.Peers)
        {
            Assert.True(peer.Exited,
                $"{peer.Role} was still running when the harness gave up.\n{run.Transcript}");
        }
    }

    [Fact]
    public void Every_peer_exited_cleanly()
    {
        foreach (var peer in run.Peers)
        {
            Assert.True(peer.ExitCode == 0,
                $"{peer.Role} exited {peer.ExitCode}.\n{run.Transcript}");
        }
    }

    [Fact]
    public void No_peer_logged_an_exception()
    {
        foreach (var peer in run.Peers)
        {
            var errors = peer.Errors.ToArray();

            Assert.True(errors.Length == 0,
                $"{peer.Role} logged {errors.Length} error line(s), first: "
                + $"{errors.FirstOrDefault()}\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_host_tore_its_own_session_down()
    {
        Assert.True(Host.Field(PeerReport.Status) == PeerReport.TornDown,
            $"the host ended as {Host.Field(PeerReport.Status)} rather than "
            + $"{PeerReport.TornDown}, so it never performed the teardown under test."
            + $"\n{run.Transcript}");

        Assert.True(Host.Field(PeerReport.Teardown) == PeerReport.Clean,
            $"the host reported teardown={Host.Field(PeerReport.Teardown)}; its SessionRoot "
            + $"still held a peer or a HostSession after Leave().\n{run.Transcript}");
    }

    [Fact]
    public void The_session_was_real_before_it_was_lost()
    {
        // Otherwise every assertion below could be satisfied by three clients that never
        // connected to anything, which is a very different thing from surviving a host loss.
        Assert.True(Host.Number(PeerReport.Intents) == FourPeerSession.ClientRoles.Length,
            $"the host tore down having heard {Host.Field(PeerReport.Intents)} intents, "
            + $"expected {FourPeerSession.ClientRoles.Length}.\n{run.Transcript}");

        foreach (var client in run.Clients)
        {
            Assert.True(client.Number(PeerReport.Beat) == Beacon.Sentinel,
                $"{client.Role} held beat={client.Field(PeerReport.Beat)} at the moment the "
                + $"host went away, so it had not converged.\n{run.Transcript}");
        }
    }

    [Fact]
    public void Every_client_noticed_the_host_had_gone()
    {
        foreach (var client in run.Clients)
        {
            // Not `timeout`, which is what a client that never noticed would report — and it
            // would report it twenty seconds later, having looked to a player like a hang.
            Assert.True(client.Field(PeerReport.Status) == PeerReport.HostLost,
                $"{client.Role} ended as {client.Field(PeerReport.Status)} rather than "
                + $"{PeerReport.HostLost}.\n{run.Transcript}");
        }
    }

    [Fact]
    public void Every_client_ended_its_own_session_cleanly()
    {
        foreach (var client in run.Clients)
        {
            Assert.True(client.Field(PeerReport.Teardown) == PeerReport.Clean,
                $"{client.Role} reported teardown={client.Field(PeerReport.Teardown)}; its "
                + $"SessionRoot still held a peer or a HostSession after the host went away."
                + $"\n{run.Transcript}");
        }
    }

    [Fact]
    public void No_client_sat_waiting_for_a_host_that_had_gone()
    {
        foreach (var client in run.Clients)
        {
            var elapsed = double.Parse(client.Field(PeerReport.Elapsed),
                System.Globalization.CultureInfo.InvariantCulture);

            output.WriteLine($"{client.Role} noticed after {elapsed:F2}s");

            Assert.True(elapsed < TeardownBudget.NoticeSeconds,
                $"{client.Role} ran {elapsed:F2}s before ending, budget "
                + $"{TeardownBudget.NoticeSeconds:F0}s. It is ending on a timeout rather than "
                + $"on the host's disconnect.\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_run_stays_inside_its_wall_clock_budget()
    {
        output.WriteLine($"host-loss run: {run.Duration.TotalSeconds:F2}s");

        Assert.True(run.Duration.TotalSeconds < TeardownBudget.WallClockSeconds,
            $"the run took {run.Duration.TotalSeconds:F2}s, budget "
            + $"{TeardownBudget.WallClockSeconds:F0}s.\n{run.Transcript}");
    }
}

/// <summary>The two bounds E0-10's runs are held to.</summary>
/// <remarks>
/// Both are set where a genuine regression lives rather than at the edge of normal variation.
/// A bound placed just past the observed value is the assertion that eventually goes flaky, and
/// a flaky assertion in the one suite whose value is not being flaky costs more than it catches.
/// </remarks>
public static class TeardownBudget
{
    /// <summary>
    /// Whole-run ceiling. Arch §10.1 budgets minutes for L3; observed is about 1.5 s for the
    /// departure run and under 1 s for the host-loss one (2026-08-25).
    /// </summary>
    public const double WallClockSeconds = 30.0;

    /// <summary>
    /// How long a client may take to notice its host has gone. Observed: one frame, about
    /// 0.2 s from the client's own first frame. Set below anything a connection timeout could
    /// produce, because ending on a timeout is the failure this is looking for.
    /// </summary>
    public const double NoticeSeconds = 3.0;
}
