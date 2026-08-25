using System.Linq;

using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// E2-04's Domain half, and arch §5.3's rule stated as a test: what a client is given to build a
/// parcel is a strict subset of what the host knows, and the subset is fixed by arch §5.2.
/// </summary>
public class ParcelSpawnArgsTests
{
    [Fact]
    public void The_payload_is_the_four_values_arch_5_2_names_and_no_others()
    {
        var members = typeof(ParcelSpawnArgs)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, System.StringComparer.Ordinal)
            .ToArray();

        // The guard rather than the review comment. Nothing is being hidden today because there
        // is nothing yet to hide — E2-03 brings the manifest, and this is the assertion that
        // fails the moment someone adds it here instead of leaving it host-side.
        Assert.Equal(["Archetype", "Condition", "Id", "Size"], members);
    }

    [Fact]
    public void Two_parcels_differing_only_in_their_policy_lock_produce_identical_arguments()
    {
        var open = new ParcelRecord(new ParcelId(9), Archetype: 1, Size: 2, Condition: 3, IsLocked: false);
        var locked = open with { IsLocked = true };

        // The anti-assertion (standards §8). A client cannot tell a locked parcel from an open
        // one before it tries, which is why a mispredicted grab on a locked parcel is expected
        // behaviour rather than a bug (arch §3.3).
        Assert.Equal(ParcelSpawnArgs.From(open), ParcelSpawnArgs.From(locked));
    }

    [Fact]
    public void The_arguments_carry_the_physical_facts_a_client_needs_to_build_the_right_box()
    {
        var record = new ParcelRecord(new ParcelId(12), Archetype: 5, Size: 4, Condition: 200, IsLocked: false);

        var args = ParcelSpawnArgs.From(record);

        Assert.Equal(12u, args.Id);
        Assert.Equal(5, args.Archetype);
        Assert.Equal(4, args.Size);
        Assert.Equal(200, args.Condition);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(ParcelRecord.TwoPersonSize - 1, 1)]
    [InlineData(ParcelRecord.TwoPersonSize, 2)]
    [InlineData(255, 2)]
    public void Capacity_is_read_off_size_alone(byte size, int expected) =>
        Assert.Equal(expected, ParcelRecord.CarriersRequiredFor(size));

    [Fact]
    public void A_client_holding_only_spawn_arguments_computes_the_carrier_count_the_host_has()
    {
        var record = new ParcelRecord(
            new ParcelId(3), Archetype: 0, Size: ParcelRecord.TwoPersonSize, Condition: 0, IsLocked: false);

        var args = ParcelSpawnArgs.From(record);

        // The reason capacity is derived rather than stored or sent: two machines reach the same
        // answer with nothing on the wire, and neither holds a copy that can drift (arch §3.3).
        Assert.Equal(record.CarriersRequired, ParcelRecord.CarriersRequiredFor(args.Size));
        Assert.Equal(2, record.CarriersRequired);
    }
}
