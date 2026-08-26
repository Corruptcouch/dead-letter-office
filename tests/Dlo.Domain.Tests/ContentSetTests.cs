using System.Collections.Generic;

using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// E13-01 and E13-03, and the load-time half of E13-02. Every rule is checked against text
/// rather than against files, so the suite stays L1 and a new invariant costs one string.
/// </summary>
public class ContentSetTests
{
    private const string Contents = "STATIONERY\nGLASSWARE\n";
    private const string Routing = "NORTHGATE-4,1\nSOUTHGATE-2,2\n";
    private const string Manifests = "NORTHGATE-4-118,2.5,10,STATIONERY\n";

    [Fact]
    public void A_sound_content_set_loads_with_nothing_to_say()
    {
        Assert.True(Load(out var set, out var problems, [Archetype(1, "Envelope", 0.4f, 1, "STATIONERY")]));

        Assert.Empty(problems);
        Assert.NotNull(set);
        Assert.Single(set.Archetypes);
        Assert.Single(set.Manifests);
        Assert.Equal(2, set.Routing.Destinations.Count);
    }

    [Fact]
    public void A_new_archetype_is_a_new_file_and_no_code_at_all()
    {
        // E13-01's criterion as a test rather than as a promise: two archetypes reach the loader
        // through the same path the first one did, and nothing here knows what they are.
        Assert.True(Load(
            out var set,
            out _,
            [
                Archetype(1, "Envelope", 0.4f, 1, "STATIONERY"),
                Archetype(2, "Glassware Crate", 22.0f, 3, "GLASSWARE"),
            ]));

        Assert.Equal(2, set!.Archetypes.Count);
        Assert.Equal("Glassware Crate", set.FindArchetype(2)!.Name);

        // And the size byte is doing its second job, so a two-person crate needs no extra field.
        Assert.Equal(2, ParcelRecord.CarriersRequiredFor(set.FindArchetype(2)!.Size));
    }

    [Fact]
    public void An_archetype_id_nobody_authored_is_inert_rather_than_fatal()
    {
        Assert.True(Load(out var set, out _, [Archetype(1, "Envelope", 0.4f, 1, "STATIONERY")]));

        // Standards §9: content outlives the table that described it. A save or an older peer
        // naming archetype 200 costs one missing parcel, not the shift.
        Assert.Null(set!.FindArchetype(200));
    }

