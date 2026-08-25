using System.Collections.Generic;

using Godot;

namespace Dlo.Game.Net;

/// <summary>
/// Measures what a <see cref="MultiplayerSynchronizer"/> actually costs, by watching the values
/// it is configured to send rather than by reading the configuration and believing it.
/// </summary>
/// <remarks>
/// Arch §3.4's claim — a railed parcel produces <b>no ongoing traffic at all</b> — is the load
/// bearing one in the whole replication budget, and E4-01, E2-05, E4-03, E2-10 and E4-10 all
/// require it proved by measurement. This is the instrument they share.
/// <para>
/// A development tool that lives in <c>src</c> for the same reason <see cref="LatencyPeer"/>
/// does: both suites need it, and they are separate projects.
/// </para>
/// </remarks>
public sealed class ReplicationMeter
{
    private readonly List<Watched> _watched = [];
    private readonly MultiplayerSynchronizer _synchronizer;

    /// <summary>
    /// Reads <paramref name="synchronizer"/>'s configuration and takes a first sample, so
    /// <see cref="Changes"/> counts what happens from now on.
    /// </summary>
    /// <param name="synchronizer">The synchronizer to meter. Its node must be in the tree.</param>
    public ReplicationMeter(MultiplayerSynchronizer synchronizer)
    {
        System.ArgumentNullException.ThrowIfNull(synchronizer);
        _synchronizer = synchronizer;

        var config = synchronizer.ReplicationConfig;
        if (config is null)
        {
            return;
        }

        var root = synchronizer.GetNode(synchronizer.RootPath);

        foreach (var property in config.GetProperties())
        {
            var mode = config.PropertyGetReplicationMode(property);
            if (mode == SceneReplicationConfig.ReplicationMode.Never)
            {
                // Never means no bytes leave because of this property, whatever it does. It is
                // not watched, and that is the entire point of the Railed class.
                continue;
            }

            var target = root.GetNodeOrNull(new NodePath(property.GetConcatenatedNames()));
            if (target is null)
            {
                continue;
            }

            _watched.Add(new Watched(target, property.GetConcatenatedSubNames(), mode));
        }

        Sample();
    }

    /// <summary>
    /// How many packets the watched properties have caused since the meter was made.
    /// </summary>
    /// <remarks>
    /// One per observed change of an <c>OnChange</c> property. A class whose properties are all
    /// <c>Never</c> can never move this off zero, and a railed parcel must not move it off zero
    /// after the one send that puts it on the belt.
    /// </remarks>
    public int Changes { get; private set; }

    /// <summary>
    /// How many properties stream on the interval whatever happens, regardless of change.
    /// </summary>
    /// <remarks>
    /// Counted separately because it is a different cost with a different cause: an
    /// <c>Always</c> property bills every interval forever, so one of them is the difference
    /// between "no ongoing traffic" and arch §3.4's unviable 200 KB/s.
    /// </remarks>
    public int Streaming { get; private set; }

    /// <summary>Bytes the changes above have cost, by Godot's own encoding of the values.</summary>
    public int Bytes { get; private set; }

    /// <summary>
    /// Takes a reading. Call once per physics frame; the constructor takes the first one.
    /// </summary>
    public void Sample()
    {
        Streaming = 0;

        foreach (var watched in _watched)
        {
            if (watched.Mode == SceneReplicationConfig.ReplicationMode.Always)
            {
                Streaming++;
            }

            var value = watched.Node.GetIndexed(watched.Property);
            if (watched.Seen && value.Equals(watched.Last))
            {
                continue;
            }

            if (watched.Seen)
            {
                Changes++;
                Bytes += GD.VarToBytes(value).Length;
            }

            watched.Last = value;
            watched.Seen = true;
        }
    }

    /// <summary>What the meter is watching, for a failure message that names it.</summary>
    public override string ToString() =>
        $"{_synchronizer.Name}: {_watched.Count} watched, {Streaming} streaming, "
        + $"{Changes} changes, {Bytes} bytes";

    private sealed class Watched(Node node, NodePath property, SceneReplicationConfig.ReplicationMode mode)
    {
        public Node Node { get; } = node;

        public NodePath Property { get; } = property;

        public SceneReplicationConfig.ReplicationMode Mode { get; } = mode;

        public Variant Last { get; set; }

        public bool Seen { get; set; }
    }
}
