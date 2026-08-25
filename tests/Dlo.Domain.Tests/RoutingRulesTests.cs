using System;
using System.Linq;

using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// E3-04. The matrix under <see cref="RoutingRules.Evaluate"/>, and the reason "did the shift
/// score correctly?" never requires four peers and a controller (arch §4.5).
/// </summary>
public class RoutingRulesTests
{
    private const string Routes = "NORTHGATE-4,1\nSOUTHGATE-2,2\nEASTGATE-9,3\n";

    private static readonly ChuteId _northgate = new(1);
    private static readonly ChuteId _southgate = new(2);

    [Fact]
    public void The_right_chute_is_correctly_routed()
    {
        var policy = Policy();

        Assert.Equal(
            RoutingOutcome.CorrectlyRouted,
            RoutingRules.Evaluate(Parcel("NORTHGATE-4-118"), _northgate, policy));
    }

    [Fact]
    public void The_wrong_chute_is_a_misroute()
    {
        var policy = Policy();

        Assert.Equal(
            RoutingOutcome.Misrouted,
            RoutingRules.Evaluate(Parcel("NORTHGATE-4-118"), _southgate, policy));
    }

    [Fact]
    public void A_chute_this_facility_does_not_have_is_a_misroute()
    {
        var policy = Policy();

        // Chute zero is "no chute" (ChuteId's own doc), and it is not a special case here: the
        // policy says northgate goes down one, this is not one, and that is all a misroute is.
        Assert.Equal(
            RoutingOutcome.Misrouted,
            RoutingRules.Evaluate(Parcel("NORTHGATE-4-118"), default, policy));
    }

    [Fact]
    public void A_destination_the_policy_has_never_heard_of_is_a_dead_letter()
    {
        var policy = Policy();

        // Not an error and not a misroute (arch §4.5). A content file cannot produce this —
        // E13-02 rejects an address whose destination is in no route at load — so what reaches
        // it is a save, a replay or a peer from an older build, and the answer degrades.
        Assert.Equal(
            RoutingOutcome.DeadLetter,
            RoutingRules.Evaluate(Parcel("WESTGATE-7-100"), _northgate, policy));
    }

    [Fact]
    public void A_parcel_with_no_paperwork_at_all_is_a_dead_letter()
    {
        var policy = Policy();
        var unaddressed = new ParcelRegistry().Register(archetype: 1, size: 1, condition: 0);

        Assert.Null(unaddressed.Manifest);
        Assert.Equal(RoutingOutcome.DeadLetter, RoutingRules.Evaluate(unaddressed, _northgate, policy));
    }

    [Fact]
    public void A_chute_that_was_right_this_morning_is_a_misroute_after_the_policy_changes()
    {
        var policy = Policy();
        var parcel = Parcel("NORTHGATE-4-118");

        Assert.Equal(RoutingOutcome.CorrectlyRouted, RoutingRules.Evaluate(parcel, _northgate, policy));

        // The antagonist landing a hit, expressed as a data change (arch §4.5). The same parcel
        // down the same chute, and the crew has not been told anything except by the chart.
        Assert.True(policy.Reroute(new DestinationCode("NORTHGATE-4"), _southgate));

        Assert.Equal(RoutingOutcome.Misrouted, RoutingRules.Evaluate(parcel, _northgate, policy));
        Assert.Equal(RoutingOutcome.CorrectlyRouted, RoutingRules.Evaluate(parcel, _southgate, policy));
    }

    [Fact]
    public void Nothing_caches_a_routing_answer_across_a_policy_change()
    {
        var policy = Policy();
        var parcel = Parcel("SOUTHGATE-2-4");

        // Standards §12 forbids caching one, and the way that regresses is a memo keyed on the
        // parcel: ask enough times before the change that any such memo is warm, then change it.
        for (var i = 0; i < 8; i++)
        {
            Assert.Equal(RoutingOutcome.CorrectlyRouted, RoutingRules.Evaluate(parcel, _southgate, policy));
        }

        policy.Reroute(new DestinationCode("SOUTHGATE-2"), new ChuteId(3));

        Assert.Equal(RoutingOutcome.Misrouted, RoutingRules.Evaluate(parcel, _southgate, policy));
    }

    [Fact]
    public void One_destination_maps_to_one_chute_and_a_content_file_cannot_say_otherwise()
    {
        // The first of E13-03's two ends: a table that maps a destination twice does not load
        // with the last one winning. It does not load.
        Assert.False(ContentSetTests.Load(
            out _, out var problems, routing: "NORTHGATE-4,1\nNORTHGATE-4,2\n"));

        Assert.Contains(problems, p => p.Invariant == "each destination maps to exactly one chute");
    }

