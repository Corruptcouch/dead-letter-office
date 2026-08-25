namespace Dlo.Domain;

/// <summary>
/// Identifies one chute on the chute floor (vision §8).
/// </summary>
/// <remarks>
/// A wrapper rather than a bare number because standards §3 requires it of every identifier, and
/// because a chute number and a destination's block number are both small integers that would
/// otherwise be assignment-compatible.
/// </remarks>
/// <param name="Value">The chute's authored number. Zero is no chute.</param>
public readonly record struct ChuteId(byte Value);
