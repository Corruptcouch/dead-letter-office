using System;
using System.Collections.Generic;

using Xunit;

namespace Dlo.Domain.Tests;

/// <summary>
/// E0-04. The whole point of <see cref="HostSession"/> taking its systems as constructor
/// arguments is that these tests exist: a live session, asserted against, with no engine, no
/// transport and no peer anywhere near it.
/// </summary>
public class HostSessionTests
{
    /// <summary>
    /// A substitute <see cref="IRandom"/>. Every draw is the low end of its range, so a test
    /// that depends on a draw asserts a branch rather than an RNG (standards §8).
    /// </summary>
    private sealed class StubRandom : IRandom
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;

        public float NextFloat() => 0f;

        public T Pick<T>(IReadOnlyList<T> items) => items[0];

        public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weight) => items[0];
    }

    private static HostSession NewSession(out ShiftDirector director, out ShiftLedger ledger)
    {
        director = new ShiftDirector();
        ledger = new ShiftLedger();
        return new HostSession(director, ledger, new StubRandom(), new ParcelRegistry());
    }

    [Fact]
    public void A_session_holds_the_systems_it_was_given()
    {
        var session = NewSession(out var director, out var ledger);

        // Reference equality is the assertion, not a convenience: if HostSession ever builds
        // its own, this passes nothing along and the report would be written from a ledger
        // nobody else can reach (arch §3.2).
        Assert.Same(director, session.Director);
        Assert.Same(ledger, session.Ledger);
    }

    [Fact]
    public void A_joining_peer_is_recorded()
    {
        var session = NewSession(out _, out _);

        Assert.True(session.PeerJoined(new PeerId(2)));
        Assert.Contains(new PeerId(2), session.ConnectedPeers);
    }

    [Fact]
    public void The_same_peer_joining_twice_is_still_one_peer()
    {
        var session = NewSession(out _, out _);
        session.PeerJoined(new PeerId(2));

        // A transport is allowed to say the same thing twice; a crew of four that reports five
        // is worse than a dropped message, because nothing looks wrong.
        Assert.False(session.PeerJoined(new PeerId(2)));
        Assert.Single(session.ConnectedPeers);
    }

    [Fact]
    public void A_leaving_peer_is_forgotten()
    {
        var session = NewSession(out _, out _);
        session.PeerJoined(new PeerId(2));

        Assert.True(session.PeerLeft(new PeerId(2)));
        Assert.Empty(session.ConnectedPeers);
    }

    [Fact]
    public void A_peer_that_never_joined_can_still_leave()
    {
        var session = NewSession(out _, out _);

        // Disconnects arrive for peers the host never finished accepting. Teardown that throws
        // on one turns a dropped client into a broken shift for everyone else (E0-10).
        Assert.False(session.PeerLeft(new PeerId(9)));
    }

    [Fact]
    public void A_session_refuses_to_be_built_without_its_systems()
    {
        // Nullable is enabled, so this is a claim about callers who ignore it - the Game layer
        // compiles with warnings, not errors, so the guard is real (standards §1, §9).
        Assert.Throws<ArgumentNullException>(
            () => new HostSession(null!, new ShiftLedger(), new StubRandom(), new ParcelRegistry()));

        Assert.Throws<ArgumentNullException>(
            () => new HostSession(new ShiftDirector(), new ShiftLedger(), new StubRandom(), null!));
    }

    [Fact]
    public void The_shift_clock_accumulates()
    {
        var session = NewSession(out var director, out _);

        director.Advance(1.5f);
        director.Advance(2.0f);

        Assert.Equal(3.5f, session.Director.ElapsedSeconds);
    }

    [Fact]
    public void The_shift_clock_does_not_run_backwards()
    {
        var session = NewSession(out var director, out _);
        director.Advance(5f);

        Assert.Throws<ArgumentOutOfRangeException>(() => director.Advance(-1f));
        Assert.Equal(5f, session.Director.ElapsedSeconds);
    }

    [Fact]
    public void The_shift_clock_refuses_a_delta_that_is_not_a_finite_number()
    {
        var session = NewSession(out var director, out _);
        director.Advance(5f);

        // The check above does not catch these: `NaN < 0` is false. One of them ends the shift
        // rather than skewing it, because every later reading of the clock is NaN too.
        Assert.Throws<ArgumentOutOfRangeException>(() => director.Advance(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => director.Advance(float.PositiveInfinity));

        Assert.Equal(5f, session.Director.ElapsedSeconds);
    }
}
