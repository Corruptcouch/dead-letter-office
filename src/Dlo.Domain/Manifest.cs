namespace Dlo.Domain;

/// <summary>
/// The paperwork attached to a parcel: where it claims to be going and what it claims to be
/// (E2-03).
/// </summary>
/// <remarks>
/// <b>Every field is a Domain type</b>, so the whole thing serialises to numbers and strings and
/// carries no engine type anywhere (standards §9). It is also the thing a client does not get
/// until it has scanned the parcel — which is enforced by not putting it in
/// <see cref="ParcelSpawnArgs"/>, not by hiding it in a UI (arch §5.3).
/// </remarks>
/// <param name="Address">Where it is going. Already parsed, so it is already routable.</param>
/// <param name="Weight">Kilograms, as declared on the paperwork.</param>
/// <param name="Fragility">
/// How badly it takes a fall, 0 to 255. Declared, so it can be wrong.
/// </param>
/// <param name="DeclaredContents">What the label claims is inside.</param>
// ponytail: every field here is a declaration, and nothing yet compares one to reality.
// Ceiling: a manifest cannot be wrong about anything, because ActualContents does not exist and
// nothing weighs a parcel. The declaration/reality gap is the game's thesis (vision §9) and this
// is only the declaring half of it.
// Upgrade: E2-08 adds ActualContents to ParcelRecord - not here, because a manifest is what the
// label says - and the comparison becomes possible the moment a box can be opened (E2-07).
public sealed record Manifest(
    Address Address,
    float Weight,
    byte Fragility,
    ContentsCode DeclaredContents)
{
    /// <summary>Where the facility routes this, which is all it routes on (arch §4.5).</summary>
    public DestinationCode Destination => Address.Destination;
}
