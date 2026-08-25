using System;

namespace Dlo.Domain;

/// <summary>
/// Owns the shift clock — the one continuously changing quantity in an otherwise event-driven
/// domain (arch §4.3).
/// </summary>
/// <remarks>
/// <para>
/// There is no fixed domain tick and no ambient clock. Domain never reads the time; the host
/// advances this and passes shift time <i>in</i> as a parameter wherever it is needed
/// (standards §0: no <c>DateTime.Now</c>, no <c>Time.GetTicksMsec()</c>, no delta time).
/// Physics runs at Godot's own fixed rate, as physics must, and the domain consumes discrete
/// events from it rather than ticking alongside it.
/// </para>
/// </remarks>
// ponytail: the shift clock, and nothing else yet.
// Ceiling: this director directs nothing - it cannot start or end a shift, has no quota, and
// knows nothing about a stint. A shift is currently just elapsed seconds.
// Upgrade: E5 (The Ratchet) adds quota arithmetic and the shift/stint progression, and E11
// adds what happens between shifts. Both build on this clock rather than replacing it, which
// is why it is here at all - the seam that constructs it is E0-04's actual subject.
public sealed class ShiftDirector
{
    /// <summary>Seconds elapsed in the current shift, host-owned.</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>
    /// Advances the shift clock by <paramref name="seconds"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="seconds"/> is negative. Time does not run backwards, and a negative
    /// delta is a caller bug that would otherwise show up much later as a report that
    /// disagrees with what players remember.
    /// </exception>
    public void Advance(float seconds)
    {
        if (seconds < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seconds), seconds, "The shift clock does not run backwards.");
        }

        ElapsedSeconds += seconds;
    }
}
