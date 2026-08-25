namespace Dlo.Domain;

/// <summary>
/// One authored kind of parcel (E13-01). Adding a kind is a content file, never a class
/// (arch §4.1).
/// </summary>
/// <remarks>
/// The archetype is the authoring-time template; <see cref="ParcelRecord"/> is the instance the
/// shift actually has. Everything here is checked at content load, so an archetype that exists
/// is one whose mass is sane and whose declared contents resolve.
/// </remarks>
/// <param name="Id">
/// Matches <see cref="ParcelRecord.Archetype"/> and travels in <see cref="ParcelSpawnArgs"/>, so
/// it is a byte and stays one.
/// </param>
/// <param name="Name">What an author calls it. For content errors and the authoring guide.</param>
/// <param name="Mass">Kilograms. Sanity-checked against <see cref="MinMass"/>
/// and <see cref="MaxMass"/>.</param>
/// <param name="Size">Drives <see cref="ParcelRecord.CarriersRequiredFor"/>.</param>
/// <param name="DeclaredContents">What the label claims. Must appear in the contents table.</param>
public sealed record ParcelArchetype(
    byte Id,
    string Name,
    float Mass,
    byte Size,
    ContentsCode DeclaredContents)
{
    /// <summary>Lighter than this is not a parcel, it is a bug in a content file.</summary>
    public const float MinMass = 0.1f;

    /// <summary>
    /// Heavier than this and E1-01's stability envelope stops applying.
    /// </summary>
    /// <remarks>
    /// The number is a content guard, not a physics claim: E1-01 measured what Jolt stays stable
    /// carrying, and an archetype authored past it would fail as jitter in a playtest rather than
    /// as an error anyone could trace back to the file.
    /// </remarks>
    public const float MaxMass = 120.0f;

    /// <summary>The largest authored size. Above this nothing has a mesh to show.</summary>
    public const byte MaxSize = 6;
}
