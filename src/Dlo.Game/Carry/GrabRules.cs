namespace Dlo.Game.Carry;

/// <summary>Why a grab was granted or refused.</summary>
public enum GrabVerdict
{
    /// <summary>The carrier gets a slot.</summary>
    Granted,

    /// <summary>Too far away.</summary>
    OutOfReach,

    /// <summary>Every slot on the load is already taken.</summary>
    NoSlotFree,

    /// <summary>Policy forbids it (E3-05 is what will set that).</summary>
    Locked,

    /// <summary>This carrier is already holding it.</summary>
    AlreadyHolding,
}

/// <summary>
/// The grab decision, as a pure function (E1-04). No engine types, no nodes, no clock — so the
/// suite can assert every branch directly rather than by staging a physics scene per case.
/// </summary>
/// <remarks>
/// It lives in the Game layer rather than Domain because a load is not yet a domain fact; when
/// E2-01 lands <c>ParcelRecord</c>, this moves down beside <c>RoutingRules.Evaluate</c> (arch §4.5)
/// and becomes L1. Keeping it pure now is what makes that a move rather than a rewrite.
/// </remarks>
public static class GrabRules
{
    /// <summary>
    /// How far a carrier can reach, in metres. Generous on purpose: a grab that misses because
    /// the player was 5 cm short reads as broken input, which is the one thing vision §3.1
    /// forbids. The awkwardness is meant to come from the object, never from the reach.
    /// </summary>
    public const float Reach = 2.0f;

    /// <summary>
    /// Decides one grab. <paramref name="heldBy"/> is how many carriers already hold the load,
    /// and <paramref name="alreadyHolding"/> whether this carrier is one of them.
    /// </summary>
    public static GrabVerdict Evaluate(
        float distance,
        int heldBy,
        int carriersRequired,
        bool locked,
        bool alreadyHolding = false)
    {
        // Ordered most-specific first, so a locked load out of reach reports the lock. The
        // reason reaches the client, and "you cannot have this" is more use than "you missed".
        if (alreadyHolding)
        {
            return GrabVerdict.AlreadyHolding;
        }

        if (locked)
        {
            return GrabVerdict.Locked;
        }

        if (heldBy >= System.Math.Max(1, carriersRequired))
        {
            return GrabVerdict.NoSlotFree;
        }

        return distance <= Reach ? GrabVerdict.Granted : GrabVerdict.OutOfReach;
    }
}
