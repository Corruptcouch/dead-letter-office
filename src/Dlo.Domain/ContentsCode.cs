namespace Dlo.Domain;

/// <summary>
/// What a manifest claims is inside a parcel, as an authored code rather than prose.
/// </summary>
/// <remarks>
/// A code so it can be checked: an archetype naming contents that no content file declares is a
/// build failure rather than a box that says nothing (E13-01). The declaration is only ever a
/// claim — E2-08's <c>ActualContents</c> is the other half, and the gap between them is the
/// game's thesis in miniature (vision §9).
/// </remarks>
/// <param name="Value">The authored code, upper case, as it appears in the contents table.</param>
public readonly record struct ContentsCode(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;
}
