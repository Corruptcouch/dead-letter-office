using System.Collections.Generic;

namespace Dlo.Domain;

/// <summary>
/// Which chute each destination goes down (E13-03). The routing chart on the far wall renders
/// this (vision §8).
/// </summary>
/// <remarks>
/// Built from data, so a policy change is a data edit — which is what E3-08 needs when the PA
/// changes the rules mid-shift (arch §4.5). <b>Every destination maps to exactly one chute</b>,
/// and that is enforced when the content loads rather than trusted: a destination listed twice
/// is a rejected content set, not a last-one-wins.
/// </remarks>
public sealed class RoutingPolicy
{
    private readonly Dictionary<DestinationCode, ChuteId> _routes;

    /// <summary>Wraps an already-validated mapping. <see cref="ContentSet"/> is what builds it.</summary>
    internal RoutingPolicy(Dictionary<DestinationCode, ChuteId> routes)
    {
        _routes = routes;
    }

    /// <summary>Every destination this facility knows how to route.</summary>
    public IReadOnlyCollection<DestinationCode> Destinations => _routes.Keys;

    /// <summary>
    /// The chute for <paramref name="destination"/>, or <c>null</c> if this facility does not
    /// route there.
    /// </summary>
    /// <remarks>
    /// Null rather than a throw or a default chute: an unknown destination is a dead letter,
    /// which is the game's title and eventually E10, not an error condition (standards §9).
    /// </remarks>
    public ChuteId? ChuteFor(DestinationCode destination) =>
        _routes.TryGetValue(destination, out var chute) ? chute : null;
}