    [Fact]
    public void One_destination_maps_to_one_chute_and_a_mid_shift_change_cannot_say_otherwise()
    {
        var policy = Policy();
        var parcel = Parcel("EASTGATE-9-2");

        // The second end, and the one a mid-shift change could reach. Rerouting REPLACES: after
        // two changes the destination goes down the last chute named and no other, so there is
        // no reachable state where a parcel is correctly routed down two.
        policy.Reroute(new DestinationCode("EASTGATE-9"), new ChuteId(1));
        policy.Reroute(new DestinationCode("EASTGATE-9"), new ChuteId(2));

        var correct = Chutes().Count(chute => RoutingRules.Evaluate(parcel, chute, policy)
            == RoutingOutcome.CorrectlyRouted);

        Assert.Equal(1, correct);
        Assert.Equal(RoutingOutcome.CorrectlyRouted, RoutingRules.Evaluate(parcel, new ChuteId(2), policy));
    }

    [Fact]
    public void A_destination_nobody_authored_cannot_be_invented_mid_shift()
    {
        var policy = Policy();

        // Management moves parcels between chutes; it does not open districts. Allowing this
        // would author content at runtime that no validator ever saw, and E13-02 rejects an
        // address whose destination is in no route precisely so that cannot happen.
        Assert.False(policy.Reroute(new DestinationCode("WESTGATE-7"), _northgate));
        Assert.Null(policy.ChuteFor(new DestinationCode("WESTGATE-7")));

        // And a change to no chute at all is refused too: chute zero is not a chute, and a
        // destination routed there would read as a dead letter the content files never declared.
        Assert.False(policy.Reroute(new DestinationCode("NORTHGATE-4"), default));
        Assert.Equal(_northgate, policy.ChuteFor(new DestinationCode("NORTHGATE-4")));
    }

    [Fact]
    public void Misroute_is_not_revealed_until_the_whistle()
    {
        var policy = Policy();
        var registry = new ParcelRegistry();
        var parcel = registry.Register(
            archetype: 1, size: 1, condition: 0, manifest: Manifest("NORTHGATE-4-118"));

        var before = registry.Find(parcel.Id);
        Assert.Equal(RoutingOutcome.Misrouted, RoutingRules.Evaluate(parcel, _southgate, policy));

        // Arch §4.4: the domain records misrouting silently and reveals it at the whistle, and
        // the delayed reveal is the blame engine's ammunition. A pure function is what makes that
        // structural rather than a rule somebody remembers — evaluating leaves the record, the
        // registry and the policy exactly as they were, so there is nothing for a live indicator
        // to have been driven from.
        Assert.Equal(before, registry.Find(parcel.Id));
        Assert.False(policy.IsAmended);

        // And the outcome has nowhere to live on a parcel, which is the way this would regress:
        // a field on the record is a field a node can render and a client can be sent.
        var leaks = typeof(ParcelRecord)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(RoutingOutcome)
                || p.PropertyType == typeof(RoutingOutcome?))
            .Select(p => p.Name)
            .ToArray();

        Assert.Empty(leaks);
    }

    [Fact]
    public void Evaluate_has_exactly_the_signature_the_architecture_names()
    {
        var method = typeof(RoutingRules).GetMethod(nameof(RoutingRules.Evaluate));
        var parameters = method!.GetParameters().Select(p => p.ParameterType).ToArray();

        // Arch §4.5 writes this signature out in full, and it is the whole of the design: three
        // inputs and no fourth. A clock or a registry appearing here is how "pure" is lost, and
        // it would be lost in a diff that reads like a convenience.
        Assert.True(method.IsStatic);
        Assert.Equal(typeof(RoutingOutcome), method.ReturnType);
        Assert.Equal([typeof(ParcelRecord), typeof(ChuteId), typeof(PolicyState)], parameters);
    }

    [Fact]
    public void Evaluate_refuses_a_null_parcel_or_policy()
    {
        // Not politeness: a null policy silently treated as "routes nowhere" would score a whole
        // shift as dead letters and look like a content problem.
        Assert.Throws<ArgumentNullException>(() =>
            RoutingRules.Evaluate(null!, _northgate, Policy()));

        Assert.Throws<ArgumentNullException>(() =>
            RoutingRules.Evaluate(Parcel("NORTHGATE-4-118"), _northgate, null!));
    }

    /// <summary>Every chute the authored table names, for a count-the-correct-ones assertion.</summary>
    private static ChuteId[] Chutes() => [default, new(1), new(2), new(3)];

    /// <summary>
    /// The policy in force, seeded from real authored content rather than from a hand-built
    /// dictionary — so the matrix is over the shape <c>ContentTool</c> validates.
    /// </summary>
    private static PolicyState Policy()
    {
        Assert.True(ContentSetTests.Load(out var set, out _, routing: Routes));
        return new PolicyState(set!.Routing);
    }

    private static ParcelRecord Parcel(string address) => new ParcelRegistry()
        .Register(archetype: 1, size: 1, condition: 0, manifest: Manifest(address));

    private static Manifest Manifest(string address)
    {
        Assert.True(Address.TryParse(address, out var parsed));
        return new Manifest(parsed, Weight: 3.0f, Fragility: 10, new ContentsCode("STATIONERY"));
    }
}
