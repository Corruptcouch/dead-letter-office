namespace Dlo.Domain;

/// <summary>
/// One authored file, already read off the disk. Domain never opens a file (standards §0), so
/// the caller does the I/O and hands the text over with its path attached.
/// </summary>
/// <param name="Path">
/// Where it came from, carried only so a problem can name it. A validator whose output does not
/// say which file is wrong teaches nobody anything at 11pm (E13-05).
/// </param>
/// <param name="Text">The file's whole contents.</param>
public readonly record struct ContentFile(string Path, string Text);
