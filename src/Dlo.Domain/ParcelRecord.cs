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
/// <param name="CarriersRequired">
/// How many carriers the load needs at once. Two means one player cannot lift it, and the domain
/// decides that rather than the joint (arch §3.3).
/// </param>
/// <param name="IsLocked">
/// Whether policy currently forbids picking it up. The host validates this on every grab.
/// </param>
// ponytail: identity, plus the two facts the carry already needed, and nothing else.
// Ceiling: no manifest, address, destination or tamper state, so nothing here can be routed or
// judged yet. There is also no write path - a record is registered once and never changes.
// Upgrade: E2-03 brings the manifest model, and E2-07's tamper state is the first thing that has
// to change after registration; that is the story that gives the registry an update method.
public sealed record ParcelRecord(ParcelId Id, int CarriersRequired, bool IsLocked);
