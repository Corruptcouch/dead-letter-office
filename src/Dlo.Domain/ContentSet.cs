using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Dlo.Domain;

/// <summary>
/// Every authored thing the shift draws on, after it has been checked (E13-01, E13-02, E13-03).
/// </summary>
/// <remarks>
/// <b>All or nothing.</b> A content set with one bad row does not load with that row missing; it
/// does not load. Partially applying a malformed unit is how a house project ended up simulating
/// a board nobody had described (standards §9), and here it would be a shift with a destination
/// that routes nowhere.
/// <para>
/// A <i>lookup</i> that misses is a different thing entirely, and degrades: see
/// <see cref="FindArchetype"/>.
/// </para>
/// </remarks>
public sealed partial class ContentSet
{
    private readonly Dictionary<byte, ParcelArchetype> _archetypes;
    private readonly HashSet<ContentsCode> _contents;

    private ContentSet(
        Dictionary<byte, ParcelArchetype> archetypes,
        HashSet<ContentsCode> contents,
        List<Manifest> manifests,
        RoutingPolicy routing)
    {
        _archetypes = archetypes;
        _contents = contents;
        Manifests = manifests;
        Routing = routing;
    }

    /// <summary>Which chute each destination goes down.</summary>
    public RoutingPolicy Routing { get; }

    /// <summary>Every authored manifest, in file order.</summary>
    public IReadOnlyList<Manifest> Manifests { get; }

    /// <summary>Every authored parcel kind.</summary>
    public IReadOnlyCollection<ParcelArchetype> Archetypes => _archetypes.Values;

    /// <summary>Every contents code a manifest or an archetype may name.</summary>
    public IReadOnlyCollection<ContentsCode> Contents => _contents;

    /// <summary>
    /// The archetype <paramref name="id"/> names, or <c>null</c> if this content set has never
    /// heard of it.
    /// </summary>
    /// <remarks>
    /// <b>Unknown ids degrade; they do not crash</b> (standards §9). A save, a replay or a peer
    /// on an older build can name an archetype this table no longer has, and the answer is one
    /// inert parcel and a log line rather than a dead shift. The caller logs — Domain has no
    /// output of its own.
    /// </remarks>
    public ParcelArchetype? FindArchetype(byte id) => _archetypes.GetValueOrDefault(id);

    /// <summary>
    /// Reads and checks a whole content set.
    /// </summary>
    /// <param name="archetypes">One <c>.tres</c> per parcel kind.</param>
    /// <param name="contents">The contents table: one code per row.</param>
    /// <param name="manifests">The manifest table: address, weight, fragility, contents.</param>
    /// <param name="routing">The routing table: destination, chute.</param>
    /// <param name="set">The loaded content, or <c>null</c> if anything was wrong.</param>
    /// <param name="problems">Everything wrong with it, or empty. Never null.</param>
    /// <returns><c>true</c> if the content set loaded.</returns>
    /// <remarks>
    /// Every problem is collected rather than thrown on the first: an author who fixes content
    /// one error per run is an author who stops running the validator.
    /// </remarks>
    public static bool TryLoad(
        IReadOnlyList<ContentFile> archetypes,
        ContentFile contents,
        ContentFile manifests,
        ContentFile routing,
        out ContentSet? set,
        out IReadOnlyList<ContentProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(archetypes);

        var found = new List<ContentProblem>();

        var codes = ReadContents(contents, found);
        var policy = new RoutingPolicy(ReadRoutes(routing, found));
        var kinds = ReadArchetypes(archetypes, codes, found);
        var papers = ReadManifests(manifests, codes, policy, found);

        problems = found;
        set = found.Count == 0 ? new ContentSet(kinds, codes, papers, policy) : null;
        return set is not null;
    }

    private static HashSet<ContentsCode> ReadContents(ContentFile file, List<ContentProblem> found)
    {
        var codes = new HashSet<ContentsCode>();

        foreach (var row in ContentText.Rows(file.Text))
        {
            var code = row.Fields[0];

            if (!Code().IsMatch(code))
            {
                found.Add(new ContentProblem(
                    file.Path,
                    row.Line,
                    "a contents code is upper case, 2 to 24 of A-Z, 0-9 and underscore",
                    Show(code)));
                continue;
            }

            if (!codes.Add(new ContentsCode(code)))
            {
                found.Add(new ContentProblem(
                    file.Path, row.Line, "each contents code is declared once", Show(code)));
            }
        }

        return codes;
    }

    private static Dictionary<DestinationCode, ChuteId> ReadRoutes(
        ContentFile file,
        List<ContentProblem> found)
    {
        var routes = new Dictionary<DestinationCode, ChuteId>();

        foreach (var row in ContentText.Rows(file.Text))
        {
            if (row.Fields.Count != 2)
            {
                found.Add(new ContentProblem(
                    file.Path, row.Line, "a route is destination,chute", $"{row.Fields.Count} fields"));
                continue;
            }

            if (!Address.IsDestination(row.Fields[0], out var destination))
            {
                found.Add(new ContentProblem(
                    file.Path, row.Line, "a destination is DISTRICT-BLOCK", Show(row.Fields[0])));
                continue;
            }

            if (!byte.TryParse(row.Fields[1], CultureInfo.InvariantCulture, out var chute) || chute == 0)
            {
                found.Add(new ContentProblem(
                    file.Path, row.Line, "a chute is a number from 1 to 255", Show(row.Fields[1])));
                continue;
            }

            // E13-03's whole point. Last-one-wins here would be a shift where the chart on the
            // wall and the scoring at the whistle disagree, and nothing would ever say so.
            if (!routes.TryAdd(destination, new ChuteId(chute)))
            {
                found.Add(new ContentProblem(
                    file.Path,
                    row.Line,
                    "each destination maps to exactly one chute",
                    $"{destination} already goes to chute {routes[destination].Value}"));
            }
        }

        return routes;
    }

