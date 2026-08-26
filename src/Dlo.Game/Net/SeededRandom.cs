using System;
using System.Collections.Generic;

using Dlo.Domain;

namespace Dlo.Game.Net;

/// <summary>
/// The Game layer's <see cref="IRandom"/>, over <see cref="System.Random"/> and an explicit
/// seed (arch §4.2).
/// </summary>
/// <remarks>
/// The seed is the point: it is reported at session start so a bug report can carry it and the
/// same shift can be regenerated. Not <c>GD.Randi()</c> or <c>Random.Shared</c> — neither replays.
/// </remarks>
/// <param name="seed">The seed to replay. Record it anywhere a bug might be reported from.</param>
public sealed class SeededRandom(int seed) : IRandom
{
    private readonly Random _random = new(seed);

    /// <summary>The seed this instance was created with.</summary>
    public int Seed { get; } = seed;

    /// <inheritdoc/>
    public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    /// <inheritdoc/>
    public float NextFloat() => (float)_random.NextDouble();

    /// <inheritdoc/>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
        }

        return items[_random.Next(items.Count)];
    }

    /// <inheritdoc/>
    public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weight)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(weight);
        if (items.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
        }

        var total = 0f;
        for (var i = 0; i < items.Count; i++)
        {
            var w = weight(items[i]);
            if (!float.IsFinite(w) || w < 0f)
            {
                // Non-finite as well as negative: NaN fails every comparison below and infinity
                // swallows the roll, so both present as a draw that is fixed rather than random.
                throw new ArgumentException(
                    $"Weight {w} at index {i} is not a finite proportion.",
                    nameof(weight));
            }

            total += w;
        }

        // All-zero weights would otherwise land past the end of the list and throw somewhere
        // less informative. Uniform is the sane reading of "nothing is more likely".
        if (total <= 0f)
        {
            return items[_random.Next(items.Count)];
        }

        var roll = (float)(_random.NextDouble() * total);
        for (var i = 0; i < items.Count; i++)
        {
            roll -= weight(items[i]);
            if (roll < 0f)
            {
                return items[i];
            }
        }

        // Floating-point accumulation can leave `roll` a hair above zero after the last
        // subtraction. The last item is the correct answer, not an error.
        return items[^1];
    }
}
