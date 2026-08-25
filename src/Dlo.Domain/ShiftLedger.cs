namespace Dlo.Domain;

/// <summary>
/// Accumulates what the end-of-shift report is made of (arch §4.6). Host-owned, like every
/// domain system.
/// </summary>
/// <remarks>
/// Constructed by the session seam now rather than in Tier 3 because attribution is cheap when
/// plumbed from the start and expensive when retrofitted (vision §7, arch §4.6).
/// </remarks>
// ponytail: the ledger holds nothing yet.
// Ceiling: it records no entries, so nothing can be reported on. It exists so the one
// construction site (arch §3.2) is real and the grep guarding it has something to find.
// Upgrade: arch §4.6 fixes the shape - LedgerEntry(EventKind, ActorRef, ParcelId?, float Amount),
// and the report is a GroupBy. Adding it now would mean inventing ActorRef, ParcelId and
// EventKind ahead of E2-01 and E8. The first story with an event to record brings the entry type.
public sealed class ShiftLedger;
