namespace Dlo.Domain;

/// <summary>
/// Everything the shift knows about one parcel. A Godot node is a view of this; the record is
/// never a view of a node (arch §5.1).
/// </summary>
/// <remarks>
/// Immutable, and owned only by <see cref="ParcelRegistry"/> — a second mutable copy that could
/// drift is precisely what keeping gameplay state on the node produces (standards §5).
/// </remarks>
/// <param name="Id">Host-assigned, and the only durable handle on this parcel.</param>
/// <param name="Archetype">
/// Which authored kind of parcel this is. A number rather than a subclass, because adding an
/// archetype must never add a class (arch §4.1).
/// </param>
/// <param name="Size">How big it is. Also what decides <see cref="CarriersRequired"/>.</param>
/// <param name="Condition">Visible wear, dents and damage. Physical, so clients may see it.</param>
/// <param name="IsLocked">
/// Whether policy currently forbids picking it up. <b>Host-only</b>: it is a policy fact, it is
/// not in <see cref="ParcelSpawnArgs"/>, and a client mispredicting a locked grab is the
/// intended behaviour (arch §3.3).
/// </param>
/// <param name="Manifest">
/// The paperwork, or <c>null</c> for a parcel that arrived without any — which is a dead letter
/// and not an error (arch §4.5).
/// </param>
/// <remarks>
/// <b>The manifest is host-only, harder than the lock is.</b> It is not in
/// <see cref="ParcelSpawnArgs"/> and it never reaches a client that has not scanned the box: that
/// is the whole of arch §5.3, and E3-06 is the story that enforces it at the point of sending.
/// </remarks>
// ponytail: identity, the physical facts and the paperwork, and nothing else.
// Ceiling: no tamper state, so nothing can be opened, and no ActualContents, so a declaration
// cannot be wrong. There is also no write path - a record is registered once and never changes.
// Upgrade: E2-07's tamper state is the first thing that has to change after registration; that is
// the story that gives the registry an update method.
public sealed record ParcelRecord(
    ParcelId Id,
    byte Archetype,
    byte Size,
    byte Condition,
    bool IsLocked,
    Manifest? Manifest = null)
{
    /// <summary>The smallest <see cref="Size"/> that no single carrier can lift (E1-08).</summary>
    public const byte TwoPersonSize = 3;

    /// <summary>
    /// How many carriers this load needs at once. <b>Derived, never stored</b>, so the host and a
    /// client that only ever saw <see cref="ParcelSpawnArgs"/> reach the same answer without the
    /// number going on the wire — and so no node can hold a copy that drifts (arch §3.3).
    /// </summary>
    public int CarriersRequired => CarriersRequiredFor(Size);

    /// <summary>
    /// <see cref="CarriersRequired"/> for a parcel of <paramref name="size"/>, for callers holding
    /// spawn arguments rather than a record — which is every client.
    /// </summary>
    // ponytail: capacity is read off size alone.
    // Ceiling: a small dense parcel cannot be made a two-person load, because size is the only
    // input. Nothing authored needs that yet, and E1-08 only requires that some load exceeds one
    // person.
    // Upgrade: E13-01 authors capacity per archetype, and this reads that table instead.
    public static int CarriersRequiredFor(byte size) => size >= TwoPersonSize ? 2 : 1;
}
