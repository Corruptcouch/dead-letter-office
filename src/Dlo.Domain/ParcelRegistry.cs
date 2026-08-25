using System.Collections.Generic;

namespace Dlo.Domain;

/// <summary>
/// The only owner of parcel state: it assigns every <see cref="ParcelId"/> and maps each one to
/// its <see cref="ParcelRecord"/> (arch §5.1).
/// </summary>
/// <remarks>
/// Host-owned, like every domain system, and it never forgets — a parcel destroyed in the first
/// minute still has to be nameable when the report is built at the whistle (vision §7). A node
/// being freed, pooled or respawned does not reach this class at all, which is the whole point.
/// </remarks>
public sealed class ParcelRegistry
{
    private readonly Dictionary<ParcelId, ParcelRecord> _records = [];

    // Counted from one, so `default(ParcelId)` resolves to nothing instead of to whichever
    // parcel happened to be registered first. Do not "tidy" this to zero.
    private uint _nextId = 1;

    /// <summary>How many parcels have been registered this shift.</summary>
    public int Count => _records.Count;

    /// <summary>
    /// Assigns the next id and records a parcel under it.
    /// </summary>
    /// <param name="archetype">Which authored kind of parcel this is.</param>
    /// <param name="size">How big it is, which is also what decides its carrier count.</param>
    /// <param name="condition">Visible wear.</param>
    /// <param name="isLocked">Whether policy forbids picking it up. Host-only.</param>
    /// <returns>The stored record, carrying the id it was just given.</returns>
    public ParcelRecord Register(byte archetype, byte size, byte condition, bool isLocked = false)
    {
        var record = new ParcelRecord(new ParcelId(_nextId++), archetype, size, condition, isLocked);
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
