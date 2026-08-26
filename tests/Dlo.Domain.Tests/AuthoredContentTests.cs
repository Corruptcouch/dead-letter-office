using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// The content the repo actually ships, checked against the rules the repo actually has
/// (E13-01, E13-05). <c>ContentTool validate</c> runs the same load; this is the copy that
/// fails a developer's suite before CI has to.
/// </summary>
/// <remarks>
/// The one test here that touches the disk, and it is worth the exception: a validator nobody
/// runs is a comment, and the authored files are the thing most likely to be edited by someone
/// who is not running the tool.
/// </remarks>
public partial class AuthoredContentTests
{
    [Fact]
    public void The_content_this_repo_ships_is_valid()
    {
        var loaded = ContentSet.TryLoad(
            Archetypes(), File("contents.csv"), File("manifests.csv"), File("routing.csv"),
            out var set,
            out var problems);

        // The problems are in the failure message, because a red build that says "expected true,
        // got false" would send someone to run the tool by hand to find out why.
        Assert.True(loaded, string.Join(Environment.NewLine, problems));
        Assert.NotNull(set);
    }

    [Fact]
    public void Every_authored_archetype_is_addressable_by_the_id_that_travels_in_spawn_args()
    {
        Assert.True(ContentSet.TryLoad(
            Archetypes(), File("contents.csv"), File("manifests.csv"), File("routing.csv"),
            out var set,
            out _));

        // The id on an archetype is the byte in ParcelSpawnArgs (arch §5.2). If these ever come
        // apart, a client builds the wrong box from a payload that looked fine on the wire.
        foreach (var archetype in set!.Archetypes)
        {
            Assert.Same(archetype, set.FindArchetype(archetype.Id));
        }
    }

    [Fact]
    public void Every_authored_manifest_routes_somewhere()
    {
        Assert.True(ContentSet.TryLoad(
            Archetypes(), File("contents.csv"), File("manifests.csv"), File("routing.csv"),
            out var set,
            out _));

        // E13-02, asserted against the shipped files rather than a fixture: the load-time check
        // is only worth anything if the content it guards is the content that ships.
        Assert.NotEmpty(set!.Manifests);
        Assert.All(set.Manifests, m => Assert.NotNull(set.Routing.ChuteFor(m.Destination)));
    }

    [Fact]
    public void At_least_one_authored_parcel_needs_two_people()
    {
        Assert.True(ContentSet.TryLoad(
            Archetypes(), File("contents.csv"), File("manifests.csv"), File("routing.csv"),
            out var set,
            out _));

        // E1-08 needs an object the domain marks as over one-person capacity, and Gate 0 needs a
        // heavy awkward box to exist at all. Content that quietly lost its only two-person load
        // would leave both testing nothing.
        Assert.Contains(set!.Archetypes, a => ParcelRecord.CarriersRequiredFor(a.Size) > 1);
    }

    [Fact]
    public void Every_authored_resource_names_its_script_in_the_case_the_disk_uses()
    {
        // res:// is the Godot project directory, which is the parent of the content directory.
        var project = Directory.GetParent(Root())!.FullName;

        foreach (var file in Archetypes())
        {
            foreach (var reference in ExternalResource().Matches(file.Text).Cast<Match>())
            {
                var referenced = reference.Groups[1].Value;

                // Windows resolves `res://Content/...` against a `content/` directory without
                // complaint; a Linux export does not, and the resource loads with no script
                // attached. So the case is asserted, which File.Exists here cannot do.
                Assert.True(
                    NamedExactly(project, referenced),
                    $"{file.Path} references 'res://{referenced}', which nothing matches exactly.");
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="referenced"/> exists under <paramref name="project"/> spelled
    /// exactly as it is written, walking a segment at a time because the host filesystem will
    /// happily answer a case-insensitive question with yes.
    /// </summary>
    private static bool NamedExactly(string project, string referenced)
    {
        var at = project;

        foreach (var segment in referenced.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Directory.EnumerateFileSystemEntries(at).FirstOrDefault(
                entry => string.Equals(Path.GetFileName(entry), segment, StringComparison.Ordinal));

            if (next is null)
            {
                return false;
            }

            at = next;
        }

        return true;
    }

    [GeneratedRegex("path=\"res://([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ExternalResource();

    private static IReadOnlyList<ContentFile> Archetypes() =>
        Directory.GetFiles(Path.Combine(Root(), "archetypes"), "*.tres")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new ContentFile(f, System.IO.File.ReadAllText(f)))
            .ToList();

    private static ContentFile File(string name)
    {
        var path = Path.Combine(Root(), name);
        return new ContentFile(path, System.IO.File.ReadAllText(path));
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Dlo.Game", "content");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"No src/Dlo.Game/content above '{AppContext.BaseDirectory}'.");
    }
}
