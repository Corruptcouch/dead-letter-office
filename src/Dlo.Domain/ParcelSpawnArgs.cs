namespace Dlo.Domain;

/// <summary>
/// Everything a client is given to build a parcel it has never seen: arch §5.2's payload, in
/// arch §5.2's shape.
/// </summary>
/// <remarks>
/// <b>What is missing is the point.</b> No manifest, no destination and no policy lock travel
/// here, so a client cannot hold information it has not earned at the scan desk (arch §5.3).
/// That is a property of the wire, not of the UI, which is what makes it true rather than
/// merely displayed.
/// </remarks>
/// <param name="Id">The host-assigned <see cref="ParcelId"/>, as its bare value.</param>
/// <param name="Archetype">Which authored kind to build.</param>
/// <param name="Size">How big to build it — and, via
/// <see cref="ParcelRecord.CarriersRequiredFor"/>, how many carriers it will need.</param>
/// <param name="Condition">How battered it should look.</param>
public readonly record struct ParcelSpawnArgs(uint Id, byte Archetype, byte Size, byte Condition)
{
    /// <summary>The publishable subset of <paramref name="record"/>, and only that subset.</summary>
    public static ParcelSpawnArgs From(ParcelRecord record)
    {
        System.ArgumentNullException.ThrowIfNull(record);

        return new ParcelSpawnArgs(record.Id.Value, record.Archetype, record.Size, record.Condition);
    }
}
