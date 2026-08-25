using System;
using System.Collections.Generic;

namespace Dlo.Domain;

/// <summary>
/// The only owner of parcel state: it assigns every <see cref="ParcelId"/> and maps each one to
/// its <see cref="ParcelRecord"/> (arch §5.1).
/// </summary>
/// <remarks>
/// Host-owned, like every domain system, and it never forgets — a parcel destroyed in the first
/// minute still has to be nameable when the report is built at the whistle (vision §7).
/// </remarks>
public sealed class ParcelRegistry
{
    private readonly Dictionary<ParcelId, ParcelRecord> _records = [];

    // Counted from one, so `default(ParcelId)` resolves to nothing instead of to whichever
    // parcel happened to be registered first. Do not "tidy" this to zero.
    private uint _nextId = 1;

    /// <summary>
    /// Assigns the next id and records a parcel under it.
    /// </summary>
    /// <param name="carriersRequired">
    /// How many carriers the load needs at once (arch §3.3). At least one.
    /// </param>
    /// <param name="isLocked">Whether policy currently forbids picking it up.</param>
    /// <returns>The stored record, carrying the id it was just given.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="carriersRequired"/> is below one. A parcel nobody has to carry is a
    /// caller bug that would otherwise surface as a load behaving like scenery.
    /// </exception>
    public ParcelRecord Register(int carriersRequired, bool isLocked)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(carriersRequired, 1);

        var record = new ParcelRecord(new ParcelId(_nextId++), carriersRequired, isLocked);
        _records[record.Id] = record;
        return record;
    }

    /// <summary>
    /// Finds the parcel that <paramref name="id"/> names.
    /// </summary>
    /// <param name="id">An id this registry assigned.</param>
    /// <returns>
    /// The record, or <c>null</c> if no parcel was ever registered under that id.
    /// </returns>
    public ParcelRecord? Find(ParcelId id) => _records.GetValueOrDefault(id);
}
