using System.Collections.Generic;

namespace Dlo.Domain;

/// <summary>
/// The routing rules actually in force, which management may change mid-shift (arch §4.5).
/// </summary>
/// <remarks>
/// <b>Separate from <see cref="RoutingPolicy"/> because one is authored and one is live.</b> The
/// authored policy is what the content files said and never changes; this is what the chart on
/// the far wall renders and what the PA can rewrite under the crew (E3-08). Keeping the seed
/// intact is also what makes "the rules changed at 4 minutes" answerable at the whistle.
/// </remarks>
/// <param name="authored">The policy the content files loaded (E13-03).</param>
public sealed class PolicyState(RoutingPolicy authored)
{
    private readonly Dictionary<DestinationCode, ChuteId> _changed = [];

    /// <summary>Whether management has changed anything since the shift started.</summary>
    public bool IsAmended => _changed.Count > 0;

    /// <summary>
    /// The chute <paramref name="destination"/> currently goes down, or <c>null</c> if this
    /// facility does not route there.
    /// </summary>
    /// <param name="destination">A destination read off a parcel's address.</param>
    public ChuteId? ChuteFor(DestinationCode destination) =>
        _changed.TryGetValue(destination, out var chute) ? chute : authored.ChuteFor(destination);

    /// <summary>
    /// Sends <paramref name="destination"/> down <paramref name="chute"/> from now on.
    /// </summary>
    /// <param name="destination">A destination the authored policy already knows.</param>
    /// <param name="chute">The chute it goes down now. Chute zero is no chute, and is refused.</param>
    /// <returns><c>false</c> if the change was refused, and nothing changed.</returns>
    /// <remarks>
    /// <b>Replaces, never adds</b>, which is the second end of E13-03's one-destination-one-chute
    /// rule: content load rejects a table that maps a destination twice, and there is no reachable
    /// state here that maps one to two. A destination the authored policy has never heard of is
    /// refused as well — management moves parcels between chutes, it does not invent districts
    /// mid-shift, and doing so would author content that no validator ever saw.
    /// </remarks>
    public bool Reroute(DestinationCode destination, ChuteId chute)
    {
        if (chute.Value == 0 || authored.ChuteFor(destination) is null)
        {
            return false;
        }

        _changed[destination] = chute;
        return true;
    }
}