    [Theory]
    [InlineData(0.0f, 1, "STATIONERY", "mass is")]
    [InlineData(500.0f, 1, "STATIONERY", "mass is")]
    [InlineData(1.0f, 0, "STATIONERY", "size is")]
    [InlineData(1.0f, 99, "STATIONERY", "size is")]
    [InlineData(1.0f, 1, "NOT_DECLARED", "declared contents")]
    public void An_archetype_outside_its_sane_range_is_reported_against_the_rule_it_broke(
        float mass, int size, string contents, string invariant)
    {
        Assert.False(Load(out var set, out var problems, [Archetype(1, "Thing", mass, size, contents)]));

        Assert.Null(set);
        Assert.Contains(problems, p => p.Invariant.StartsWith(invariant, System.StringComparison.Ordinal));

        // The message names the file, because "invalid" teaches nobody anything at 11pm (E13-05).
        Assert.Contains(problems, p => p.File.Contains("thing.tres", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Two_archetypes_claiming_one_id_is_a_rejected_content_set()
    {
        Assert.False(Load(
            out _,
            out var problems,
            [
                Archetype(7, "First", 1.0f, 1, "STATIONERY"),
                Archetype(7, "Second", 2.0f, 1, "GLASSWARE"),
            ]));

        var clash = Assert.Single(problems);
        Assert.Equal("each archetype id is used once", clash.Invariant);
        Assert.Contains("First", clash.Detail, System.StringComparison.Ordinal);
    }

    [Fact]
    public void A_destination_routed_twice_is_a_rejected_content_set()
    {
        // E13-03's whole criterion. Last-one-wins would be a shift where the chart on the wall
        // and the scoring at the whistle disagree, silently.
        Assert.False(Load(out var set, out var problems, routing: "NORTHGATE-4,1\nNORTHGATE-4,3\n"));

        Assert.Null(set);
        Assert.Contains(problems, p => p.Invariant == "each destination maps to exactly one chute");
    }

    [Fact]
    public void A_destination_written_two_ways_is_still_one_destination()
    {
        // The canonicalisation in the grammar is what makes the rule above enforceable at all.
        Assert.False(Load(out _, out var problems, routing: "NORTHGATE-4,1\nNORTHGATE-04,3\n"));

        Assert.Contains(problems, p => p.Invariant == "each destination maps to exactly one chute");
    }

    [Fact]
    public void An_address_that_no_route_can_reach_is_caught_at_load_and_not_at_the_chart()
    {
        // E13-02's criterion. Discovered mid-shift this is untraceable; discovered here it names
        // the row.
        Assert.False(Load(out _, out var problems, manifests: "ELSEWHERE-1-4,2.0,10,STATIONERY\n"));

        var problem = Assert.Single(problems);
        Assert.Equal("an address parses to a routable destination", problem.Invariant);
        Assert.Equal(1, problem.Line);
    }

    [Fact]
    public void A_chute_zero_is_refused_because_zero_is_no_chute()
    {
        Assert.False(Load(out _, out var problems, routing: "NORTHGATE-4,0\n"));

        Assert.Contains(problems, p => p.Invariant.StartsWith("a chute is", System.StringComparison.Ordinal));
    }

    [Fact]
    public void One_bad_row_rejects_the_whole_set_rather_than_loading_the_rest()
    {
        Assert.False(Load(
            out var set,
            out _,
            [
                Archetype(1, "Fine", 1.0f, 1, "STATIONERY"),
                Archetype(2, "Broken", 900.0f, 1, "STATIONERY"),
            ]));

        // Standards §9: reject the whole malformed unit. A set that loaded the good half would
        // be a shift missing an archetype nobody removed.
        Assert.Null(set);
    }

    [Fact]
    public void Every_problem_is_reported_at_once_rather_than_one_per_run()
    {
        Assert.False(Load(
            out _,
            out var problems,
            [
                Archetype(1, "Heavy", 900.0f, 1, "STATIONERY"),
                Archetype(2, "Huge", 1.0f, 99, "STATIONERY"),
            ]));

        // An author who fixes content one error per run is an author who stops running this.
        Assert.Equal(2, problems.Count);
    }

    [Fact]
    public void Comments_and_blank_lines_are_not_content()
    {
        Assert.True(Load(
            out var set,
            out var problems,
            routing: "# the chart\n\nNORTHGATE-4,1\n\n# and the rest\nSOUTHGATE-2,2\n"));

        Assert.Empty(problems);
        Assert.Equal(2, set!.Routing.Destinations.Count);
    }

    [Fact]
    public void A_destination_nothing_routes_to_has_no_chute_rather_than_a_default_one()
    {
        Assert.True(Load(out var set, out _));

        // Null, not chute zero and not a throw: an unroutable destination is a dead letter,
        // which is the game's title and eventually E10 — not an error condition.
        Assert.Null(set!.Routing.ChuteFor(new DestinationCode("NOWHERE-1")));
        Assert.Equal(new ChuteId(1), set.Routing.ChuteFor(new DestinationCode("NORTHGATE-4")));
    }

    [Fact]
    public void A_declared_weight_that_is_not_a_finite_number_is_rejected()
    {
        // "NaN" parses, and `NaN <= 0` is false, so the range check alone lets it through - and
        // it reaches the ledger, where every total it touches becomes NaN and nothing says why.
        Assert.False(Load(
            out var set,
            out var problems,
            [Archetype(1, "Envelope", 0.4f, 1, "STATIONERY")],
            manifests: "NORTHGATE-4-118,NaN,10,STATIONERY\n"));

        Assert.Null(set);
        Assert.Contains(
            problems,
            p => p.Invariant.StartsWith("a declared weight", System.StringComparison.Ordinal));
    }

    [Fact]
    public void A_contents_row_carrying_more_than_a_code_is_rejected()
    {
        // Reading field zero alone accepts the second column and silently ignores it, so an
        // author who thought they were declaring something would never be told otherwise.
        Assert.False(ContentSet.TryLoad(
            [Archetype(1, "Envelope", 0.4f, 1, "STATIONERY")],
            new ContentFile("content/contents.csv", "STATIONERY,3\nGLASSWARE\n"),
            new ContentFile("content/manifests.csv", Manifests),
            new ContentFile("content/routing.csv", Routing),
            out _,
            out var problems));

        Assert.Contains(
            problems,
            p => p.Invariant.StartsWith("a contents row", System.StringComparison.Ordinal));
    }

    private static ContentFile Archetype(int id, string name, float mass, int size, string contents) =>
        new(
            $"content/archetypes/{name.ToLowerInvariant().Replace(' ', '_')}.tres",
            "[gd_resource type=\"Resource\" script_class=\"ParcelArchetypeResource\" format=3]\n"
            + "\n[resource]\n"
            + $"id = {id}\nname = \"{name}\"\nmass = {mass.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n"
            + $"size = {size}\ndeclared_contents = \"{contents}\"\n");

    /// <summary>
    /// Loads a content set out of strings rather than files. <c>internal</c> so that
    /// <see cref="RoutingRulesTests"/>'s matrix runs over the same loader this suite checks,
    /// rather than over a hand-built dictionary of the shape it hopes the loader produces.
    /// </summary>
    internal static bool Load(
        out ContentSet? set,
        out IReadOnlyList<ContentProblem> problems,
        ContentFile[]? archetypes = null,
        string? routing = null,
        string? manifests = null) =>
        ContentSet.TryLoad(
            archetypes ?? [],
            new ContentFile("content/contents.csv", Contents),
            new ContentFile("content/manifests.csv", manifests ?? Manifests),
            new ContentFile("content/routing.csv", routing ?? Routing),
            out set,
            out problems);
}
