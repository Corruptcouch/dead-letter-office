using System;
using System.Globalization;
using System.Linq;

using Xunit;
using Xunit.Abstractions;

namespace Dlo.Net.Tests;

/// <summary>
/// E1-06. Three clients reach for one parcel at once; exactly one ends up holding it, the losers
/// let go cleanly, and no client ever made a physics joint.
/// </summary>
/// <remarks>
/// <b>This is arch §10.4's named case</b>, and it is the reason the L3 harness exists at all — a
/// contention bug cannot be reproduced in one process, because one process has one
/// <c>MultiplayerAPI</c> and one physics world (E0-08).
/// <para>
/// <b>What "the same frame" can and cannot mean here.</b> Three separate processes cannot be proved
/// to have pressed on the same frame, and pretending otherwise would be a lie in the test name. What
/// is under test is the property that matters: however the three requests interleave on the wire,
/// the host serialises them and grants exactly one. The clients are released by one replicated
/// signal, so the window between their requests is a network hop rather than anything scheduled.
/// </para>
/// </remarks>
public class ContentionTests(ContentionRun run, ITestOutputHelper output)
    : IClassFixture<ContentionRun>
{
    private PeerOutcome Host => run.Host!;

    private PeerOutcome[] Clients => [.. run.Clients];

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
    public void The_host_granted_the_parcel_to_exactly_one_carrier()
    {
        Report();

        // The whole story in one number. Two holders on a one-person parcel is the bug this test
        // exists for, and it is the kind that only appears when two requests land in the same tick.
        Assert.Equal(1, Host.Number(PeerReport.Holders));
    }

    [Fact]
    public void Exactly_one_client_believes_it_won()
    {
        Report();

        var winners = Clients.Count(peer => peer.Number(PeerReport.Won) == 1);

        // Asserted separately from the host's count, and not as a conjunction with it (standards
        // §8). "The host granted one" and "one client thinks it holds it" are different claims:
        // the pair failing together is a resolution bug, and only the second failing is a
        // broadcast bug - GrabResolved not reaching the peer it named.
        Assert.Equal(1, winners);
    }

    [Fact]
    public void Every_client_agrees_with_the_host_about_who_holds_it()
    {
        Report();

        // Each client reports the holder count it has locally. All three must match the host,
        // including the two that lost - a loser holding a stale "I have it" is what leaves a
        // client with hands full of nothing for the rest of the shift.
        foreach (var peer in Clients)
        {
            Assert.Equal(
                Host.Number(PeerReport.Holders),
                peer.Number(PeerReport.Holders));
        }
    }

    [Fact]
    public void No_client_ever_created_a_physics_joint()
    {
        Report();

        // Arch §3.3: the real joint exists only on the host. This is the cross-process half that
        // one process cannot prove (E0-05 made the same split) - a client here has its own physics
        // world, so a joint in it could only have been created locally.
        foreach (var peer in Clients)
        {
            Assert.Equal(0, peer.Number(PeerReport.Joints));
        }

        // And the host made exactly the one it granted.
        Assert.Equal(1, Host.Number(PeerReport.Joints));
    }

    [Fact]
    public void The_losers_end_up_where_the_host_says_the_parcel_is()
    {
        Report();

        var truth = Where(Host);

        foreach (var peer in Clients.Where(p => p.Number(PeerReport.Won) == 0))
        {
            var seen = Where(peer);
            var gap = Distance(truth, seen);

            // "The loser sees the parcel move toward the winner" (E1-06), as an assertion: the
            // loser's copy converges on the host's, rather than being left in the hands of a grab
            // that did not happen. A loser stuck holding its prediction would sit ~0.5 m away, at
            // its own hand.
            Assert.True(
                gap < 0.25,
                $"{peer.Role} has the parcel {gap:F3} m from where the host does "
                + $"(host {Host.Field(PeerReport.Parcel)}, {peer.Role} {peer.Field(PeerReport.Parcel)}).");
        }
    }

    [Fact]
    public void The_parcel_never_teleported_on_anybody()
    {
        Report();

        foreach (var peer in run.Peers.Where(p => p.Reported))
        {
            var jump = Number(peer.Field(PeerReport.Jump));

            // It does not teleport (E1-06). At 60 Hz a parcel moving even 10 m/s covers 0.17 m in a
            // frame, so a third of a metre is comfortably above anything physical and far below the
            // ~0.5 m snap a rolled-back prediction would produce.
            Assert.True(
                jump < 0.33,
                $"{peer.Role} saw the parcel jump {jump:F3} m in one frame.");
        }
    }

    [Fact]
    public void The_losers_did_not_leave_the_parcel_inside_the_floor()
    {
        Report();

        foreach (var peer in run.Peers.Where(p => p.Reported))
        {
            var y = Where(peer)[1];

            // "It does not end up inside geometry" (E1-06). The parcel is 0.6 tall and the floor
            // these peers run on is at y = 0, so anything below 0.2 has sunk into it.
            Assert.True(y > 0.2, $"{peer.Role} has the parcel at y={y:F3}, inside the floor.");
        }
    }

    private static double[] Where(PeerOutcome peer)
    {
        var parts = peer.Field(PeerReport.Parcel).Split('|');
        return parts.Length == 3
            ? [.. parts.Select(Number)]
            : [double.NaN, double.NaN, double.NaN];
    }

    private static double Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : double.NaN;

    private static double Distance(double[] a, double[] b) =>
        Math.Sqrt(((a[0] - b[0]) * (a[0] - b[0]))
            + ((a[1] - b[1]) * (a[1] - b[1]))
            + ((a[2] - b[2]) * (a[2] - b[2])));

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
