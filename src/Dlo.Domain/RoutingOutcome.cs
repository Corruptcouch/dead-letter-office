namespace Dlo.Domain;

/// <summary>
/// What a chute did with a parcel (arch §4.5). The `RESOLVED` half of the lifecycle in
/// <see cref="RoutingRules.Evaluate"/>'s own terms.
/// </summary>
/// <remarks>
/// <b>Nobody is told this while the shift is running</b> (arch §4.4). It is computed, recorded
/// silently, and read out at the whistle, because the delayed reveal is what the blame engine
/// fires (vision §7).
/// </remarks>
public enum RoutingOutcome
{
    /// <summary>Down the chute the policy in force says it goes.</summary>
    CorrectlyRouted,

    /// <summary>Down a chute that is not that one. Recorded, not announced.</summary>
    Misrouted,

    /// <summary>
    /// Nowhere this facility routes: no paperwork at all, or a destination the policy has never
    /// heard of.
    /// </summary>
    /// <remarks>
    /// Not an error condition — it is the game's title, and eventually E10. The two ways of
    /// reaching it are deliberately one value while nothing tells them apart; E10 is the story
    /// that has to care which.
    /// </remarks>
    DeadLetter,
}
