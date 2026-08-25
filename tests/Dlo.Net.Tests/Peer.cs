using System;
using System.Globalization;
using System.Linq;

using Dlo.Game.Net;
using Godot;

namespace Dlo.Net.Tests;

/// <summary>
/// One L3 peer: one Godot process, one role, one scripted scenario, one line of output and one
/// exit code. The harness in <see cref="FourPeerSession"/> launches four of these.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four processes, not four <c>SceneTree</c>s</b> — E0-08's finding, and the reason this
/// file is a whole peer rather than a quarter of one. Both shapes work and both are fast;
/// what decides it is that a Godot process has exactly one physics world, so four in-process
/// peers would put four copies of every parcel into it, colliding with each other. Measured
/// 2026-08-24: two bodies in two sibling subtrees of one process shoved each other apart, so
/// the shared world is a fact rather than a worry. E1-06 (two clients grabbing one parcel)
/// and E2-09 (belt → grab → throw → tube) are the tests that would otherwise have been lies.
/// The full finding, including what in-process would have been cheaper at, is on
/// <see cref="FourPeerSession"/>.
/// </para>
/// <para>
/// <b>The peers run this project, not <c>src/Dlo.Game</c>.</b> That keeps harness-only code —
/// <see cref="Beacon"/>, this scenario — out of the shipping build, and it is the same trade
/// tests/Dlo.Game.Tests already made at L2. The ceiling is the same too, and worth stating:
/// this builds <see cref="SessionRoot"/> as an ordinary node, so it does not cover
/// SessionRoot's registration as an autoload (arch §6.2) or <c>Main.tscn</c>. What it does
/// cover is every line inside SessionRoot, EnetTransport and HostSession, across four real
/// processes and a real socket.
/// </para>
/// </remarks>
public partial class Peer : Node
{
    private const string RoleArgument = "--dlo-role=";
    private const string HostRole = "host";
    private const string ClientRolePrefix = "client";
    private const string Address = "127.0.0.1";

    /// <summary>Clients the host expects. Four players, so the host plus three (vision §4).</summary>
    private const int ClientCount = 3;

    /// <summary>
    /// How long a peer waits before declaring the run failed. Generous, because the cost of a
    /// too-tight deadline is a flaky suite and a flaky suite is not a suite (E0-09). A green
    /// run takes about two seconds, so this is ten times the observed cost.
    /// </summary>
    private const double DeadlineSeconds = 20.0;

    /// <summary>How long a client stays up after reporting, so its reliable RPC leaves.</summary>
    /// <remarks>
    /// ponytail: a fixed hold rather than an acknowledgement from the host.
    /// Ceiling: it assumes half a second is enough to flush one reliable packet over
    /// loopback, which it is by three orders of magnitude, and would not be over a real
    /// network. Upgrade: the host acks, or the client waits for the host's disconnect.
    /// Both cost a second protocol message to remove a constant that is not currently wrong.
    /// </remarks>
    private const double FlushSeconds = 0.5;

    /// <summary>How long a client waits before re-attempting a connection.</summary>
    private const double RetrySeconds = 0.25;

    private string _role = HostRole;
    private SessionRoot _session = null!;
    private Beacon _beacon = null!;
    private double _elapsed;
    private double _sentAt = double.NaN;
    private double _lastAttempt;
    private int _attempts;
    private int _id;
    private bool _finished;

    private bool IsHost => _role == HostRole;

    private bool HasReported => !double.IsNaN(_sentAt);

    private int Crew => _session?.Session?.ConnectedPeers.Count ?? 0;

    /// <summary>This peer's connection state, with Godot's stand-in read as what it means.</summary>
    /// <remarks>
    /// <c>Multiplayer.MultiplayerPeer</c> is never null — Godot substitutes an
    /// <see cref="OfflineMultiplayerPeer"/>, and that one reports <c>Connected</c>. Taking it
    /// at face value would make a torn-down client look connected and stop it retrying.
    /// </remarks>
    private MultiplayerPeer.ConnectionStatus Status => Multiplayer.MultiplayerPeer switch
    {
        null or OfflineMultiplayerPeer => MultiplayerPeer.ConnectionStatus.Disconnected,
        var peer => peer.GetConnectionStatus(),
    };