    private static Dictionary<byte, ParcelArchetype> ReadArchetypes(
        IReadOnlyList<ContentFile> files,
        HashSet<ContentsCode> codes,
        List<ContentProblem> found)
    {
        var kinds = new Dictionary<byte, ParcelArchetype>();

        foreach (var file in files)
        {
            var values = ContentText.Resource(file.Text);
            var before = found.Count;

            if (!Number(values, "id", out byte id))
            {
                found.Add(new ContentProblem(
                    file.Path, 0, "an archetype has a numeric id", Missing(values, "id")));
            }

            var name = values.GetValueOrDefault("name", string.Empty);
            if (name.Length == 0)
            {
                found.Add(new ContentProblem(file.Path, 0, "an archetype has a name", "absent"));
            }

            if (!Mass(values, out var mass))
            {
                found.Add(new ContentProblem(
                    file.Path,
                    0,
                    $"mass is {ParcelArchetype.MinMass} to {ParcelArchetype.MaxMass} kg",
                    Missing(values, "mass")));
            }

            if (!Number(values, "size", out byte size) || size == 0 || size > ParcelArchetype.MaxSize)
            {
                found.Add(new ContentProblem(
                    file.Path,
                    0,
                    $"size is 1 to {ParcelArchetype.MaxSize}",
                    Missing(values, "size")));
            }

            var declared = new ContentsCode(values.GetValueOrDefault("declared_contents", string.Empty));
            if (!codes.Contains(declared))
            {
                found.Add(new ContentProblem(
                    file.Path,
                    0,
                    "declared contents are declared in the contents table",
                    Show(declared.Value)));
            }

            // Only a whole, sound archetype is added. A half-built one would go on to collide
            // with a real id, and the second message would send the author to the wrong file.
            if (found.Count != before)
            {
                continue;
            }

            if (!kinds.TryAdd(id, new ParcelArchetype(id, name, mass, size, declared)))
            {
                found.Add(new ContentProblem(
                    file.Path,
                    0,
                    "each archetype id is used once",
                    $"id {id} is already '{kinds[id].Name}'"));
            }
        }

        return kinds;
    }

    private static List<Manifest> ReadManifests(
        ContentFile file,
        HashSet<ContentsCode> codes,
        RoutingPolicy policy,
        List<ContentProblem> found)
    {
        var papers = new List<Manifest>();

        foreach (var row in ContentText.Rows(file.Text))
        {
            if (row.Fields.Count != 4)
            {
                found.Add(new ContentProblem(
                    file.Path,
                    row.Line,
                    "a manifest is address,weight,fragility,contents",
                    $"{row.Fields.Count} fields"));
                continue;
            }

            if (!Address.TryParse(row.Fields[0], out var address))
            {
                found.Add(new ContentProblem(
                    file.Path, row.Line, "an address is DISTRICT-BLOCK-UNIT", Show(row.Fields[0])));
                continue;
            }

            // E13-02's criterion, and the reason it is checked here rather than at the chart: a
            // destination discovered to be unroutable mid-shift is a bug nobody can trace back
            // to a file.
            if (policy.ChuteFor(address.Destination) is null)
            {
                found.Add(new ContentProblem(
                    file.Path,
                    row.Line,
                    "an address parses to a routable destination",
                    $"{address.Destination} is in no route"));
                continue;
            }

            if (!float.TryParse(row.Fields[1], CultureInfo.InvariantCulture, out var weight) || weight <= 0)
            {
                found.Add(new ContentProblem(
                    file.Path, row.Line, "a declared weight is above zero", Show(row.Fields[1])));
                continue;
            }

            if (!byte.TryParse(row.Fields[2], CultureInfo.InvariantCulture, out var fragility))
            {
                found.Add(new ContentProblem(
                    file.Path, row.Line, "fragility is 0 to 255", Show(row.Fields[2])));
                continue;
            }

            var declared = new ContentsCode(row.Fields[3]);
            if (!codes.Contains(declared))
            {
                found.Add(new ContentProblem(
                    file.Path,
                    row.Line,
                    "declared contents are declared in the contents table",
                    Show(row.Fields[3])));
                continue;
            }

            papers.Add(new Manifest(address, weight, fragility, declared));
        }

        return papers;
    }

    private static bool Number(IReadOnlyDictionary<string, string> values, string key, out byte value) =>
        byte.TryParse(values.GetValueOrDefault(key), CultureInfo.InvariantCulture, out value);

    private static bool Mass(IReadOnlyDictionary<string, string> values, out float mass) =>
        float.TryParse(values.GetValueOrDefault("mass"), CultureInfo.InvariantCulture, out mass)
        && mass >= ParcelArchetype.MinMass
        && mass <= ParcelArchetype.MaxMass;

    private static string Missing(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? Show(value) : "absent";

    private static string Show(string? value) =>
        string.IsNullOrEmpty(value) ? "empty" : $"'{value}'";

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,23}$", RegexOptions.CultureInvariant)]
    private static partial Regex Code();
}
