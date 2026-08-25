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
/// <b>Four processes, not four <c>SceneTree</c>s</b> — E0-08's finding: a Godot process has exactly
/// one physics world, so four in-process peers would put four copies of every parcel into it. The
/// measurements are in arch §11 and the reasoning is on <see cref="FourPeerSession"/>.
/// <para>
/// <b>Every run is identical up to convergence; only the ending differs</b> (E0-10). All three
/// <see cref="Scenario"/>s connect four peers, publish one value and collect three reports; what
/// follows is the scenario — nothing, a client leaving, or the host tearing down. That is what the
/// state machine below buys: a failure before convergence is E0-09's, after it E0-10's.
/// </para>
/// <para>
/// <b>The peers run this project, not <c>src/Dlo.Game</c></b>, which keeps harness-only code out of
/// the shipping build. The ceiling: <see cref="SessionRoot"/> is built as an ordinary node, so
/// its autoload registration (arch §6.2) and <c>Main.tscn</c> are not covered — everything inside
/// SessionRoot, EnetTransport and HostSession is, across four real processes and a real socket.
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
    private string _scenario = Scenario.Converge;
    private SessionRoot _session = null!;
    private Beacon _beacon = null!;
    private double _elapsed;
    private double _sentAt = double.NaN;
    private double _echoedAt = double.NaN;
    private double _lastAttempt;
    private int _attempts;
    private int _id;
    private string _teardown = PeerReport.Live;
    private bool _finished;

    private bool IsHost => _role == HostRole;

    /// <summary>Whether this peer is the one that walks out in <see cref="Scenario.Departure"/>.</summary>
    private bool IsLeaver => _role == Scenario.Leaver;

    private bool HasReported => !double.IsNaN(_sentAt);

    private bool HasEchoed => !double.IsNaN(_echoedAt);

    private int Crew => _session?.Session?.ConnectedPeers.Count ?? 0;

    /// <summary>Reports naming <see cref="Beacon.Aftermath"/> — the survivors, host-side.</summary>
    private int Echoes => _beacon.Reports.Count(report => report.Value == Beacon.Aftermath);

    /// <summary>This peer's connection state, with Godot's stand-in read as what it means.</summary>
    /// <remarks>
    /// <c>Multiplayer.MultiplayerPeer</c> is never null — Godot substitutes an
    /// <see cref="OfflineMultiplayerPeer"/>, and that one reports <c>Connected</c>. Taking it
    /// at face value would make a torn-down client look connected and stop it retrying, and in
    /// <see cref="Scenario.HostLoss"/> it would hide the very host loss under test.
    /// </remarks>
    private MultiplayerPeer.ConnectionStatus Status => Multiplayer.MultiplayerPeer switch
    {
        null or OfflineMultiplayerPeer => MultiplayerPeer.ConnectionStatus.Disconnected,
        var peer => peer.GetConnectionStatus(),
    };

    /// <inheritdoc/>
    public override void _Ready()
    {
        _role = Argument(RoleArgument, HostRole);
        _scenario = Argument(Scenario.Argument, Scenario.Converge);

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
        // late and reading a value that was already sitting there when it arrived. Guarded on
        // the default rather than on the sentinel, because in `departure` the value moves on
        // to Aftermath and a `!= Sentinel` guard would then publish the sentinel a second time.
        if (Crew == ClientCount + 1 && _beacon.Beat == default)
        {
            _beacon.Beat = Beacon.Sentinel;
        }

        if (_beacon.Reports.Count < ClientCount)
        {
            return;
        }

        // Every scenario arrives here having done exactly what E0-09 asserts. What follows is
        // the ending under test.
        switch (_scenario)
        {
            case Scenario.Departure:
                // The second value goes out only once the leaver is actually gone. Publishing
                // it any earlier would let a survivor converge on it BEFORE the departure,
                // which proves nothing about surviving one - and an assertion that passes for
                // the wrong reason is the one thing this suite cannot afford.
                if (Crew == ClientCount)
                {
                    _beacon.Beat = Beacon.Aftermath;
                }

                if (Echoes == ClientCount - 1)
                {
                    Finish(PeerReport.Ok, 0);
                }

                break;

            case Scenario.HostLoss:
                EndSession(PeerReport.TornDown);
                break;

            default:
                Finish(PeerReport.Ok, 0);
                break;
        }
    }

    private void TickClient()
    {
        if (Status != MultiplayerPeer.ConnectionStatus.Connected)
        {
            if (HasReported)
            {
                // The host is gone. In `hostloss` that is the scenario under test; in the
                // others it only means the host collected what it needed and finished first.
                // Either way this peer's session has to end cleanly, so both take one path.
                EndSession(_scenario == Scenario.HostLoss ? PeerReport.HostLost : PeerReport.Ok);
            }
            else if (_elapsed - _lastAttempt > RetrySeconds)
            {
                // Four processes start at once and the host is not always first to bind.
                // Retrying is what removes that race; the alternative is sequencing on the
                // host's stdout, which is block-buffered through a pipe and would trade a real
                // race for a worse one.
                Attempt();
            }

            return;
        }

        if (!HasReported)
        {
            // Converged, so report. The RPC arriving at the host is what proves the
            // replication went through — the host never has to trust a client's word for it.
            if (_beacon.Beat == Beacon.Sentinel)
            {
                Send(out _sentAt);
            }

            return;
        }

        switch (_scenario)
        {
            case Scenario.Departure when IsLeaver:
                // The departure itself, held until the flush window so the host is certain to
                // hold this peer's report before it loses the peer that sent it. A leaver
                // whose report never arrived would make the host's `heard` ambiguous.
                if (_elapsed - _sentAt > FlushSeconds)
                {
                    EndSession(PeerReport.Left);
                }

                break;

            case Scenario.Departure:
                // A survivor. It waits for the host's second value, echoes it, and only then
                // goes - so its report is evidence that it kept working after the departure
                // rather than merely through the moment of it.
                if (!HasEchoed)
                {
                    if (_beacon.Beat == Beacon.Aftermath)
                    {
                        Send(out _echoedAt);
                    }
                }
                else if (_elapsed - _echoedAt > FlushSeconds)
                {
                    Finish(PeerReport.Ok, 0);
                }

                break;

            case Scenario.HostLoss:
                // Nothing to do but stay up. This scenario ends in the disconnected branch
                // above, and a client that ends any other way was never told the host went
                // away - which is exactly the failure the scenario is looking for.
                break;

            default:
                if (_elapsed - _sentAt > FlushSeconds)
                {
                    Finish(PeerReport.Ok, 0);
                }

                break;
        }
    }

    /// <summary>Sends what this peer is holding to the host, and stamps when it did.</summary>
    private void Send(out double stamp)
    {
        stamp = _elapsed;
        _beacon.RpcId(1, Beacon.MethodName.ReportBeat, _beacon.Beat);
    }

    /// <summary>
    /// Ends this peer's session and records whether the teardown left anything behind (E0-10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SessionRoot.Leave</c> is contracted to be safe when there is no session and safe
    /// twice. This is that contract checked from the outside, on peers that really did lose a
    /// host or really did tear down — the only place where it means anything. The result
    /// travels in the report rather than being asserted here, because a peer that throws
    /// inside <c>_Process</c> tells the harness far less than one that exits normally saying
    /// <c>teardown=dirty</c>.
    /// </para>
    /// <para>
    /// <b>The story stops here.</b> Re-forming a session afterwards is not E0-10's to do: the
    /// player-facing message and the return to the lobby are E12-05, which is also explicit
    /// that there is no host migration. A peer that rebuilt a session here would be testing
    /// that decision rather than this one.
    /// </para>
    /// </remarks>
    private void EndSession(string status)
    {
        _session.Leave();

        _teardown = !_session.IsInSession && _session.Session is null
            ? PeerReport.Clean
            : PeerReport.Dirty;

        Finish(status, 0);
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
        $"{PeerReport.Scenario}={_scenario}",
        $"{PeerReport.Status}={status}",
        $"{PeerReport.Id}={_id}",
        $"{PeerReport.Crew}={Crew}",
        $"{PeerReport.Beat}={_beacon?.Beat ?? 0}",
        $"{PeerReport.Intents}={_beacon?.Reports.Count ?? 0}",
        $"{PeerReport.Heard}={Heard()}",
        $"{PeerReport.Teardown}={_teardown}",
        $"{PeerReport.Attempts}={_attempts}",
        $"{PeerReport.Elapsed}={_elapsed.ToString("F2", CultureInfo.InvariantCulture)}");

    private string Heard() =>
        _beacon is null || _beacon.Reports.Count == 0
            ? PeerReport.None
            : string.Join(',', _beacon.Reports.Select(report => $"{report.Key}:{report.Value}"));

    /// <summary>Reads one <c>--switch=value</c> off the command line, or its default.</summary>
    private static string Argument(string prefix, string fallback)
    {
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                return argument[prefix.Length..];
            }
        }

        return fallback;
    }
}
