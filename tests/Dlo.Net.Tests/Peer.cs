using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Dlo.Game;
using Dlo.Game.Carry;
using Dlo.Game.Facility;
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

    /// <summary>E2-05's belt: short, so every peer's parcel reaches the end and waits there.</summary>
    /// <remarks>
    /// <b>The end of the belt is what makes the assertion tight.</b> Four peers extrapolating the
    /// same spline from the same tuple still start their clocks a hop apart, so mid-ride they are
    /// a few centimetres out by construction. Parked against the end they are not: the belt has a
    /// last metre and everybody's arithmetic stops at it.
    /// </remarks>
    private const float BeltLength = 2.0f;

    /// <summary>Metres per second the belt runs at, in <see cref="Scenario.Railed"/>.</summary>
    private const float BeltSpeed = 1.2f;

    /// <summary>How high the belt sits, so knocking a parcel off it is a fall (E2-05).</summary>
    private const float BeltHeight = 1.5f;

    /// <summary>Seconds the parcel rides before it is knocked off. Twice the belt's own length.</summary>
    private const double RideSeconds = 3.0;

    /// <summary>Arch §8's ceiling on awake parcel bodies, which is what E2-10 measures at.</summary>
    private const int AwakeBodies = 40;

    /// <summary>Parcels on E2-10's belt: two lanes packed back from the end at <c>Spacing</c>.</summary>
    private const int RailedBodies = 34;

    /// <summary>E2-10's belt, long enough that <see cref="RailedBodies"/> fills both its lanes.</summary>
    private const float YardBeltLength = 12.0f;

    /// <summary>Seconds each of E2-10's four readings is taken over.</summary>
    /// <remarks>
    /// Long enough that one late packet cannot move the answer, short enough that four of them
    /// plus the settle fit inside the peers' 20 s deadline with room to spare.
    /// </remarks>
    private const double WindowSeconds = 1.5;

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

    // E2-10 only.
    private readonly List<Carryable> _yard = [];
    private int _stage;
    private double _windowFrom = double.NaN;
    private double _idleRate = -1;
    private double _entryRate = -1;
    private double _railedRate = -1;
    private double _awakeRate = -1;

    // E2-05 only.
    private Conveyor? _belt;
    private ReplicationMeter? _rideMeter;
    private Vector3 _railAt;
    private float _railDistance = -1.0f;
    private double _rideFrom = double.NaN;
    private int _rideChanges = -1;
    private int _rideBytes = -1;
    private int _fallReliable = -1;
    private int _fallStream = -1;
    private double _looseAt = double.NaN;
    private double _quietAt = double.NaN;

    // E1-06 only.
    private Carryable? _parcel;
    private PlayerCharacter? _carrier;
    private GrabPredictor? _predictor;
    private Vector3 _lastParcelAt = Vector3.Zero;
    private double? _resolvedAt;
    private int _framesSinceResolved;

    /// <summary>Who this peer saw holding the parcel when the contest resolved.</summary>
    /// <remarks>
    /// Captured rather than read at exit, for the same reason as the peer id: a peer that has
    /// left has no holder map left to read, and the question is who held it while there was a
    /// session to hold it in.
    /// </remarks>
    private string _resolvedHolders = PeerReport.None;

    private int _resolvedHolderCount;
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

    /// <summary>How many clients have echoed <paramref name="value"/> back, host-side.</summary>
    private int Echoes(int value) => _beacon.Reports.Count(report => report.Value == value);

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
            BuildCarriers();
        }

        if (_scenario == Scenario.Budget)
        {
            BuildYard();
        }

        if (_scenario == Scenario.Railed)
        {
            // LatencyPeer at zero delay, purely so a client can say how its packets arrived
            // (E0-07 built the decorator; this is the first run that puts it on a real socket).
            ProjectSettings.SetSetting(LatencyPeer.EnabledSetting, true);
            ProjectSettings.SetSetting(LatencyPeer.DelaySetting, 0);
            ProjectSettings.SetSetting(LatencyPeer.JitterSetting, 0);

            BuildParcel();
            BuildBelt();
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
        WatchBelt();

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

                if (Echoes(Beacon.Aftermath) == ClientCount - 1)
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

            case Scenario.Railed:
                TickRailedHost();
                break;

            case Scenario.Budget:
                TickBudgetHost();
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

            case Scenario.Railed:
                TickRailedClient();
                break;

            case Scenario.Budget:
                // Passive. A client's only job here is to be a real destination for the host's
                // upstream, which is the number under measurement.
                if (_beacon.Beat == Beacon.Weighed && !HasAnswered)
                {
                    Answer(Beacon.Weighed);
                }
                else if (HasAnswered && _elapsed - _answeredAt > FlushSeconds)
                {
                    Finish(PeerReport.Ok, 0);
                }

                break;

            default:
                if (_elapsed - _sentAt > FlushSeconds)
                {
                    Finish(PeerReport.Ok, 0);
                }

                break;
        }
    }

    /// <summary>
    /// The host's side of <see cref="Scenario.Railed"/>: ride, knock off, settle, go quiet.
    /// </summary>
    private void TickRailedHost()
    {
        switch (_beacon.Beat)
        {
            case Beacon.Sentinel:
                // On the belt. From here the host says nothing further about where the parcel is;
                // every peer computes it from the tuple this one line writes (arch §3.4).
                _belt!.Accept(_parcel!, lane: 0, distance: 0.0f);
                _rideMeter = new ReplicationMeter(_parcel!.Synchronizer);
                _rideFrom = _elapsed;
                _beacon.Beat = Beacon.Riding;
                break;

            case Beacon.Riding when _elapsed - _rideFrom > RideSeconds:
                // What the ride cost, read off the instrument before anything else happens to it.
                _rideChanges = _rideMeter!.Changes;
                _rideBytes = _rideMeter.Bytes;

                _belt!.Release(_parcel!);
                _beacon.Beat = Beacon.Loose;
                break;

            case Beacon.Loose when _parcel!.Class == ReplicationClass.Sleeping:
                _beacon.Beat = Beacon.Asleep;
                break;

            case Beacon.Asleep when Echoes(Beacon.Asleep) == ClientCount:
                Finish(PeerReport.Ok, 0);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// A client's side of <see cref="Scenario.Railed"/>: watch, and report what it was told.
    /// </summary>
    /// <remarks>
    /// It is deliberately passive. Everything it knows about the parcel arrived as replication,
    /// so its final position is evidence about what the host sent rather than about what it did.
    /// </remarks>
    private void TickRailedClient()
    {
        if (_beacon.Beat == Beacon.Loose && double.IsNaN(_looseAt))
        {
            _looseAt = _elapsed;
        }

        // The fall window opens a flush AFTER the parcel came off the belt, not at the moment it
        // did. Clearing the rail tuple is a watched change and travels reliably, and it shares a
        // poll with the phase value that announces it - so a window starting on the announcement
        // would count that one reliable packet about half the time, which is a flaky test rather
        // than a finding.
        if (!double.IsNaN(_looseAt) && _fallReliable < 0 && _elapsed - _looseAt > FlushSeconds
            && Wire is { } opening)
        {
            _fallReliable = opening.Taken(MultiplayerPeer.TransferModeEnum.Reliable);
            _fallStream = opening.Taken(MultiplayerPeer.TransferModeEnum.Unreliable);
        }

        if (_beacon.Beat != Beacon.Asleep)
        {
            return;
        }

        if (double.IsNaN(_quietAt))
        {
            _quietAt = _elapsed;
            return;
        }

        if (!HasAnswered)
        {
            // One flush window after the host went quiet, so the final transform it owes has had
            // time to land before this peer states where it thinks the parcel is.
            if (_elapsed - _quietAt > FlushSeconds)
            {
                if (Wire is { } closing && _fallReliable >= 0)
                {
                    _fallReliable = closing.Taken(MultiplayerPeer.TransferModeEnum.Reliable) - _fallReliable;
                    _fallStream = closing.Taken(MultiplayerPeer.TransferModeEnum.Unreliable) - _fallStream;
                }

                Answer(Beacon.Asleep);
            }
        }
        else if (_elapsed - _answeredAt > FlushSeconds)
        {
            Finish(PeerReport.Ok, 0);
        }
    }

    /// <summary>
    /// The host's side of <see cref="Scenario.Budget"/>: three readings, taken as load is added.
    /// </summary>
    /// <remarks>
    /// <b>Three, not one</b>, because E2-10 requires that an over-budget finding name which
    /// replication class is misbehaving. One number would say the facility is too expensive and
    /// nothing about which part of arch §3.4 is not paying for itself.
    /// </remarks>
    private void TickBudgetHost()
    {
        switch (_stage)
        {
            case 0 when _yard.TrueForAll(p => p.Class == ReplicationClass.Sleeping):
                // Everything on the floor and asleep. The baseline is the beacon and the
                // session's own keep-alive, and nothing else.
                Open();
                break;

            case 1 when Elapsed(WindowSeconds):
                _idleRate = Close();
                Load();
                Open();
                break;

            case 2 when Elapsed(WindowSeconds):
                // The entry burst, kept separate rather than folded in. Arch §3.4 prices a railed
                // parcel at "~6 bytes, once", and a single window starting at Accept would charge
                // that once to every second of the reading and make the belt look expensive.
                _entryRate = Close();
                Open();
                break;

            case 3 when Elapsed(WindowSeconds):
                _railedRate = Close();
                Wake();
                Open();
                break;

            case 4 when Elapsed(WindowSeconds):
                _awakeRate = Close();
                _beacon.Beat = Beacon.Weighed;
                _stage = 5;
                break;

            case 5 when Echoes(Beacon.Weighed) == ClientCount:
                Finish(PeerReport.Ok, 0);
                break;

            default:
                break;
        }
    }

    /// <summary>Zeroes the wire counter and starts the next reading.</summary>
    private void Open()
    {
        Sent();
        _windowFrom = _elapsed;
        _stage++;
    }

    /// <summary>Closes a reading and returns it in bytes per second.</summary>
    private double Close() => Sent() / Math.Max(_elapsed - _windowFrom, double.Epsilon);

    private bool Elapsed(double seconds) => _elapsed - _windowFrom > seconds;

    /// <summary>
    /// Bytes ENet has put on the wire since this was last called, and zero thereafter.
    /// </summary>
    /// <remarks>
    /// ENet's own counter rather than anything this harness adds up, because arch §8's budget is
    /// about what leaves the machine — UDP headers, acknowledgements and all — and a tally kept
    /// above the transport would report the payload and call it the cost.
    /// </remarks>
    private double Sent() => Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet
        ? enet.Host.PopStatistic(ENetConnection.HostStatistic.SentData)
        : 0.0;

    /// <summary>Fills both lanes of the belt, backed up from its end.</summary>
    private void Load()
    {
        for (var i = 0; i < RailedBodies; i++)
        {
            // Packed against the end rather than fed in at the start, so the belt is already
            // backed up when it is measured. That is the state vision §2 says a shift lives in.
            _belt!.Accept(_yard[i], lane: i % 2, distance: YardBeltLength - (i / 2 * _belt.Spacing));
        }
    }

    /// <summary>Wakes arch §8's forty, and keeps them awake for the length of the reading.</summary>
    private void Wake()
    {
        foreach (var parcel in _yard.Skip(RailedBodies))
        {
            // CanSleep off, because the budget is about bodies that are awake: a box that dozed
            // off halfway through the window would be measured in the cheap class it is not in.
            parcel.CanSleep = false;
            parcel.Sleeping = false;
        }
    }

    /// <summary>
    /// Builds E2-10's yard: one belt, and enough parcels to fill it and arch §8's awake budget.
    /// </summary>
    /// <remarks>
    /// Built identically on every peer, at identical paths, for the reason everything else here
    /// is: nothing spawns yet (E2-04 owns that), and a synchronizer needs the same node on both
    /// ends of the wire.
    /// </remarks>
    private void BuildYard()
    {
        var floor = new StaticBody3D { Name = "Floor", Position = new Vector3(0, -0.5f, 0) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(60, 1, 60) } });
        AddChild(floor);

        _belt = new Conveyor
        {
            Name = "Belt",
            BeltId = 5,
            Speed = BeltSpeed,
            Lanes = 2,
            Length = YardBeltLength,
            Position = new Vector3(-20, BeltHeight, 0),
        };
        AddChild(_belt);

        for (var i = 0; i < RailedBodies + AwakeBodies; i++)
        {
            var parcel = new Carryable
            {
                Name = $"Parcel{i}",
                Mass = 8.0f,

                // A grid on the floor, resting rather than dropped: the settle before the first
                // reading is dead time in a 20 s deadline, and a box placed at rest sleeps in the
                // half-second Jolt asks for instead of bouncing first.
                Position = new Vector3(((i % 10) - 5) * 1.0f, 0.201f, ((i / 10) - 4) * 1.0f),
            };
            parcel.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(0.4f, 0.4f, 0.4f) },
            });

            AddChild(parcel);
            _yard.Add(parcel);
        }
    }

    /// <summary>This peer's lag decorator, or <c>null</c> if it is not wrapped in one.</summary>
    private LatencyPeer? Wire => Multiplayer.MultiplayerPeer as LatencyPeer;

    /// <summary>Tracks where the parcel was the last time this peer saw it on a belt (E2-05).</summary>
    private void WatchBelt()
    {
        if (_belt?.DistanceOf(_parcel!) is not { } distance)
        {
            return;
        }

        _railAt = _parcel!.GlobalPosition;
        _railDistance = distance;
    }

    /// <summary>
    /// Builds E2-05's belt, identically on every peer and at the same path as every other peer's.
    /// </summary>
    private void BuildBelt()
    {
        _belt = new Conveyor
        {
            Name = "Belt",
            BeltId = 5,
            Speed = BeltSpeed,
            Lanes = 1,
            Length = BeltLength,
            Position = new Vector3(0, BeltHeight, 0),
        };

        AddChild(_belt);
        _parcel!.Position = new Vector3(0, BeltHeight, 0);
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
        $"{PeerReport.Holders}={_resolvedHolderCount}",
        $"{PeerReport.Holder}={Holder()}",
        $"{PeerReport.Parcel}={Where(_parcel)}",
        $"{PeerReport.Jump}={_biggestJump.ToString("F3", CultureInfo.InvariantCulture)}",
        $"{PeerReport.Rail}={Where3(_railAt)}",
        $"{PeerReport.RailDistance}={_railDistance.ToString("F3", CultureInfo.InvariantCulture)}",
        $"{PeerReport.Class}={_parcel?.Class.ToString() ?? PeerReport.None}",
        $"{PeerReport.RideChanges}={_rideChanges}",
        $"{PeerReport.RideBytes}={_rideBytes}",
        $"{PeerReport.FallReliable}={_fallReliable}",
        $"{PeerReport.FallStream}={_fallStream}",
        $"{PeerReport.IdleRate}={_idleRate.ToString("F0", CultureInfo.InvariantCulture)}",
        $"{PeerReport.EntryRate}={_entryRate.ToString("F0", CultureInfo.InvariantCulture)}",
        $"{PeerReport.RailedRate}={_railedRate.ToString("F0", CultureInfo.InvariantCulture)}",
        $"{PeerReport.AwakeRate}={_awakeRate.ToString("F0", CultureInfo.InvariantCulture)}",
        $"{PeerReport.RailedBodies}={(_belt?.Carrying ?? 0)}",
        $"{PeerReport.AwakeBodies}={_yard.Count(p => !p.CanSleep)}",
        $"{PeerReport.Elapsed}={_elapsed.ToString("F2", CultureInfo.InvariantCulture)}");

    private string Heard() =>
        _beacon is null || _beacon.Reports.Count == 0
            ? PeerReport.None
            : string.Join(',', _beacon.Reports.Select(report => $"{report.Key}:{report.Value}"));

    /// <summary>
    /// Builds the contested parcel and this peer's carrier, identically on every peer.
    /// </summary>
    /// <remarks>
    /// <b>The parcel brings its own replication now</b> (E2-05): <see cref="Carryable"/> builds its
    /// synchronizer, picks its class from its own state, and freezes itself on every peer that
    /// does not own it. This method builds a box on a floor and nothing else.
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

        AddChild(_parcel);
        _lastParcelAt = _parcel.Position;
    }

    /// <summary>
    /// Builds this peer's carrier and the predictor it reaches through. <see cref="Scenario.Contention"/>
    /// only — <see cref="Scenario.Railed"/> wants the box and the floor without four bodies
    /// standing where the belt is.
    /// </summary>
    private void BuildCarriers()
    {
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
        if (_resolvedAt is null && Grabs.HoldersOf(ParcelPath) is { Count: > 0 } holders)
        {
            _resolvedAt = _elapsed;
            _resolvedHolderCount = holders.Count;
            _resolvedHolders = string.Join(',', holders);
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

    private string Holder() => _resolvedHolders;

    private static int Joints(Node node)
    {
        var found = node is Joint3D ? 1 : 0;
        foreach (var child in node.GetChildren())
        {
            found += Joints(child);
        }

        return found;
    }

    private static string Where(Node3D? node) =>
        node is null ? PeerReport.None : Where3(node.Position);

    private static string Where3(Vector3 at) => string.Join(
        '|',
        at.X.ToString("F3", CultureInfo.InvariantCulture),
        at.Y.ToString("F3", CultureInfo.InvariantCulture),
        at.Z.ToString("F3", CultureInfo.InvariantCulture));

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
