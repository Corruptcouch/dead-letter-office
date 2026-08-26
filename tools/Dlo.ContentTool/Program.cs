using System;
using System.IO;
using System.Linq;

using Dlo.Domain;

// E13-05. Reads the authored content set off the disk, hands it to Domain to be judged, and
// turns the verdict into an exit code CI can fail on (E13-06).
//
// The split is deliberate: this file does I/O and formatting, Domain does every rule. That is
// what keeps the rules L1-testable without a directory of fixture files, and it is why a
// content problem reads the same here as it does in a test.
if (args.Length == 0 || !string.Equals(args[0], "validate", StringComparison.Ordinal))
{
    Console.Error.WriteLine("usage: Dlo.ContentTool validate [<content directory>]");
    return 2;
}

var root = args.Length > 1 ? args[1] : ContentDirectory();

if (root is null || !Directory.Exists(root))
{
    Console.Error.WriteLine($"No content directory at '{root ?? "(not found)"}'.");
    return 2;
}

var archetypeDirectory = Path.Combine(root, "archetypes");

// A missing table reads as an empty one below, and an empty table breaks no rule - so without
// this an empty directory validates, and the gate reports a content set nobody authored as sound.
var missing = new[] { "contents.csv", "manifests.csv", "routing.csv" }
    .Select(name => Path.Combine(root, name))
    .Where(path => !File.Exists(path))
    .ToList();

if (!Directory.Exists(archetypeDirectory)
    || !Directory.EnumerateFiles(archetypeDirectory, "*.tres").Any())
{
    missing.Add(archetypeDirectory);
}

if (missing.Count > 0)
{
    // 2 rather than 1: nothing was judged. "Invalid content" and "there is no content" send an
    // author to different places, and only the second is ever a broken checkout.
    Console.Error.WriteLine(
        $"Content set is incomplete at '{root}'. Missing: {string.Join(", ", missing)}.");
    return 2;
}

var archetypes = Directory.GetFiles(archetypeDirectory, "*.tres")
    .OrderBy(f => f, StringComparer.Ordinal)
    .Select(Read)
    .ToList();

var loaded = ContentSet.TryLoad(
    archetypes,
    Read(Path.Combine(root, "contents.csv")),
    Read(Path.Combine(root, "manifests.csv")),
    Read(Path.Combine(root, "routing.csv")),
    out var content,
    out var problems);

foreach (var problem in problems)
{
    Console.Error.WriteLine(problem.ToString());
}

if (!loaded)
{
    // Named rather than counted alone: "3 problems" sends an author looking, and the lines above
    // already told them where. This one is the summary CI shows in a collapsed log.
    Console.Error.WriteLine(
        $"Content is invalid: {problems.Count} problem{(problems.Count == 1 ? string.Empty : "s")}.");
    return 1;
}

Console.WriteLine(
    $"Content is valid: {content!.Archetypes.Count} archetypes, {content.Contents.Count} contents codes, "
    + $"{content.Manifests.Count} manifests, {content.Routing.Destinations.Count} destinations.");

return 0;

// A missing file is read as empty rather than thrown on, so the report is "this table is
// missing its rows" from Domain instead of a stack trace from here.
static ContentFile Read(string path) =>
    new(path, File.Exists(path) ? File.ReadAllText(path) : string.Empty);

// Walks up from the running binary to the repo root, so `Dlo.ContentTool validate` with no
// argument works from anywhere - including CI, which runs it from the checkout root.
static string? ContentDirectory()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "src", "Dlo.Game", "content");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}
