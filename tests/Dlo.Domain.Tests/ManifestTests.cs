using System.Linq;

using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// E2-03. The paperwork model: every field a Domain type, and the destination read off the
/// address rather than stored beside it.
/// </summary>
public class ManifestTests
{
    [Fact]
    public void A_manifest_routes_on_the_address_it_carries()
    {
        Assert.True(Address.TryParse("SOUTHGATE-11-903", out var address));
        var manifest = new Manifest(address, Weight: 6.25f, Fragility: 120, new ContentsCode("PERISHABLE"));

        // Derived, not stored. A second copy of the destination is a second thing to keep in
        // step, and the report is where you would find out it had drifted.
        Assert.Equal(address.Destination, manifest.Destination);
        Assert.Equal("SOUTHGATE-11", manifest.Destination.Value);
    }

    [Fact]
    public void Every_field_of_a_manifest_is_a_domain_type_or_a_primitive()
    {
        var offenders = typeof(Manifest)
            .GetProperties()
            .Select(p => p.PropertyType)
            .Where(t => !t.IsPrimitive && t.Assembly != typeof(Manifest).Assembly)
            .Select(t => t.FullName)
            .ToArray();

        // Standards §9, one layer out from §0: a manifest is going into a save file and onto the
        // wire, so a Godot type reaching it would be discovered by a serialiser rather than by a
        // reader. The architecture test guards the assembly; this guards the shape.
        Assert.Empty(offenders);
    }

    [Fact]
    public void Two_manifests_with_the_same_paperwork_are_equal()
    {
        Assert.True(Address.TryParse("NORTHGATE-4-118", out var address));

        var first = new Manifest(address, 2.5f, 10, new ContentsCode("STATIONERY"));
        var second = new Manifest(address, 2.5f, 10, new ContentsCode("STATIONERY"));

        // Value semantics all the way down, including through Address and ContentsCode — which
        // is what lets a test compare a respawned parcel's paperwork to what it started with.
        Assert.Equal(first, second);
    }

    [Fact]
    public void A_declared_weight_is_a_claim_and_nothing_checks_it_yet()
    {
        Assert.True(Address.TryParse("NORTHGATE-4-118", out var address));

        // The declaration/reality gap is the game's thesis (vision §9), and today only the
        // declaring half exists. This test exists to be edited when E2-08 adds the other half:
        // a manifest that cannot disagree with the box is a manifest nobody needs to read.
        var manifest = new Manifest(address, Weight: 0.1f, Fragility: 0, new ContentsCode("MACHINE_PARTS"));

        Assert.Equal(0.1f, manifest.Weight);
        Assert.Equal(new ContentsCode("MACHINE_PARTS"), manifest.DeclaredContents);
    }
}
