using Dlo.Domain;

using Godot;

namespace Dlo.Game.Carry;

/// <summary>
/// The one place <see cref="ParcelSpawnArgs"/> becomes a Godot value or the reverse (E2-04), and
/// the builder a <c>NetworkSpawner</c> is taught (arch §5.2).
/// </summary>
/// <remarks>
/// <see cref="Build"/> runs on every peer, including one that has never seen this parcel, and it
/// reads nothing but its argument. Anything it needed beyond that would be state a client cannot
/// have — which is how arch §5.3's asymmetry stays a property of the wire.
/// </remarks>
public static class ParcelSpawn
{
    /// <summary>Names parcels on the wire. Short, because it is sent with every spawn.</summary>
    public const string Key = "parcel";

    /// <summary>
    /// How many values a payload carries — arch §5.2's four, and what E2-04's negative test
    /// counts to prove nothing else got in.
    /// </summary>
    public const int Fields = 4;

    /// <summary>
    /// The wire form: four numbers in arch §5.2's order, and nothing else.
    /// </summary>
    public static Godot.Collections.Array ToPayload(ParcelSpawnArgs args) =>
        [args.Id, args.Archetype, args.Size, args.Condition];

    /// <summary>Reads back what <see cref="ToPayload"/> wrote.</summary>
    /// <exception cref="System.ArgumentException">
    /// The payload is not four values. A short payload is a protocol mismatch, and building half
    /// a parcel from it would surface three rooms away as a box with the wrong contents.
    /// </exception>
    public static ParcelSpawnArgs FromPayload(Variant payload)
    {
        var parts = payload.AsGodotArray();
        if (parts.Count != Fields)
        {
            throw new System.ArgumentException(
                $"A parcel payload is {Fields} values; this one had {parts.Count}.", nameof(payload));
        }

        return new ParcelSpawnArgs(
            (uint)parts[0].AsInt64(),
            (byte)parts[1].AsInt64(),
            (byte)parts[2].AsInt64(),
            (byte)parts[3].AsInt64());
    }

    /// <summary>Builds a parcel from its arguments and nothing else. Runs on every peer.</summary>
    public static Node Build(Variant payload) => Configure(new Carryable(), FromPayload(payload));

    /// <summary>
    /// Points <paramref name="parcel"/> at the parcel <paramref name="args"/> describes, replacing
    /// everything it was showing before.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Build"/> because E2-06 recycles a node rather than making one, and
    /// a pool that configured differently from a fresh spawn would be a source of bugs that only
    /// appear after the pool warms up.
    /// </remarks>
    public static Carryable Configure(Carryable parcel, ParcelSpawnArgs args)
    {
        System.ArgumentNullException.ThrowIfNull(parcel);

        parcel.Id = new ParcelId(args.Id);
        parcel.Archetype = args.Archetype;
        parcel.Size = args.Size;
        parcel.Condition = args.Condition;
        parcel.Name = $"Parcel{args.Id}";
        return parcel;
    }
}
