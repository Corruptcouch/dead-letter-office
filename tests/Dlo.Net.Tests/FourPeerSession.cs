using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// One four-peer run at a time, for the whole assembly. EnetTransport.Port is a fixed 27377
// (E0-02: a host and three clients on one machine all want the same number), so two runs cannot
// coexist - and xUnit parallelises across test classes by default. Measured 2026-08-25, before
// this line existed: three concurrent runs produced clients that connected to another scenario's
// host and converged on its value, then every peer timed out at 20 s. It reads as a network fault
// and it is a scheduling one, which is why it is worth the sentence.
//
// The alternative - a port per run - would mean making a shipping constant configurable for
// the benefit of the test suite. Serialising three runs that cost about a second each is the
// cheaper side of that trade by a wide margin.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Dlo.Net.Tests;

/// <summary>
/// Boots a headless host and three headless clients over <c>EnetTransport</c>, waits for all
/// four to finish, and holds what each one said. One run, shared by every test in the class,
/// because the run is the expensive part and the assertions are not.
/// </summary>
/// <remarks>
/// <b>E0-08's finding — four processes, not four <c>SceneTree</c>s.</b> Both shapes were built and
/// measured (arch §11 carries the numbers); cost did not decide it, because both are far inside
/// arch §10.1's budget. What decides it is that <b>one process has exactly one physics world</b>:
/// two rigid bodies in sibling subtrees shoved each other apart within sixty frames, so four
/// in-process peers would hold four copies of every parcel in one simulation. Arch §10.4's
/// physics-bearing assertions — grab contention (E1-06), identity through tube transit (E2-09),
/// the ledger agreeing across peers — would all have been quietly wrong. Statics, autoloads and
/// <c>ProjectSettings</c> are shared for the same reason, and host authority is a claim about
/// separate machines.
/// <para>
/// In-process remains legitimate for a test that is purely about RPC routing, with no physics and
/// no global state. It is not the shape for this suite.
/// </para>
/// </remarks>
public abstract class FourPeerSession : IDisposable
{
    /// <summary>The host peer's role name.</summary>
    public const string HostRole = "host";

    /// <summary>
    /// The three client peers' role names. The last is <see cref="Scenario.Leaver"/>, taken
    /// from there rather than spelled again so the peer that decides to leave and the test
    /// that asserts who left cannot drift apart.
    /// </summary>
    public static readonly string[] ClientRoles = ["client1", "client2", Scenario.Leaver];

    /// <summary>
    /// How long the harness waits for a peer to exit on its own. Longer than the peers' own
    /// 20 s deadline, so a peer that is working normally always gets to report its position
    /// before the harness kills it — a killed peer says nothing, and E0-09 is explicit that
    /// an L3 failure has to name what each peer held.
    /// </summary>
    private static readonly TimeSpan _exitTimeout = TimeSpan.FromSeconds(45);

    private readonly List<Process> _processes = [];
    private readonly Dictionary<Process, List<string>> _output = [];

    /// <summary>Boots the four peers on one scenario and waits for them.</summary>
    /// <param name="scenario">Which ending every peer in this run plays. See <see cref="Scenario"/>.</param>
    protected FourPeerSession(string scenario)
    {
        ScenarioName = scenario;

        var project = LocateProject(out var problem);
        if (project is null)
        {
            SetupFailure = problem;
            Peers = [];
            return;
        }

        var godot = LocateGodot(out problem);
        if (godot is null)
        {
            SetupFailure = problem;
            Peers = [];
            return;
        }

        // Godot writes .godot/ on a project's first run. Four processes racing to create it
        // is a flake that only ever fires on a cold clone, which is the worst place to meet
        // one. This pass takes no socket and exits immediately (Peer's idle role).
        Launch(godot, project, "warmup", scenario).WaitForExit((int)_exitTimeout.TotalMilliseconds);

        var started = Stopwatch.StartNew();
        var peers = new List<(string Role, Process Process)>
        {
            (HostRole, Launch(godot, project, HostRole, scenario)),
        };
        peers.AddRange(ClientRoles.Select(role => (role, Launch(godot, project, role, scenario))));

        var deadline = DateTime.UtcNow + _exitTimeout;
        var outcomes = new List<PeerOutcome>();
        foreach (var (role, process) in peers)
        {
            var remaining = deadline - DateTime.UtcNow;
            var exited = process.WaitForExit((int)Math.Max(remaining.TotalMilliseconds, 0));

            // The argument-less overload is what waits for the redirected output readers to
            // drain. Without it a peer's report line can still be in flight when it is read,
            // which presents as an intermittently missing report - the exact flake this suite
            // cannot afford.
            if (exited)
            {
                process.WaitForExit();
            }

            outcomes.Add(new PeerOutcome(role, exited, exited ? process.ExitCode : int.MinValue,
                Captured(process)));
        }

        started.Stop();
        Duration = started.Elapsed;
        Peers = outcomes;
    }

    /// <summary>
    /// Which ending this run played. Named <c>ScenarioName</c> rather than <c>Scenario</c> so
    /// that <see cref="Scenario"/>'s constants stay reachable by their own name in here.
    /// </summary>
    public string ScenarioName { get; } = string.Empty;

