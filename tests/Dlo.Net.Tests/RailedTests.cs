using System;
using System.Globalization;
using System.Linq;

using Xunit;
using Xunit.Abstractions;

namespace Dlo.Net.Tests;

/// <summary>
/// E2-05, over a real socket. One parcel rides a belt, is knocked off it, falls and settles, and
/// four peers have to agree where it is on the strength of almost nothing being sent.
/// </summary>
/// <remarks>
/// <b>The claim under test is arch §3.4's load-bearing one</b> — a railed parcel produces no
/// ongoing traffic at all, and clients extrapolate. L2 proves the class is configured that way;
/// only this proves a client that was told nothing arrives at the same answer.
/// </remarks>
public class RailedTests(RailedRun run, ITestOutputHelper output) : IClassFixture<RailedRun>
{
    /// <summary>
    /// How far apart two peers' parcels may be, in metres. Not a tuning knob: the belt has an end
    /// and every peer's arithmetic stops at it, so the peers should agree exactly and this is
    /// millimetres of float noise rather than a tolerance for skew.
    /// </summary>
    private const double Agreement = 0.01;

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
    public void A_railed_parcel_cost_the_host_nothing_while_it_rode()
    {
        Report();

        // Arch §3.4's claim, measured across three seconds of a real session rather than read off
        // a configuration. The tuple that put the parcel on the belt is already behind the meter,
        // so what is left is the ongoing cost, and the section says there is none.
        Assert.Equal(0, Host.Number(PeerReport.RideChanges));
        Assert.Equal(0, Host.Number(PeerReport.RideBytes));
    }

    [Fact]
    public void Every_client_extrapolated_the_belt_to_the_same_place()
    {
        Report();

        var truth = Where(Host, PeerReport.Rail);

        foreach (var peer in Clients)
        {
            var gap = Distance(truth, Where(peer, PeerReport.Rail));

            // The whole of "clients extrapolate" (arch §3.4). Each of these peers was sent one
            // twelve-byte tuple and then nothing, and computed the rest from a spline and a
            // speed it already had. A client that was NOT extrapolating would sit at the belt's
            // entry, two metres back.
            Assert.True(
                gap < Agreement,
                $"{peer.Role} rode to {peer.Field(PeerReport.Rail)}, the host to "
                + $"{Host.Field(PeerReport.Rail)} — {gap:F3} m apart.");
        }
    }

    [Fact]
    public void Every_peer_carried_the_parcel_the_whole_length_of_the_belt()
    {
        Report();

        foreach (var peer in run.Peers)
        {
            var distance = Number(peer.Field(PeerReport.RailDistance));

            // Agreement is worth nothing if all four agreed on standing still. The belt is two
            // metres long and the ride is three seconds at 1.2 m/s, so anything short of the end
            // means that peer's belt was not running.
            Assert.True(
                distance > 1.9,
                $"{peer.Role} only carried the parcel {distance:F3} m along a 2 m belt.");
        }
    }

    [Fact]
    public void The_host_demoted_the_parcel_to_sleeping_and_the_clients_never_classified_it()
    {
        Report();

        // Promotion and demotion, both directions, over the wire: the parcel went Railed →
        // Dynamic when it was knocked off and Dynamic → Sleeping when it settled, and the host
        // ended in the last of those.
        Assert.Equal("Sleeping", Host.Field(PeerReport.Class));

        foreach (var peer in Clients)
        {
            // Arch §3.1: the class is a decision about what to SEND. A client that reclassified
            // itself would be decoding the host's packets against a different property list —
            // which is silent, and presents as a parcel that stopped moving.
            Assert.Equal("Dynamic", peer.Field(PeerReport.Class));
        }
    }

    [Fact]
    public void Every_peer_agrees_where_the_parcel_came_to_rest()
    {
        Report();

        var truth = Where(Host, PeerReport.Parcel);

        foreach (var peer in Clients)
        {
            var gap = Distance(truth, Where(peer, PeerReport.Parcel));

            // The final transform of arch §3.4's Sleeping class, seen from the receiving end. The
            // host has stopped sending by the time this is sampled, so a client holding the right
            // answer holds it because the last thing it was told was the resting pose.
            Assert.True(
                gap < Agreement,
                $"{peer.Role} has the parcel at rest at {peer.Field(PeerReport.Parcel)}, the host "
                + $"at {Host.Field(PeerReport.Parcel)} — {gap:F3} m apart.");
        }
    }

    [Fact]
    public void Every_peer_saw_the_parcel_fall_off_the_belt_and_land()
    {
        Report();

        foreach (var peer in run.Peers)
        {
            var railHeight = Where(peer, PeerReport.Rail)[1];
            var restHeight = Where(peer, PeerReport.Parcel)[1];

            // The promotion, stated as the thing a player would see. A client that never received
            // a streamed transform would still be holding the parcel at belt height, agreeing
            // with nobody about a box hanging in the air — which is the failure this whole
            // scenario exists to catch, and it is invisible from any single peer.
            Assert.True(
                railHeight - restHeight > 1.0,
                $"{peer.Role} left the parcel at y={restHeight:F3} after riding at y={railHeight:F3}.");
        }
    }

    [Fact]
    public void The_streamed_transform_was_never_sent_reliably()
    {
        Report();

        foreach (var peer in Clients)
        {
            // Arch §3.4 asks for the transform stream to be unreliable, and this is the half of
            // that which is worth guarding: a transform put on the reliable path retransmits and
            // head-of-line blocks, and it is what the property becoming `OnChange` by accident
            // would produce. Measured across the fall, after the rail tuple's own reliable
            // clearing is behind the window.
            Assert.Equal(0, peer.Number(PeerReport.FallReliable));

            // And it really was streaming while the parcel fell, so the zero above is not the
            // zero of a peer that received nothing at all.
            Assert.True(
                peer.Number(PeerReport.FallStream) > 0,
                $"{peer.Role} took in no unreliable traffic at all while the parcel was falling.");
        }
    }

    private static double[] Where(PeerOutcome peer, string field)
    {
        var parts = peer.Field(field).Split('|');
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
