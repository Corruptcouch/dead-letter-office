using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// E13-02. One grammar, and the routable half of an address is read through that same grammar
/// rather than beside it.
/// </summary>
public class AddressTests
{
    [Fact]
    public void An_address_splits_into_the_part_the_facility_routes_on_and_the_part_it_does_not()
    {
        Assert.True(Address.TryParse("NORTHGATE-4-118", out var address));

        // Separate assertions (standards §8): a destination that swallowed the unit and a unit
        // that lost its district are different bugs with the same symptom at the chart.
        Assert.Equal("NORTHGATE-4", address.Destination.Value);
        Assert.Equal(118, address.Unit);
    }

    [Theory]
    [InlineData("northgate-4-118")]     // lower case
    [InlineData("NORTHGATE-4")]          // no unit
    [InlineData("NORTHGATE-4-118-2")]    // a fourth part
    [InlineData("NORTHGATE_4_118")]      // the wrong separator
    [InlineData("N-4-118")]              // district too short
    [InlineData("NORTHGATENORTHGATE-4-118")]
    [InlineData("NORTHGATE-4444-118")]   // block too long
    [InlineData("NORTHGATE-4-11800")]    // unit too long
    [InlineData("NORTHGATE-X-118")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_that_is_not_the_grammar_is_refused(string? text)
    {
        // Bounded on purpose. An unbounded district would accept a kilobyte of letters out of a
        // malformed file and report it much later as a routing miss (standards §9).
        Assert.False(Address.TryParse(text, out _));
    }

    [Fact]
    public void A_block_written_with_a_leading_zero_is_the_same_destination()
    {
        Assert.True(Address.TryParse("SOUTHGATE-04-9", out var padded));
        Assert.True(Address.TryParse("SOUTHGATE-4-9", out var plain));

        // Canonical, so one destination cannot become two that route differently — which is the
        // failure E13-03's "exactly one chute" would otherwise never catch.
        Assert.Equal(plain.Destination, padded.Destination);
    }

    [Fact]
    public void An_address_round_trips_through_the_text_it_is_written_as()
    {
        Assert.True(Address.TryParse("WESTHOLM-6-88", out var first));
        Assert.True(Address.TryParse(first.ToString(), out var second));

        Assert.Equal(first, second);
        Assert.Equal("WESTHOLM-6-88", first.ToString());
    }

    [Fact]
    public void A_destination_is_read_through_the_address_grammar_and_not_a_second_one()
    {
        Assert.True(Address.IsDestination("EASTMARCH-1", out var destination));
        Assert.Equal("EASTMARCH-1", destination.Value);

        // The same pattern, so these fail for the same reasons a full address does. A separate
        // destination pattern is the two-shapes mistake standards §9 calls permanent.
        Assert.False(Address.IsDestination("EASTMARCH-1-2", out _));
        Assert.False(Address.IsDestination("EASTMARCH", out _));
        Assert.False(Address.IsDestination("eastmarch-1", out _));
    }

    [Fact]
    public void Whitespace_around_an_authored_address_is_forgiven_and_nothing_else_is()
    {
        Assert.True(Address.TryParse("  NORTHGATE-9-7  ", out var padded));
        Assert.Equal("NORTHGATE-9", padded.Destination.Value);

        // A trailing space in a hand-edited CSV is a typo; a space in the middle is a different
        // address someone meant to write.
        Assert.False(Address.TryParse("NORTHGATE- 9-7", out _));
    }
}
