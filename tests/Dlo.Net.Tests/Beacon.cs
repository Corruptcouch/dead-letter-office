using System.Collections.Generic;

using Godot;

namespace Dlo.Net.Tests;

/// <summary>
/// The one thing the L3 peers actually exchange: a host-owned value that replicates
/// downward, and an intent RPC that travels upward. E0-09 asserts both.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is harness furniture, not gameplay.</b> It lives in the test project rather than
/// in <c>Dlo.Game</c> because there is no gameplay reason for it to exist yet — the real
/// replication classes are E0-06 and E2-05, and the real intents start with E1-04's
/// <c>RequestGrab</c>. What it proves is the plumbing underneath both: that a
/// <see cref="MultiplayerSynchronizer"/> configured in code moves a value from host to three
/// clients over <c>EnetTransport</c>, and that an <c>AnyPeer</c> RPC gets back.
/// </para>
/// <para>
/// <b>The sentinel is deliberately not 0 or 1.</b> A client that never received anything
/// holds the default, and a default that happens to equal the expected value is a test that
/// passes for the wrong reason forever (standards §8).
/// </para>
/// </remarks>
public partial class Beacon : Node
{
    /// <summary>The value the host publishes and every client must converge on.</summary>
    public const int Sentinel = 7;

    /// <summary>
    /// The second value, published only in <see cref="Scenario.Departure"/> and only once the
    /// leaver is gone. Converging on it is what makes E0-10's "the survivors keep functioning"
    /// an assertion rather than a hope: a survivor holding this held it <i>after</i> a peer
    /// dropped out of the session, not merely through the moment it did.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="Sentinel"/> and from the default for the same reason
    /// <see cref="Sentinel"/> is distinct from 0 — a second value that could be mistaken for
    /// the first would let the departure round pass on the strength of the first one.
    /// </remarks>
    public const int Aftermath = 11;

    /// <summary>
    /// The host-owned replicated value. <c>[Export]</c> is not decoration — a
    /// <see cref="SceneReplicationConfig"/> can only name properties Godot can see.
    /// </summary>
    [Export]
    public int Beat { get; set; }

    /// <summary>What each client reported, by peer id. Host-side only.</summary>
    public Dictionary<int, int> Reports { get; } = [];

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Authority is left at Godot's default of peer 1, which is the host (arch §3.1). A
        // synchronizer replicates FROM its authority, so this is the host publishing.
        var beat = new NodePath(".:Beat");
        var config = new SceneReplicationConfig();
        config.AddProperty(beat);
        config.PropertySetReplicationMode(beat, SceneReplicationConfig.ReplicationMode.Always);

        AddChild(new MultiplayerSynchronizer
        {
            Name = "Sync",
            ReplicationConfig = config,

            // Relative to the synchronizer, so this is the Beacon itself. Matching Godot's
            // own editor default rather than an absolute path: the peers build identical
            // trees, and an absolute path would only be a second thing to keep identical.
            RootPath = "..",

            // E0-06 is where intervals become a per-class decision (arch §3.4). Here it is
            // just "fast enough that a two-second test is not measuring the interval".
            ReplicationInterval = 0.05,
        });
    }

    /// <summary>
    /// A client telling the host what it observed. The intent half of E0-09.
    /// </summary>
    /// <param name="observed">The <see cref="Beat"/> the caller was holding when it sent.</param>
    /// <remarks>
    /// Every part of this attribute is a deliberate choice, per arch §3.1 — <c>AnyPeer</c>
    /// because clients are the callers; <c>CallLocal = false</c> because the host is not a
    /// client and must not report to itself; <c>Reliable</c> because this is a decision, not
    /// a stream.
    /// </remarks>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false,
        TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReportBeat(int observed) => Reports[Multiplayer.GetRemoteSenderId()] = observed;
}
