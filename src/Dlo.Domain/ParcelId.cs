namespace Dlo.Domain;

/// <summary>
/// Identifies one parcel for its whole life, across every node that ever shows it.
/// </summary>
/// <remarks>
/// Host-assigned by <see cref="ParcelRegistry"/>, and it survives tube transit, pooling and the
/// Godot node being freed (arch §5.1). A wrapper rather than a bare <c>uint</c> because
/// standards §3 requires it of every identifier: two bare uints are assignment-compatible, and
/// the report is where you find out.
/// </remarks>
/// <param name="Value">
/// The registry's own parcel number, counted from one — so a <c>default</c>
/// <see cref="ParcelId"/> names no parcel rather than the first one registered.
/// </param>
public readonly record struct ParcelId(uint Value);
