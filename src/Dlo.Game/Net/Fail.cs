using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// Turns Godot's returned <see cref="Error"/> codes into thrown exceptions at the transport
/// boundary (standards §9: reject the whole malformed unit rather than partially applying it).
/// </summary>
internal static class Fail
{
    /// <summary>
    /// Throws unless <paramref name="error"/> is <see cref="Error.Ok"/>.
    /// </summary>
    /// <remarks>
    /// A half-created peer is worse than no peer: it reports a connection status, so callers
    /// poll it forever instead of failing. The message names the attempt, because
    /// <c>Error.CantCreate</c> on its own has cost people an evening.
    /// </remarks>
    internal static void IfNotOk(Error error, string attempt)
    {
        if (error != Error.Ok)
        {
            throw new System.InvalidOperationException($"Transport failed to {attempt}: {error}.");
        }
    }
}
