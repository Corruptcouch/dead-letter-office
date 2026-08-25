namespace Dlo.Domain;

/// <summary>
/// Whether a parcel went down the right chute (arch §4.5).
/// </summary>
/// <remarks>
/// <b>Pure: no engine, no clock, no side effects</b> — which is why "did the shift score
/// correctly?" is answered by a table of cases rather than by four peers and a controller. It is
/// also why nothing may cache what it returns: the answer is a function of a
/// <see cref="PolicyState"/> that changes under the crew (standards §12).
/// </remarks>
public static class RoutingRules
{
    /// <summary>
    /// What <paramref name="chute"/> did with <paramref name="parcel"/> under
    /// <paramref name="policy"/>.
    /// </summary>
    /// <param name="parcel">The parcel that went down it.</param>
    /// <param name="chute">The chute it went down.</param>
    /// <param name="policy">The rules in force. <b>At chute entry</b> — see the remarks.</param>
    /// <returns>The outcome, which nobody is told until the whistle (arch §4.4).</returns>
    /// <exception cref="System.ArgumentNullException">Either reference argument is null.</exception>
    /// <remarks>
    /// <b>Which policy judges a parcel already in flight is the caller's to decide</b>, and the
    /// gaps table has it open at the epic level. This function holds no clock and cannot answer
    /// it; passing the policy in is what keeps that a decision somebody makes rather than one
    /// this file makes quietly.
    /// </remarks>
    public static RoutingOutcome Evaluate(ParcelRecord parcel, ChuteId chute, PolicyState policy)
    {
        System.ArgumentNullException.ThrowIfNull(parcel);
        System.ArgumentNullException.ThrowIfNull(policy);

        // No paperwork is not an error and not a misroute. It is the game's title (arch §4.5),
        // and the same value as a destination this facility has never routed to, because nothing
        // yet tells the two apart and inventing the distinction here would be E10 done early.
        if (parcel.Manifest is not { } manifest || policy.ChuteFor(manifest.Destination) is not { } expected)
        {
            return RoutingOutcome.DeadLetter;
        }

        return expected == chute ? RoutingOutcome.CorrectlyRouted : RoutingOutcome.Misrouted;
    }
}
