using System;

namespace Dlo.Domain;

/// <summary>
/// Owns the shift clock — the one continuously changing quantity in an otherwise event-driven
/// domain (arch §4.3).
/// </summary>
/// <remarks>
/// There is no fixed domain tick and no ambient clock. Domain never reads the time; the host
/// advances this and passes shift time <i>in</i> (standards §0: no <c>DateTime.Now</c>, no
/// <c>Time.GetTicksMsec()</c>, no delta time).
/// </remarks>
// ponytail: the shift clock, and nothing else yet.
// Ceiling: it directs nothing - no start or end, no quota, no stint. A shift is elapsed seconds.
// Upgrade: E5 adds quota arithmetic and shift progression, E11 what happens between shifts. Both
// build on this clock rather than replacing it.
public sealed class ShiftDirector
{
    /// <summary>Seconds elapsed in the current shift, host-owned.</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>
    /// Advances the shift clock by <paramref name="seconds"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="seconds"/> is negative or not finite. Either is a caller bug that would
    /// otherwise surface much later as a report disagreeing with what players remember, and a
    /// non-finite one is unrecoverable: every later reading of the clock is NaN too.
    /// </exception>
    public void Advance(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seconds), seconds, "The shift clock runs forwards, by a finite amount.");
        }

        ElapsedSeconds += seconds;
    }
}
