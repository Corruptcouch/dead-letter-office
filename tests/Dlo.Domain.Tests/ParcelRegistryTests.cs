using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// Arch §5.1: identity is host-assigned and outlives the node. Everything below is about the
/// registry and the record alone — that a record survives its node is E2-02, which needs an
/// engine.
/// </summary>
public class ParcelRegistryTests
{
    [Fact]
    public void A_registered_parcel_is_found_again_by_its_id()
    {
        var registry = new ParcelRegistry();

        var registered = registry.Register(archetype: 7, size: 4, condition: 2, isLocked: true);
        var found = registry.Find(registered.Id);

        // Separate assertions rather than one conjunction (standards §8): a registry that
        // stored the record under the wrong key and one that dropped a field are different
        // bugs, and the failure should say which.
        Assert.NotNull(found);
        Assert.Equal(7, found.Archetype);
        Assert.Equal(4, found.Size);
        Assert.Equal(2, found.Condition);
        Assert.True(found.IsLocked);
    }

    [Fact]
    public void Every_registered_parcel_gets_a_distinct_id()
    {
        var registry = new ParcelRegistry();

        var first = registry.Register(archetype: 1, size: 1, condition: 0);
        var second = registry.Register(archetype: 1, size: 1, condition: 0);

        // Two parcels with identical contents are still two parcels. If this ever fails, the
        // report blames one player for what another did, which is the whole game (vision §7).
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void An_id_that_was_never_registered_finds_nothing() =>
        Assert.Null(new ParcelRegistry().Find(new ParcelId(404)));

    [Fact]
    public void A_default_ParcelId_names_no_parcel()
    {
        var registry = new ParcelRegistry();
        registry.Register(archetype: 1, size: 1, condition: 0);

        // The reason ids count from one. An uninitialised ParcelId resolving to the first
        // parcel registered is the kind of wrong that looks like it works.
        Assert.Null(registry.Find(default));
    }

    [Fact]
    public void The_registry_does_not_forget_a_parcel_it_has_issued()
    {
        var registry = new ParcelRegistry();
        var first = registry.Register(archetype: 1, size: 1, condition: 0);

        for (var i = 0; i < 50; i++)
        {
            registry.Register(archetype: 2, size: 2, condition: 0);
        }

        // The report is built at the whistle from things that happened at the start of the
        // shift (vision §7), so eviction would be a data-loss bug wearing a memory-saving hat.
        Assert.NotNull(registry.Find(first.Id));
        Assert.Equal(51, registry.Count);
    }
}
