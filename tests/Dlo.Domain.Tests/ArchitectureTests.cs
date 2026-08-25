using System.Linq;
using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// Arch §10.5. The dependency arrow from <c>Dlo.Game</c> to <c>Dlo.Domain</c> never
/// reverses, and standards §0 is explicit that this is enforced by a test rather than by
/// discipline — because discipline fails at 11pm.
/// </summary>
public class ArchitectureTests
{
    // Now arch §10.5's assertion verbatim: E2-01 landed `ParcelRecord`, which the section
    // names and which stood in as `Vec3` until it existed. Any Domain type does the job -
    // the assertion is about the assembly, and there is only one.
    //
    // What this catches, measured by breaking it both ways (E14-06, 2026-08-24):
    // adding a GodotSharp PackageReference to Dlo.Domain and building leaves this test
    // GREEN, because Roslyn emits an assembly reference only for an assembly the code
    // actually uses. Using one Godot type turns it red immediately. That is the right
    // line - an unused reference has not reversed the dependency arrow - but it does mean
    // this test guards the code and not the csproj. E14-01's "zero package references"
    // is a separate promise with no test behind it; the review checklist is what holds it.
    [Fact]
    public void Domain_does_not_reference_Godot() =>
        Assert.DoesNotContain("GodotSharp",
            typeof(ParcelRecord).Assembly.GetReferencedAssemblies().Select(a => a.Name));
}
