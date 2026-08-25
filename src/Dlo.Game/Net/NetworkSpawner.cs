using System;
using System.Collections.Generic;

using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// Wraps <see cref="MultiplayerSpawner"/> with a custom spawn function and a registry of
/// builders, so a client can construct a spawned node from its arguments alone (E0-05,
/// arch §5.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>The payload is a deliberate decision at every call site.</b> Godot would happily
/// replicate whatever Variant it is handed, and the lazy thing is to hand it the whole record.
/// This wrapper cannot stop you doing that, but the registry makes the payload an explicit
/// argument to <see cref="Spawn"/> rather than something that travels by accident — and arch
/// §5.3 turns that from tidiness into a rule, because the manifest a client has not scanned
/// must never be on the wire. E2-04 asserts the absence by inspecting the serialised bytes.
/// </para>
/// <para>
/// <b>Adding a spawnable type does not touch this class.</b> A type registers a key and a
/// builder; the wrapper only routes. That is the difference between content and code (arch
/// §4.1), and it is why the parcel archetypes of E13-01 can arrive without a wrapper change.
/// </para>
/// <para>
/// <b>This story introduces no RPC</b>, so arch §3.1's <c>TransferMode</c> / <c>CallLocal</c>
/// rule has nothing to bite on here — spawning rides <see cref="MultiplayerSpawner"/>'s own
/// reliable channel. Stated rather than skipped, because "no RPCs were added" and "nobody
/// thought about the RPC defaults" look identical in a diff.
/// </para>
/// </remarks>
public partial class NetworkSpawner : Node
{
    private readonly Dictionary<string, Func<Variant, Node>> _builders = new(StringComparer.Ordinal);

    private MultiplayerSpawner _spawner = null!;

    /// <summary>Where spawned nodes are parented. Must be identical on every peer.</summary>
    [Export]
    public NodePath SpawnRoot { get; set; } = new("..");

    /// <inheritdoc/>
    public override void _Ready()
    {
        _spawner = new MultiplayerSpawner { Name = "Spawner", SpawnPath = SpawnRoot };

        // The custom spawn function is what makes this a wrapper rather than a scene list:
        // Godot calls it on every peer with the same payload, so a client builds its own node
        // instead of being sent one.
        // The bang is load bearing rather than lazy: Godot's spawn function is allowed to
        // return null - that is how it declines an unknown key - but Callable.From has no way
        // to say so, so the delegate type claims non-null and the compiler believes it.
        _spawner.SpawnFunction = Callable.From<Variant, Node>(payload => Build(payload)!);
        AddChild(_spawner);
    }

    /// <summary>
    /// Teaches the spawner one kind of node.
    /// </summary>
    /// <param name="key">Names the kind on the wire. Short: it is sent with every spawn.</param>
    /// <param name="build">
    /// Builds the node from its arguments and <b>nothing else</b>. It runs on every peer,
    /// including ones that have never seen this object before, so anything it reads that did
    /// not arrive in <paramref name="build"/>'s argument is state a client cannot have.
    /// </param>
    public void Register(string key, Func<Variant, Node> build)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(build);

        _builders[key] = build;
    }

    /// <summary>Spawns a registered node on every peer. Host only.</summary>
    /// <param name="key">A key given to <see cref="Register"/>.</param>
    /// <param name="args">
    /// The whole payload, and the only thing clients get. Keep it to the few values a client
    /// needs to build something that looks right (arch §5.2).
    /// </param>
    public Node Spawn(string key, Variant args)
    {
        if (!_builders.ContainsKey(key))
        {
            // Loud, because the alternative is a host that spawns nothing and clients that are
            // never told why - and the same typo would fail identically on all four machines.
            throw new InvalidOperationException(
                $"Nothing is registered to spawn '{key}'. Registered: "
                + $"{string.Join(", ", _builders.Keys)}.");
        }

        return _spawner.Spawn(Payload(key, args));
    }

    /// <summary>The wire form: the key, then the arguments. Nothing else travels.</summary>
    public static Godot.Collections.Array Payload(string key, Variant args) => [key, args];

    /// <summary>
    /// Godot's spawn function, run on every peer including the host.
    /// </summary>
    /// <remarks>
    /// Returning <c>null</c> for an unknown key rather than throwing: content outlives the
    /// table that described it (standards §9), and a client running a slightly older build
    /// should be missing one object rather than losing the session.
    /// </remarks>
    private Node? Build(Variant payload)
    {
        var parts = payload.AsGodotArray();
        if (parts.Count != 2)
        {
            GD.PushError($"Spawn payload had {parts.Count} parts, expected 2 (key, args).");
            return null;
        }

        var key = parts[0].AsString();
        if (!_builders.TryGetValue(key, out var build))
        {
            GD.PushError($"Nothing is registered to spawn '{key}'; ignoring it.");
            return null;
        }

        return build(parts[1]);
    }
}
