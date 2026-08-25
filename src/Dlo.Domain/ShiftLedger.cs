namespace Dlo.Domain;

/// <summary>
/// Accumulates what the end-of-shift report is made of (arch §4.6). Host-owned, like every
/// domain system.
/// </summary>
/// <remarks>
/// <para>
/// Vision §7 calls the report the highest value-per-hour feature in the product, and arch §4.6
/// explains why it is cheap: attribution is plumbed from the start rather than retrofitted.
/// <b>Retrofitting attribution is what makes it expensive</b>, which is why this type is
/// constructed by the session seam now, in E0-04, rather than appearing in Tier 3 alongside
/// the report that reads it.
/// </para>
/// </remarks>
// ponytail: the ledger holds nothing yet, deliberately.
// Ceiling: it records no entries, so nothing can be reported on. It exists so that the one
// construction site (arch §3.2) is real and the grep that guards it has something to find.
// Upgrade: arch §4.6 already fixes the shape - `LedgerEntry(EventKind, ActorRef, ParcelId?,
// float Amount)` and the report is a GroupBy over the list. Adding that here now would mean
// inventing ActorRef, ParcelId and EventKind ahead of E2-01 and E8, i.e. answering three
// other stories' design questions inside this one. The first story that has an actual event
// to record - E3-03's stamp is the likely first - brings the entry type with it.
public sealed class ShiftLedger;
