using System;
using System.Globalization;
using System.Linq;

using Dlo.Game;
using Dlo.Game.Carry;
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

    /// <summary>
    /// Frames ignored after a grab is granted, before the parcel is watched for teleports (E1-06).
    /// </summary>
    /// <remarks>
    /// Long enough to cover the grab snap and the replication tick that carries it. A client only
    /// hears about the parcel every <c>ReplicationInterval</c>, so its very first post-grant sample
    /// legitimately covers three frames of motion at once.
    /// </remarks>
    private const int GraceFrames = 8;

    /// <summary>How far from the parcel each carrier stands. Inside <c>GrabRules.Reach</c>.</summary>
    private const float CarrierRadius = 1.0f;

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

    // E1-06 only.
    private Carryable? _parcel;
    private PlayerCharacter? _carrier;
    private GrabPredictor? _predictor;
    private Vector3 _lastParcelAt = Vector3.Zero;
    private double? _resolvedAt;
    private int _framesSinceResolved;
    private float _biggestJump;
    private double _grabbedAt = double.NaN;
    private double _answeredAt = double.NaN;
    private bool _won;

    private bool IsHost => _role == HostRole;

    /// <summary>Whether this peer is the one that walks out in <see cref="Scenario.Departure"/>.</summary>
    private bool IsLeaver => _role == Scenario.Leaver;

    private bool HasReported => !double.IsNaN(_sentAt);

    private bool HasEchoed => !double.IsNaN(_echoedAt);

    private int Crew => _session?.Session?.ConnectedPeers.Count ?? 0;

    private bool HasGrabbed => !double.IsNaN(_grabbedAt);

    private bool HasAnswered => !double.IsNaN(_answeredAt);

    /// <summary>Clients that have said whether they won the contest (E1-06).</summary>
    private int Answers => _beacon.Reports.Count(report =>
        report.Value is Beacon.Contest or Beacon.Lost);

    /// <summary>The grab authority, which <see cref="SessionRoot"/> builds on every peer.</summary>
    private GrabDirector Grabs => _session.Grabs;

    private string ParcelPath => _parcel?.GetPath().ToString() ?? string.Empty;

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

        if (_scenario == Scenario.Contention)
        {
            // Built on every peer at the same path, exactly like the Beacon, because that is what
            // lets an RPC name it. E2-01 replaces the path with a ParcelId, and E2-04 replaces this
            // hand-built node with a spawn.
            BuildParcel();
        }

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

            // A peer does not know its own id until it has connected, and the carrier built in
            // _Ready was placed from the pre-connection one. Re-placed and re-registered once, so
            // this peer stands where the HOST also believes it stands.
            if (_carrier is not null && _carrier.Name != $"Carrier{_id}")
            {
                _carrier.Name = $"Carrier{_id}";
                _carrier.Position = CarrierSpot(_id);
                Grabs.RegisterCarrier(_id, _carrier);
            }
        }

        WatchParcel();

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

            case Scenario.Contention:
                // Every client is connected and has converged, so the field is level: the go
                // signal is the last thing any of them is waiting for.
                if (_beacon.Beat != Beacon.Contest)
                {
                    _beacon.Beat = Beacon.Contest;
                    _grabbedAt = _elapsed;
                    return;
                }

                // Waits for all three answers rather than for a clock. The first version of
                // this used a timer and the host outlived the clients by nothing at all: it quit
                // first, every client took the host-went-away path instead of its own, and the
                // outcome each of them had computed was never in the report. It read as "nobody
                // won" on a run where somebody plainly had.
                if (Answers == ClientCount)
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

            case Scenario.Contention:
                if (!HasGrabbed)
                {
                    // All three reach on the frame they see the signal. True same-frame
                    // simultaneity is not observable across processes - what is under test is that
                    // the HOST serialises whatever order they arrive in and grants exactly once.
                    if (_beacon.Beat == Beacon.Contest && _parcel is not null)
                    {
                        _grabbedAt = _elapsed;
                        _predictor!.Press(_parcel);
                    }
                }
                else if (!HasAnswered)
                {
                    // One flush window after asking, which is long enough on loopback for the
                    // host to have decided and for GrabResolved or GrabRefused to have landed.
                    if (_elapsed - _grabbedAt > FlushSeconds)
                    {
                        _won = Grabs.HoldersOf(ParcelPath).Contains(Multiplayer.GetUniqueId());
                        Answer(_won ? Beacon.Contest : Beacon.Lost);
                    }
                }
                else if (_elapsed - _answeredAt > FlushSeconds)
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

    /// <summary>Tells the host how the contest went for this peer, and stamps when it did.</summary>
    private void Answer(int outcome)
    {
        _answeredAt = _elapsed;
        _beacon.RpcId(1, Beacon.MethodName.ReportBeat, outcome);
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
        $"{PeerReport.Won}={(_won ? 1 : 0)}",
        $"{PeerReport.Joints}={Joints(this)}",
        $"{PeerReport.Holders}={Grabs.HoldersOf(ParcelPath).Count}",
        $"{PeerReport.Holder}={Holder()}",
        $"{PeerReport.Parcel}={Where(_parcel)}",
        $"{PeerReport.Jump}={_biggestJump.ToString("F3", CultureInfo.InvariantCulture)}",
        $"{PeerReport.Elapsed}={_elapsed.ToString("F2", CultureInfo.InvariantCulture)}");

    private string Heard() =>
        _beacon is null || _beacon.Reports.Count == 0
            ? PeerReport.None
            : string.Join(',', _beacon.Reports.Select(report => $"{report.Key}:{report.Value}"));

    /// <summary>
    /// Builds the contested parcel and this peer's carrier, identically on every peer.
    /// </summary>
    /// <remarks>
    /// <b>The transform synchronizer is harness furniture</b>, like <see cref="Beacon"/> itself.
    /// Parcels get their real replication classes in E2-05; this one carries a plain host-owned
    /// transform so a losing client can be seen to converge on the host's truth rather than on a
    /// guess of its own. Without it, E1-06's "the loser sees it move toward the winner" would have
    /// nothing to observe, because nothing would be sending the winner's version.
    /// </remarks>
    private void BuildParcel()
    {
        // Without a floor the carrier, the parcel and the grip all free-fall together: the carry
        // still works (the parcel tracks 5 cm under the hand, exactly E1-01's sag) but every
        // position in the report is measured in a lift shaft. Measured 2026-08-25 - the first run
        // of this scenario reported the parcel at y = -5.4.
        var floor = new StaticBody3D { Name = "Floor", Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(60, 1, 60) },
        });
        AddChild(floor);

        _parcel = new Carryable
        {
            Name = "Parcel",
            Mass = 20.0f,
            Position = new Vector3(0, 1.0f, 0),
        };
        _parcel.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.6f, 0.6f, 0.6f) },
        });

        var transform = new NodePath(".:position");
        var config = new SceneReplicationConfig();
        config.AddProperty(transform);
        config.PropertySetReplicationMode(transform, SceneReplicationConfig.ReplicationMode.Always);

        _parcel.AddChild(new MultiplayerSynchronizer
        {
            Name = "Sync",
            ReplicationConfig = config,
            RootPath = "..",
            ReplicationInterval = 0.05,
        });

        AddChild(_parcel);
        _lastParcelAt = _parcel.Position;

        // A carrier each, standing in a different place, all of them inside reach.
        //
        // Different places matter: with everyone co-located the loser's own hand and the winner's
        // hand are the same point, and E1-06's "the loser sees it move toward the winner" asserts
        // nothing at all. Equidistant matters too - nobody may win on range, or the contest is
        // decided by geometry instead of by the host.
        //
        // The spot is derived from the peer id, so the host and the peer itself compute the same
        // one without any protocol to agree it. Nothing spawns characters per peer yet (no story
        // owns that), and this is the cheapest thing that is not a lie.
        _carrier = Carrier(Multiplayer.GetUniqueId());
        Multiplayer.PeerConnected += id => Grabs.RegisterCarrier(id, Carrier(id));

        // Clients reach through the predictor rather than calling the director, because that is
        // what a real client does - and it is the only way the rollback under test is the real one.
        _predictor = new GrabPredictor { Name = "Predictor" };
        AddChild(_predictor);
        _predictor.Bind(Grabs, _carrier, arms: null);
    }

    /// <summary>Builds, places and registers one peer's carrier, and returns it.</summary>
    private PlayerCharacter Carrier(long peerId)
    {
        var carrier = new PlayerCharacter
        {
            Name = $"Carrier{peerId}",
            Position = CarrierSpot(peerId),
        };
        carrier.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Height = 1.8f, Radius = 0.3f },
        });

        AddChild(carrier);
        Grabs.RegisterCarrier(peerId, carrier);
        return carrier;
    }

    /// <summary>Where the carrier for <paramref name="peerId"/> stands: around the parcel, in reach.</summary>
    private static Vector3 CarrierSpot(long peerId)
    {
        // Quartered around the parcel. Peer ids are large and arbitrary, so this only has to be
        // stable and spread - it does not have to be fair.
        var angle = Mathf.Pi * 0.5f * (peerId % 4);
        return new Vector3(
            Mathf.Cos(angle) * CarrierRadius,
            1.0f,
            Mathf.Sin(angle) * CarrierRadius);
    }

    /// <summary>Tracks the biggest single-frame move this peer saw the parcel make (E1-06).</summary>
    private void WatchParcel()
    {
        if (_parcel is null)
        {
            return;
        }

        var at = _parcel.Position;

        // Watched only once the contest is RESOLVED, and there is a real distinction here.
        //
        // E1-06 forbids the parcel teleporting on the loser when the rollback lands. It says
        // nothing about the winner's grab, and the grab does currently snap the parcel into the
        // hand - see GrabDirector.Crew, where the lift is explicit. Measuring from before the grant
        // would fold that snap into this number and the assertion would be about the wrong thing.
        if (_resolvedAt is null && Grabs.HoldersOf(ParcelPath).Count > 0)
        {
            _resolvedAt = _elapsed;
        }

        // Counted in FRAMES, not seconds. A wall-clock grace made this flaky: under the load of
        // four scenarios back to back a single frame can run long, the grab snap lands inside the
        // window, and the run fails on a 2 m "teleport" that is really the lift. Frames do not
        // stretch. Observed once in a full-suite run, 2026-08-25.
        if (_resolvedAt is not null && ++_framesSinceResolved > GraceFrames)
        {
            _biggestJump = Mathf.Max(_biggestJump, at.DistanceTo(_lastParcelAt));
        }

        _lastParcelAt = at;
    }

    private string Holder()
    {
        var holders = _parcel is null ? [] : Grabs.HoldersOf(ParcelPath);
        return holders.Count == 0 ? PeerReport.None : string.Join(',', holders);
    }

    private static int Joints(Node node)
    {
        var found = node is Joint3D ? 1 : 0;
        foreach (var child in node.GetChildren())
        {
            found += Joints(child);
        }

        return found;
    }

    private static string Where(Node3D? node) => node is null
        ? PeerReport.None
        : string.Join(
            '|',
            node.Position.X.ToString("F3", CultureInfo.InvariantCulture),
            node.Position.Y.ToString("F3", CultureInfo.InvariantCulture),
            node.Position.Z.ToString("F3", CultureInfo.InvariantCulture));

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
