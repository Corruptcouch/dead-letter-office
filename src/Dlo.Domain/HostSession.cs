using System;
using System.Collections.Generic;

namespace Dlo.Domain;

/// <summary>
/// The host's live session: it owns the domain systems for as long as a shift is being played,
/// and knows who is connected to it.
/// </summary>
/// <remarks>
/// It receives its systems and never builds them (arch §3.2), so the L1 suite can construct a
/// session with substitutes and no engine. <c>SessionRoot</c> is the one place that does build
/// them, and the review checklist greps for a second because no test can catch it. Clients
/// construct nothing: there is no client-side <see cref="ShiftDirector"/> to drift out of step.
/// </remarks>
public sealed class HostSession
{
    private readonly HashSet<PeerId> _connectedPeers = [];

    /// <summary>
    /// Creates a session over already-built systems.
    /// </summary>
    /// <param name="director">The shift clock (arch §4.3).</param>
    /// <param name="ledger">What the end-of-shift report is made of (arch §4.6).</param>
    /// <param name="random">
    /// Injected so a bug report can carry a seed (arch §4.2). Held rather than used for now —
    /// the first system that samples anything takes it from here.
    /// </param>
    public HostSession(ShiftDirector director, ShiftLedger ledger, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(director);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(random);

        Director = director;
        Ledger = ledger;
        Random = random;
    }

    /// <summary>The shift clock this session runs on.</summary>
    public ShiftDirector Director { get; }

    /// <summary>The ledger this session's report will be built from.</summary>
    public ShiftLedger Ledger { get; }

    /// <summary>The session's randomness source.</summary>
    public IRandom Random { get; }

    /// <summary>Every peer currently connected, the host included.</summary>
    public IReadOnlyCollection<PeerId> ConnectedPeers => _connectedPeers;

    /// <summary>
    /// Records that <paramref name="peer"/> has joined.
    /// </summary>
    /// <returns><c>true</c> if this was a new peer; <c>false</c> if it was already known.</returns>
    /// <remarks>
    /// Idempotent on purpose: a transport may report the same thing twice, and a session that
    /// double-counted would report a crew of five.
    /// </remarks>
    public bool PeerJoined(PeerId peer) => _connectedPeers.Add(peer);

    /// <summary>
    /// Records that <paramref name="peer"/> has left.
    /// </summary>
    /// <returns><c>true</c> if the peer was connected; <c>false</c> if it was not known.</returns>
    /// <remarks>
    /// This forgets the <i>connection</i> and nothing else. When there is a ledger, it must
    /// survive a leaver — vision §7 and E12-04 require the report to still name someone who quit.
    /// </remarks>
    public bool PeerLeft(PeerId peer) => _connectedPeers.Remove(peer);
}
