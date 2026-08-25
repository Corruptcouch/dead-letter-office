using Dlo.Domain;

using Godot;

namespace Dlo.Game.Content;

/// <summary>
/// The editor-facing face of a parcel archetype: what an author fills in, saved as a
/// <c>.tres</c> (arch §7, E13-01).
/// </summary>
/// <remarks>
/// Flat scalars on purpose. <c>ContentTool</c> reads these files as text, because it may
/// reference Domain and nothing else (arch §1.3) — so an array, a dictionary or a sub-resource
/// here would be a property the validator cannot see.
/// <para>
/// <b>Authoring shape only.</b> <see cref="ParcelArchetype"/> is the checked one, and
/// <c>ContentSet</c> is what turns a file into it — nothing trusts these values.
/// </para>
/// </remarks>
[GlobalClass]
public partial class ParcelArchetypeResource : Resource
{
    /// <summary>Matches <see cref="ParcelRecord.Archetype"/>. Unique across the content set.</summary>
    [Export]
    public int Id { get; set; }

    /// <summary>What an author calls it. Shown in content errors, never to a player.</summary>
    [Export]
    public string Name { get; set; } = string.Empty;

    /// <summary>Kilograms, within <see cref="ParcelArchetype.MinMass"/>
    /// and <see cref="ParcelArchetype.MaxMass"/>.</summary>
    [Export]
    public float Mass { get; set; } = 1.0f;

    /// <summary>1 to <see cref="ParcelArchetype.MaxSize"/>. Also decides the carrier count.</summary>
    [Export]
    public int Size { get; set; } = 1;

    /// <summary>A code from the contents table. What the label claims is inside.</summary>
    [Export]
    public string DeclaredContents { get; set; } = string.Empty;
}