    /// <summary>What stopped the run before it started, or <c>null</c> if it ran.</summary>
    /// <remarks>
    /// A missing Godot is reported as a failure rather than a skip, deliberately. An L3 suite
    /// that quietly skips itself is worse than no L3 suite: it reports green.
    /// </remarks>
    public string? SetupFailure { get; }

    /// <summary>Wall-clock cost of the four-peer run itself, excluding the warm-up pass.</summary>
    public TimeSpan Duration { get; }

    /// <summary>Every peer's outcome, host first.</summary>
    public IReadOnlyList<PeerOutcome> Peers { get; }

    /// <summary>The host's outcome, or <c>null</c> if the run never started.</summary>
    public PeerOutcome? Host => Peers.FirstOrDefault(peer => peer.Role == HostRole);

    /// <summary>The three clients' outcomes.</summary>
    public IEnumerable<PeerOutcome> Clients => Peers.Where(peer => peer.Role != HostRole);

    /// <summary>
    /// Every peer's position, for use as an assertion message. This is the difference between
    /// an L3 failure that costs a minute and one that costs an hour (E0-09).
    /// </summary>
    public string Transcript
    {
        get
        {
            if (SetupFailure is not null)
            {
                return SetupFailure;
            }

            var text = new StringBuilder(
                $"four-peer `{ScenarioName}` run took {Duration.TotalSeconds:F2}s");
            foreach (var peer in Peers)
            {
                text.AppendLine().Append(peer.Describe());
            }

            return text.ToString();
        }
    }

    /// <summary>Kills anything still running and releases the process handles.</summary>
    /// <remarks>
    /// The peers end themselves in a green run, so this is the failure path: a hung peer holds
    /// port 27377 and poisons the next run, which presents as flakiness rather than as the
    /// hang it is (E0-09).
    /// </remarks>
    public void Dispose()
    {
        foreach (var process in _processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
                // The process ended between HasExited and Kill. That is the outcome wanted.
            }
            finally
            {
                process.Dispose();
            }
        }

        _processes.Clear();
    }

    private Process Launch(string godot, string project, string role, string scenario)
    {
        var start = new ProcessStartInfo(godot)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--headless");
        start.ArgumentList.Add("--path");
        start.ArgumentList.Add(project);

        // Everything after `--` reaches the peer as OS.GetCmdlineUserArgs().
        start.ArgumentList.Add("--");
        start.ArgumentList.Add($"--dlo-role={role}");
        start.ArgumentList.Add($"{Scenario.Argument}{scenario}");

        var process = new Process { StartInfo = start };
        var captured = new List<string>();
        process.OutputDataReceived += (_, line) => Capture(captured, line.Data);
        process.ErrorDataReceived += (_, line) => Capture(captured, line.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _processes.Add(process);
        _output[process] = captured;
        return process;
    }

    private IReadOnlyList<string> Captured(Process process)
    {
        var lines = _output[process];
        lock (lines)
        {
            return lines.ToArray();
        }
    }

    private static void Capture(List<string> lines, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (lines)
        {
            lines.Add(line);
        }
    }

    private static string? LocateProject(out string problem)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "dlo.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            problem = $"No dlo.sln above {AppContext.BaseDirectory}; cannot find the peer project.";
            return null;
        }

        var project = Path.Combine(directory.FullName, "tests", "Dlo.Net.Tests");

        // Godot loads a project's C# from .godot/mono/temp/bin/Debug whether or not the run
        // that built it was a Debug one. Checking here turns "every peer timed out" - which
        // reads as a network fault and is the wrong thing to debug - into one sentence naming
        // the actual cause.
        var assembly = Path.Combine(project, ".godot", "mono", "temp", "bin", "Debug",
            "Dlo.Net.Tests.dll");
        if (!File.Exists(assembly))
        {
            problem = $"The peers load {assembly}, and it is not there. Godot reads the Debug "
                + "output path regardless of the configuration this suite was built in, so run "
                + "`dotnet build tests/Dlo.Net.Tests` (Debug) before the L3 suite.";
            return null;
        }

        problem = string.Empty;
        return project;
    }

    private static string? LocateGodot(out string problem)
    {
        var pinned = Environment.GetEnvironmentVariable("GODOT_BIN");
        if (!string.IsNullOrWhiteSpace(pinned) && File.Exists(pinned))
        {
            problem = string.Empty;
            return pinned;
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in new[] { "godot", "godot.exe" })
            {
                var candidate = Path.Combine(directory.Trim(), name);
                if (File.Exists(candidate))
                {
                    problem = string.Empty;
                    return candidate;
                }
            }
        }

        problem = pinned is null
            ? "GODOT_BIN is not set and no `godot` is on PATH. The L3 suite launches the "
                + "pinned editor (4.7.2-stable-mono) as four headless processes; point "
                + "GODOT_BIN at that executable. The README records this machine's path."
            : $"GODOT_BIN is set to '{pinned}', and there is no file there.";
        return null;
    }
}

/// <summary>E0-09's run: four peers converge on one value and end. Nothing goes wrong.</summary>
public sealed class ConvergeRun() : FourPeerSession(Scenario.Converge);

/// <summary>
/// E0-10's first run: one client leaves mid-session and the other three keep working.
/// </summary>
public sealed class DepartureRun() : FourPeerSession(Scenario.Departure);

/// <summary>
/// E0-10's second run: the host tears the session down and every client ends its own cleanly.
/// </summary>
public sealed class HostLossRun() : FourPeerSession(Scenario.HostLoss);
