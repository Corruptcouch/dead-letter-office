using System.Globalization;
using System.Text.RegularExpressions;

namespace Dlo.Domain;

/// <summary>
/// Where a parcel is going, as written on it: <c>DISTRICT-BLOCK-UNIT</c>, for example
/// <c>NORTHGATE-4-118</c> (E13-02).
/// </summary>
/// <remarks>
/// <b>One schema, and deliberately no second one.</b> There is a single pattern, a single
/// parser and a single canonical rendering, because a grammar that grows a second shape with a
/// fallback bridging them is permanent: every reader afterwards has to handle both, forever
/// (standards §9). An address that does not match is rejected at content load, not discovered
/// mid-shift at the routing chart.
/// </remarks>
/// <param name="Destination">The part the facility routes on.</param>
/// <param name="Unit">The number beyond the facility's remit, carried so the label reads right.</param>
public readonly partial record struct Address(DestinationCode Destination, ushort Unit)
{
    /// <summary>The one pattern. Anchored, upper case, no optional halves.</summary>
    /// <remarks>
    /// Bounded rather than open — <c>+</c> on the district would accept a kilobyte of letters
    /// from a malformed content file and report it as a routing miss much later (standards §9:
    /// reject the whole malformed unit at the boundary).
    /// </remarks>
    public const string Pattern = "^(?<district>[A-Z]{2,12})-(?<block>[0-9]{1,3})-(?<unit>[0-9]{1,4})$";

    /// <summary>
    /// Reads an authored address. The only way to make one, so an <see cref="Address"/> that
    /// exists has already parsed.
    /// </summary>
    /// <param name="text">The authored text. Surrounding whitespace is trimmed; nothing else is.</param>
    /// <param name="address">The parsed address, or <c>default</c>.</param>
    /// <returns><c>true</c> if <paramref name="text"/> is an address.</returns>
    public static bool TryParse(string? text, out Address address)
    {
        address = default;

        var match = Grammar().Match((text ?? string.Empty).Trim());
        if (!match.Success)
        {
            return false;
        }

        // Bounded by the pattern to three and four digits, so neither can overflow its type and
        // neither needs a second check here. Invariant throughout, both ways: a culture with its
        // own digits would otherwise write a destination code the content files do not contain.
        var block = ushort.Parse(match.Groups["block"].Value, CultureInfo.InvariantCulture);
        var unit = ushort.Parse(match.Groups["unit"].Value, CultureInfo.InvariantCulture);

        // Canonical, so BLOCK 04 and BLOCK 4 are one destination rather than two that never
        // route the same way.
        var district = match.Groups["district"].Value;
        address = new Address(
            new DestinationCode($"{district}-{block.ToString(CultureInfo.InvariantCulture)}"),
            unit);

        return true;
    }

    /// <summary>
    /// Reads an authored destination — the address grammar's routable left-hand side.
    /// </summary>
    /// <param name="text">The authored destination, <c>DISTRICT-BLOCK</c>.</param>
    /// <param name="destination">The canonical destination, or <c>default</c>.</param>
    /// <returns><c>true</c> if <paramref name="text"/> is a destination.</returns>
    /// <remarks>
    /// Checked <b>through the address grammar</b> rather than beside it, by probing with a unit
    /// number and keeping the half that matters. A second pattern for destinations is exactly
    /// the two-shapes mistake standards §9 calls permanent, and this is one line instead.
    /// </remarks>
    public static bool IsDestination(string? text, out DestinationCode destination)
    {
        destination = default;

        if (!TryParse($"{(text ?? string.Empty).Trim()}-1", out var probe))
        {
            return false;
        }

        destination = probe.Destination;
        return true;
    }

    /// <summary>The address as it is written on the parcel.</summary>
    public override string ToString() =>
        $"{Destination.Value}-{Unit.ToString(CultureInfo.InvariantCulture)}";

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex Grammar();
}
