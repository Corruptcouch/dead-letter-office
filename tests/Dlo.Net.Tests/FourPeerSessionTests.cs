using System.Linq;

using Xunit;
using Xunit.Abstractions;

namespace Dlo.Net.Tests;

/// <summary>
/// E0-09. A headless host and three headless clients, four real processes, one real socket:
/// an intent RPC arrives and a replicated value converges, and nothing is left running
/// afterwards.
/// </summary>
/// <remarks>
/// <para>
/// One run, several assertions. The run costs about two seconds and the assertions cost
/// nothing, so re-running it per fact would buy nothing and pay for it four times over —
/// but each fact is still its own test, because "L3 failed" and "client2 never saw the value
/// the host published" are worth very different amounts at 11pm (standards §8).
/// </para>
/// <para>
/// Every failure message carries the whole run: what each peer exited with and what each peer
/// was holding. E0-09 asks for a failure that names which peer disagreed and what it held,
/// and that is a property of the message rather than of the assertion.
/// </para>
/// </remarks>
public class FourPeerSessionTests(ConvergeRun run, ITestOutputHelper output)
    : IClassFixture<ConvergeRun>
{
    /// <summary>
    /// Arch §10.1 budgets minutes for L3. Observed: about two seconds. This bound is neither,
    /// deliberately — it is set where a genuine regression lives rather than at the edge of
    /// normal variation, so it cannot become the flaky assertion in a suite whose whole value
    /// is not being flaky.
    /// </summary>
    private const double BudgetSeconds = 30.0;

    [Fact]
    public void The_run_started_at_all() =>
        Assert.True(run.SetupFailure is null, run.SetupFailure);

    [Fact]
    public void Every_peer_ended_on_its_own()
    {
        // The orphan check, and it runs before Dispose gets a chance to tidy up. A peer left
        // holding port 27377 poisons the next run, and that presents as flakiness rather than
        // as the hang it actually is (E0-09).
        foreach (var peer in Peers())
        {
            Assert.True(peer.Exited,
                $"{peer.Role} was still running when the harness gave up.\n{run.Transcript}");
        }
    }

    [Fact]
    public void Every_peer_exited_cleanly()
    {
        foreach (var peer in Peers())
        {
            Assert.True(peer.ExitCode == 0,
                $"{peer.Role} exited {peer.ExitCode}.\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_host_received_an_intent_from_every_client()
    {
        var host = Host();

        Assert.True(host.Number(PeerReport.Intents) == FourPeerSession.ClientRoles.Length,
            $"the host received {host.Field(PeerReport.Intents)} intents, expected "
            + $"{FourPeerSession.ClientRoles.Length}.\n{run.Transcript}");
    }

    [Fact]
    public void The_host_counted_a_crew_of_four()
    {
        var host = Host();

        // Godot gives the host peer id 1 and the host plays too, so the crew is four and not
        // three (arch §3.1). A count that is wrong by one here is wrong everywhere it is used.
        Assert.True(host.Number(PeerReport.Crew) == 4,
            $"the host counted a crew of {host.Field(PeerReport.Crew)}.\n{run.Transcript}");
    }

    [Fact]
    public void Every_client_converged_on_the_replicated_value()
    {
        foreach (var client in Clients())
        {
            Assert.True(client.Number(PeerReport.Beat) == Beacon.Sentinel,
                $"{client.Role} held beat={client.Field(PeerReport.Beat)}, expected "
                + $"{Beacon.Sentinel}.\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_host_heard_that_value_back_from_three_distinct_peers()
    {
        var host = Host();
        var heard = host.Heard;

        // Distinct ids, not just three reports: three messages from one confused client would
        // otherwise satisfy the count while proving nothing about the other two.
        Assert.True(heard.Count == FourPeerSession.ClientRoles.Length,
            $"the host heard from {heard.Count} distinct peers.\n{run.Transcript}");

        foreach (var (id, beat) in heard)
        {
            Assert.True(beat == Beacon.Sentinel,
                $"peer {id} reported beat={beat}, expected {Beacon.Sentinel}.\n{run.Transcript}");
        }
    }

    [Fact]
    public void Each_client_reported_under_its_own_identity()
    {
        var ids = Clients().Select(client => client.Field(PeerReport.Id)).ToArray();

        // Godot hands ENet clients a random uint rather than 2, 3, 4 (measured 2026-08-24).
        // Nothing may assume otherwise, and a collision would make E3-06's per-post filtering
        // send a manifest to the wrong machine.
        Assert.True(ids.Distinct().Count() == ids.Length,
            $"client ids were not distinct: {string.Join(", ", ids)}.\n{run.Transcript}");
        foreach (var client in Clients())
        {
            Assert.True(client.Number(PeerReport.Id) > 1,
                $"{client.Role} reported id={client.Field(PeerReport.Id)}; 1 is the host and 0 "
                + $"is no connection at all.\n{run.Transcript}");
        }
    }

    [Fact]
    public void No_client_built_a_domain_system()
    {
        // Arch §3.2's promise, asserted across a process boundary where it means something:
        // clients construct nothing, so there is no client-side ShiftDirector to drift out of
        // step with the host's. A client reports a crew of 0 because it has no HostSession to
        // ask — and the L2 suite can only ever check that on the machine that built it.
        foreach (var client in Clients())
        {
            Assert.True(client.Number(PeerReport.Crew) == 0,
                $"{client.Role} reported a crew of {client.Field(PeerReport.Crew)}, so it built "
                + $"a HostSession of its own.\n{run.Transcript}");
        }
    }

    [Fact]
    public void The_run_stays_inside_its_wall_clock_budget()
    {
        // E0-08's second criterion, and it is asserted rather than recorded so that it stays
        // true. A suite that grows to twenty minutes is a suite that gets skipped, and a
        // number in a document does not notice when that starts happening.
        output.WriteLine($"four-peer run: {run.Duration.TotalSeconds:F2}s");

        Assert.True(run.Duration.TotalSeconds < BudgetSeconds,
            $"the run took {run.Duration.TotalSeconds:F2}s, budget {BudgetSeconds:F0}s."
            + $"\n{run.Transcript}");
    }

    private PeerOutcome Host()
    {
        var host = run.Host;
        Assert.True(host is not null, run.SetupFailure ?? "the host never started.");
        Assert.True(host!.Reported, $"the host never reported.\n{run.Transcript}");
        return host;
    }

    private PeerOutcome[] Clients()
    {
        var clients = run.Clients.ToArray();
        Assert.True(clients.Length == FourPeerSession.ClientRoles.Length,
            run.SetupFailure ?? $"only {clients.Length} clients started.\n{run.Transcript}");

        foreach (var client in clients)
        {
            Assert.True(client.Reported, $"{client.Role} never reported.\n{run.Transcript}");
        }

        return clients;
    }

    private PeerOutcome[] Peers()
    {
        Assert.True(run.SetupFailure is null, run.SetupFailure);
        return [.. run.Peers];
    }
}
