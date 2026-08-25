namespace Dlo.Domain;

/// <summary>
/// Identifies one connected machine for the lifetime of a session.
/// </summary>
/// <remarks>
/// <para>
/// A wrapper rather than a bare <c>long</c> because standards §3 requires it of every
/// identifier: two bare longs are assignment-compatible, the compiler will not save you, and
/// the report is where you find out.
/// </para>
/// <para>
/// <b>Not an identity, and not what the report blames.</b> A peer id is a connection — it is
/// reassigned when someone reconnects, and it means nothing once the session ends. The thing
/// that survives a shift and carries culpability is <c>ActorRef</c> (arch §4.6), which does
/// not exist yet; E12-06 is where a peer gets a name that outlives its connection.
/// </para>
/// </remarks>
/// <param name="Value">The transport's own peer number. Host is 1 (arch §3.1, Godot's rule).</param>
public readonly record struct PeerId(long Value);
