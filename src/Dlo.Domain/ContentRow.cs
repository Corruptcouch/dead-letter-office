using System.Collections.Generic;

namespace Dlo.Domain;

/// <summary>One row of an authored data table, with the line it came from.</summary>
/// <param name="Line">1-based, so a problem can point at it.</param>
/// <param name="Fields">The row's cells, trimmed, in authored order.</param>
public sealed record ContentRow(int Line, IReadOnlyList<string> Fields);
