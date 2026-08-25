namespace Dlo.Domain;

/// <summary>
/// The part of an <see cref="Address"/> this facility actually routes on: a district and a
/// block, and nothing finer.
/// </summary>
/// <remarks>
/// What the routing chart maps to a chute (vision §8, arch §4.5). The unit number on an address
/// is the receiving building's business, not the dead letter office's — which is why the
/// destination stops where it does.
/// </remarks>
/// <param name="Value">
/// Canonical form, <c>DISTRICT-BLOCK</c>, always upper case. Produced by
/// <see cref="Address.TryParse"/> rather than assembled by hand.
/// </param>
public readonly record struct DestinationCode(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;
}
