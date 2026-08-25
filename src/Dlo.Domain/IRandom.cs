using System;
using System.Collections.Generic;

namespace Dlo.Domain;

/// <summary>
/// The only source of randomness Domain is allowed to use (arch §4.2, standards §0).
/// No <c>GD.Randi()</c>, no <c>Random.Shared</c>, no static RNG anywhere below the Game layer.
/// </summary>
/// <remarks>
/// This exists for one reason, and it is smaller than it looks: <b>a bug report can carry a
/// seed.</b> "The facility generated a loading dock inside the break room" is only fixable if
/// you can regenerate that facility. It is <i>not</i> here to make the simulation
/// deterministic across peers — arch §4.2 is explicit that this project is not
/// determinism-dependent, and host authority is what keeps four peers agreeing instead.
/// </remarks>
public interface IRandom
{
    /// <summary>A value in <c>[minInclusive, maxExclusive)</c>.</summary>
    int NextInt(int minInclusive, int maxExclusive);

    /// <summary>A value in <c>[0, 1)</c>.</summary>
    float NextFloat();

    /// <summary>One item, uniformly chosen.</summary>
    T Pick<T>(IReadOnlyList<T> items);

    /// <summary>One item, chosen in proportion to <paramref name="weight"/>.</summary>
    T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weight);
}
