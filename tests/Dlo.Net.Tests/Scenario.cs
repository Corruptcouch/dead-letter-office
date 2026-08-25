namespace Dlo.Net.Tests;

/// <summary>
/// Which of the three L3 scenarios a peer was launched to play. Named on the command line by
/// the harness, echoed back in the peer's report, and compile-time shared by both halves.
/// </summary>
/// <remarks>
/// One value selects one scripted ending, and every peer in a run gets the same one. The scenarios
/// differ only in what happens <i>after</i> convergence, so a failure before it is E0-09's and a
/// failure after it is E0-10's.
/// <para>
/// <b>Strings rather than an enum</b> because they cross a process boundary as a command-line
/// switch and come back in a text report. An enum would be converted twice each way to buy nothing.
/// </para>
/// </remarks>
public static class Scenario
{
    /// <summary>The command-line switch that selects a scenario.</summary>
    public const string Argument = "--dlo-scenario=";

    /// <summary>
    /// E0-09. Four peers connect, converge on one replicated value, report it back, and end.
    /// The default, so a peer launched without a scenario plays the original one.
    /// </summary>
    public const string Converge = "converge";

    /// <summary>
    /// E0-10, first criterion. One client leaves mid-session and the rest must keep working —
    /// proved by a <i>second</i> value the host publishes only once the leaver is gone.
    /// </summary>
    public const string Departure = "departure";

    /// <summary>
    /// E0-10, second criterion. The host tears the session down and every client must end its
    /// own session cleanly rather than hang until its deadline.
    /// </summary>
    public const string HostLoss = "hostloss";

    /// <summary>
    /// The client that leaves in <see cref="Departure"/>. Named here rather than on either
    /// side so the peer deciding to leave and the test asserting who left cannot disagree.
    /// </summary>
    public const string Leaver = "client3";
}
