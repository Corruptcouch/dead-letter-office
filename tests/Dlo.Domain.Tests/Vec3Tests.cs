using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// The L1 harness check (E14-04). Its job is to prove the suite is real: that xUnit
/// discovers a test, that this project genuinely compiles and links against
/// <c>Dlo.Domain</c>, and that a failure is reported as one.
///
/// Both facts below are properties of <see cref="Vec3"/>'s declaration rather than of
/// hand-written logic, and that is the point — AGENTS.md exempts trivial one-liners from
/// tests, so the first test asserts the smallest real value it can rather than inventing
/// Domain logic for something to check.
/// </summary>
public class Vec3Tests
{
    [Fact]
    public void Vec3_keeps_its_components_in_the_order_they_were_given()
    {
        var v = new Vec3(3f, -4f, 0.5f);

        // Three assertions rather than one conjunction (standards §8): a swapped X and Z
        // is the classic boundary-conversion bug, and it should name which axis moved.
        Assert.Equal(3f, v.X);
        Assert.Equal(-4f, v.Y);
        Assert.Equal(0.5f, v.Z);
    }

    [Fact]
    public void Two_Vec3s_with_the_same_components_are_equal()
    {
        // Value semantics are load-bearing: a record that quietly became a class would
        // compare by reference and every Domain comparison would go silently wrong. This
        // also proves `readonly record struct` compiles with no hand-declared
        // IsExternalInit, which is what net10.0 buys over netstandard2.1 (standards §1).
        Assert.Equal(new Vec3(1f, 2f, 3f), new Vec3(1f, 2f, 3f));
    }
}