    /// <inheritdoc/>
    public override void _Ready()
    {
        _role = RoleFromCommandLine();

        // Headless Godot runs its main loop as fast as the machine allows. Four of those is
        // four cores spinning to make a two-second test two seconds long, and on a shared CI
        // runner it is four cores taken from whatever else is on the box.
        Engine.MaxFps = 60;

        _beacon = new Beacon { Name = "Beacon" };
        AddChild(_beacon);

        _session = new SessionRoot { Name = "SessionRoot", Transport = new EnetTransport() };
        AddChild(_session);

        if (IsHost)
        {
            _session.Host(ClientCount);
        }
        else if (_role.StartsWith(ClientRolePrefix, StringComparison.Ordinal))
        {
            Attempt();
        }
        else
        {
            // The warm-up pass. Godot creates .godot/ on a project's first run, and four
            // processes racing to create it is a flake waiting for a cold clone. This role
            // touches no socket.
            Finish(PeerReport.Idle, 0);
        }
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        if (_finished)
        {
            return;
        }

        _elapsed += delta;

        // Captured while connected rather than read at exit. A client that finishes because
        // the host went away first - which is the normal ending, and was the observed one on
        // the very first green run - has already lost its id by then, and reports 0.
        if (Status == MultiplayerPeer.ConnectionStatus.Connected)
        {
            _id = Multiplayer.GetUniqueId();
        }

        if (IsHost)
        {
            TickHost();
        }
        else
        {
            TickClient();
        }

        if (!_finished && _elapsed > DeadlineSeconds)
        {
            Finish(PeerReport.Timeout, 2);
        }
    }

    private void TickHost()
    {
        // Published only once the crew is complete, so a client cannot pass by connecting
        // late and reading a value that was already sitting there when it arrived.
        if (Crew == ClientCount + 1 && _beacon.Beat != Beacon.Sentinel)
        {
            _beacon.Beat = Beacon.Sentinel;
        }

        if (_beacon.Reports.Count == ClientCount)
        {
            Finish(PeerReport.Ok, 0);
        }
    }

    private void TickClient()
    {
        var connected = Status == MultiplayerPeer.ConnectionStatus.Connected;

        if (HasReported)
        {
            // Either the flush window elapsed, or the host collected its three reports and
            // went away first. Both mean this client is done.
            if (!connected || _elapsed - _sentAt > FlushSeconds)
            {
                Finish(PeerReport.Ok, 0);
            }

            return;
        }

        if (connected)
        {
            // Converged, so report. The RPC arriving at the host is what proves the
            // replication went through — the host never has to trust a client's word for it.
            if (_beacon.Beat == Beacon.Sentinel)
            {
                _sentAt = _elapsed;
                _beacon.RpcId(1, Beacon.MethodName.ReportBeat, _beacon.Beat);
            }

            return;
        }

        // Four processes start at once and the host is not always first to bind. Retrying is
        // what removes that race; the alternative is sequencing on the host's stdout, which is
        // block-buffered through a pipe and would trade a real race for a worse one.
        if (_elapsed - _lastAttempt > RetrySeconds)
        {
            Attempt();
        }
    }

    private void Attempt()
    {
        _attempts++;
        _lastAttempt = _elapsed;

        try
        {
            _session.Leave();
            _session.Join(Address);
        }
        catch (InvalidOperationException e)
        {
            // EnetTransport throws rather than returning a half-made peer (Fail.IfNotOk). At
            // this level that is not fatal: the host may simply not be listening yet, and the
            // next tick tries again. The deadline is what ends it if it never does.
            GD.Print($"{_role}: attempt {_attempts} failed: {e.Message}");
        }
    }

    private void Finish(string status, int exitCode)
    {
        _finished = true;
        GD.Print(Report(status));
        GetTree().Quit(exitCode);
    }

    private string Report(string status) => string.Join(
        ' ',
        PeerReport.Prefix,
        $"{PeerReport.Role}={_role}",
        $"{PeerReport.Status}={status}",
        $"{PeerReport.Id}={_id}",
        $"{PeerReport.Crew}={Crew}",
        $"{PeerReport.Beat}={_beacon?.Beat ?? 0}",
        $"{PeerReport.Intents}={_beacon?.Reports.Count ?? 0}",
        $"{PeerReport.Heard}={Heard()}",
        $"{PeerReport.Attempts}={_attempts}",
        $"{PeerReport.Elapsed}={_elapsed.ToString("F2", CultureInfo.InvariantCulture)}");

    private string Heard() =>
        _beacon is null || _beacon.Reports.Count == 0
            ? PeerReport.None
            : string.Join(',', _beacon.Reports.Select(report => $"{report.Key}:{report.Value}"));

    private static string RoleFromCommandLine()
    {
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith(RoleArgument, StringComparison.Ordinal))
            {
                return argument[RoleArgument.Length..];
            }
        }

        return HostRole;
    }
}
