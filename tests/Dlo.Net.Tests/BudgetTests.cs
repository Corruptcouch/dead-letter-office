using System.Globalization;

using Xunit;
using Xunit.Abstractions;

namespace Dlo.Net.Tests;

/// <summary>
/// E2-10. Arch §8's worst case, metered on a real socket: forty awake parcel bodies and a belt
/// backed up to its end, with a host and three clients.
/// </summary>
/// <remarks>
/// <b>Read the finding in arch §11 before changing a number here.</b> Arch §8's 60 KB/s is
/// provisional and this is the measurement that replaces it: the belt is free, exactly as §3.4
/// claims, and forty awake bodies are not affordable at 30 Hz to three clients. The assertions
/// below guard the measurement against drift; what to do about the budget is the owner's.
/// </remarks>
public class BudgetTests(BudgetRun run, ITestOutputHelper output) : IClassFixture<BudgetRun>
{
    /// <summary>Arch §8's gameplay budget, in bytes per second.</summary>
    private const double Budget = 60_000;

    /// <summary>
    /// The ceiling this suite actually holds the awake reading to, in bytes per second.
    /// </summary>
    /// <remarks>
    /// Above the 68–70 KB/s measured 2026-08-25 and below the ~100 KB/s arch §3.4's own 28-byte
    /// arithmetic predicts, so it catches a regression to a fatter encoding without going red on
    /// window noise. It is <b>not</b> a budget, and it is not a revision of one — see arch §11.
    /// </remarks>
    private const double Recorded = 85_000;

    /// <summary>Bytes per body per client per tick that arch §3.4 assumed a transform costs.</summary>
    private const double AssumedTransform = 28;

    /// <summary>Clients the host is sending to, and <c>Dynamic</c>'s rate, from arch §3.4.</summary>
    private const double Clients = 3;

    private const double DynamicHz = 30;

    private PeerOutcome Host => run.Host!;

    [Fact]
    public void The_run_started_at_all() =>
        Assert.True(run.SetupFailure is null, run.SetupFailure);

    [Fact]
    public void Every_peer_exited_cleanly()
    {
        Report();

        foreach (var peer in run.Peers)
        {
            Assert.True(peer.Exited, $"{peer.Role} never exited.");
            Assert.Equal(0, peer.ExitCode);
            Assert.Equal(PeerReport.Ok, peer.Field(PeerReport.Status));
        }
    }

    [Fact]
    public void A_belt_backed_up_to_its_end_costs_less_than_one_awake_body()
    {
        Report();

        var idle = Rate(PeerReport.IdleRate);
        var railed = Rate(PeerReport.RailedRate);
        var bodies = Host.Number(PeerReport.AwakeBodies);
        var perBody = (Rate(PeerReport.AwakeRate) - railed) / bodies;

        // Arch §3.4's keystone, on the wire rather than at the configuration: thirty-four parcels
        // riding a belt cost the host less than ONE loose box does. Measured 2026-08-25 at 1244
        // B/s against an idle 1306 — the belt is below the noise floor of the beacon, so the
        // comparison has to be against something that is not zero to mean anything.
        Assert.True(
            railed - idle < perBody,
            $"A full belt cost {railed - idle:F0} B/s over idle, more than the {perBody:F0} B/s "
            + $"one awake body costs. Arch §3.4 prices a railed parcel at nothing ongoing.");
    }

    [Fact]
    public void Every_client_picked_the_whole_belt_up_from_the_tuples_alone()
    {
        Report();

        foreach (var peer in run.Clients)
        {
            // The other half of "the belt is free": it is free because nothing is sent, and this
            // is the assertion that the clients nevertheless have all of it. A belt that cost
            // nothing because the parcels never arrived would pass the reading above.
            Assert.Equal(Host.Number(PeerReport.RailedBodies), peer.Number(PeerReport.RailedBodies));
        }
    }

    [Fact]
    public void One_transform_costs_no_more_than_the_architecture_assumed()
    {
        Report();

        var perBody = (Rate(PeerReport.AwakeRate) - Rate(PeerReport.RailedRate))
            / Host.Number(PeerReport.AwakeBodies);
        var perTick = perBody / Clients / DynamicHz;

        // Arch §3.4's arithmetic starts from 28 bytes of transform. Measured 18.6 including UDP
        // and ENet framing, so the encoding is better than the section assumed — which matters,
        // because it means the budget below is missed on body count and rate, not on waste.
        Assert.True(
            perTick < AssumedTransform,
            $"One awake body costs {perTick:F1} bytes per client per tick, against the "
            + $"{AssumedTransform} arch §3.4 assumed.");
    }

    [Fact]
    public void Forty_awake_bodies_still_cost_what_the_finding_recorded()
    {
        Report();

        var awake = Rate(PeerReport.AwakeRate);

        // A regression guard on a recorded number, not a budget check. Arch §8's 60 KB/s and its
        // own "≤ 40 awake bodies" cannot both hold at 30 Hz to three clients, and E2-10's job was
        // to say so with a figure rather than to pick which one gives — that is the owner's.
        output.WriteLine(
            $"E2-10: {awake:F0} B/s against arch §8's {Budget:F0} B/s gameplay budget "
            + $"({awake / Budget:P0} of it), with {Host.Number(PeerReport.AwakeBodies)} awake bodies "
            + $"and {Host.Number(PeerReport.RailedBodies)} on the belt.");

        Assert.True(
            awake < Recorded,
            $"Forty awake bodies now cost {awake:F0} B/s, past the {Recorded:F0} B/s recorded in "
            + "arch §11. Something got more expensive; the finding says what it used to be.");
    }

    private double Rate(string field) =>
        double.TryParse(Host.Field(field), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : double.NaN;

    /// <summary>
    /// Prints every peer's whole position, so a failure names who disagreed and what they held
    /// (E0-09) rather than only that somebody did.
    /// </summary>
    private void Report()
    {
        foreach (var peer in run.Peers)
        {
            output.WriteLine(peer.Describe());
        }
    }
}
