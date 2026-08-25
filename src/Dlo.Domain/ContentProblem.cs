namespace Dlo.Domain;

/// <summary>
/// One reason a content set was rejected: the file, the invariant it broke, and what was
/// actually there (E13-05).
/// </summary>
/// <remarks>
/// Three fields rather than one string because the whole point is that the message is
/// actionable. "Invalid" teaches nobody anything at 11pm, which is the audience this epic is
/// written for (vision §13).
/// </remarks>
/// <param name="File">The authored file's path.</param>
/// <param name="Line">The 1-based line, or 0 where the problem is the file as a whole.</param>
/// <param name="Invariant">The rule, stated as the rule — not as its violation.</param>
/// <param name="Detail">What was found instead.</param>
public sealed record ContentProblem(string File, int Line, string Invariant, string Detail)
{
    /// <summary>One line, in the shape an editor's error list can jump to.</summary>
    public override string ToString() =>
        Line > 0
            ? $"{File}({Line}): {Invariant} — {Detail}"
            : $"{File}: {Invariant} — {Detail}";
}
