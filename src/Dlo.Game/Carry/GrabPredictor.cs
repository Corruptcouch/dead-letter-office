using System.Linq;

using Godot;

namespace Dlo.Game.Carry;

/// <summary>
/// The client half of a grab: hands and a visual attachment on the frame the button goes down,
/// before the host has agreed to anything (E1-05).
/// </summary>
/// <remarks>
/// <b>This is the only optimistic path in the build, and it is justified by one specific
/// property</b> (arch §3.3). At 80 ms RTT a grab that waits for confirmation feels broken, and
/// vision §3.1 is emphatic that the parcel must be the problem rather than the input. When the
/// prediction is wrong the rollback does not read as jank — it reads as a teammate yanking the box
/// away half a second before you got it, which is vision §3.5's comedy engine firing for free.
/// <para>
/// <b>Do not generalise it, and do not let it touch physics.</b> Stamping, opening and incinerating
/// are decisions the report has already recorded, so rolling one back would un-decide something a
/// player has been blamed for — they wait for the host, and E2-07 and E3-03 say so in their own
/// criteria. Nothing here creates a joint either: the real one lives only on the host, so a
/// mispredicted grab cannot leave a client holding a constraint the host never heard of.
/// </para>
/// </remarks>
public partial class GrabPredictor : Node
{
    /// <summary>
    /// Physics frames a refused parcel takes to leave the hands (E1-06).
    /// </summary>
    /// <remarks>
    /// A quarter of a second. Long enough that the parcel is visibly <i>taken</i> rather than
    /// deleted from your hands, short enough that nobody waits for it. Arch §3.3 is explicit that
    /// the mispredicted case has to read as a teammate yanking the box away — and that only works
    /// if it looks like that, which means it has to be seen moving.
    /// </remarks>
    public const int SlipFrames = 15;

    private GrabDirector _director = null!;
    private CarryArms? _arms;
    private PlayerCharacter _carrier = null!;
    private Carryable? _slipping;
    private Vector3 _slipFrom;
    private int _slipFrame;

    /// <summary>The load this client is pretending to hold, or <c>null</c>.</summary>
    public Carryable? Predicted { get; private set; }

    /// <summary>Whether the host has confirmed <see cref="Predicted"/>.</summary>
    public bool Confirmed { get; private set; }

    /// <summary>Why the last prediction was thrown away, for the suite to assert against.</summary>
    public GrabVerdict? LastRefusal { get; private set; }

    /// <summary>Wires this predictor to its carrier and the session's director.</summary>
    public void Bind(GrabDirector director, PlayerCharacter carrier, CarryArms? arms)
    {
        System.ArgumentNullException.ThrowIfNull(director);
        System.ArgumentNullException.ThrowIfNull(carrier);

        _director = director;
        _carrier = carrier;
        _arms = arms;

        _director.Denied += OnDenied;
        _director.HoldsChanged += OnHoldsChanged;
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        if (_director is not null)
        {
            _director.Denied -= OnDenied;
            _director.HoldsChanged -= OnHoldsChanged;
        }
    }

    /// <summary>
    /// Press grab. The hands move and the load attaches <b>on this frame</b>, at any latency,
    /// and the host is asked in the same breath.
    /// </summary>
    public void Press(Carryable load)
    {
        System.ArgumentNullException.ThrowIfNull(load);

        Predicted = load;
        Confirmed = false;
        LastRefusal = null;

        // Asked FIRST, and that ordering is load bearing in the other direction from how it looks.
        // Grab does not block - on a client it posts an RPC and returns - so nothing here waits for
        // an answer and the attach still lands in this call, on this frame. But predicting first
        // moves the load, and on a listen-host the host validates against the very position the
        // prediction just invented: a grab from across the room passed its own range check,
        // measured 2026-08-25. Ask, then predict whatever is left undecided.
        _director.Grab(load);

        // Both no-ops if the host already answered synchronously, which is what happens on a host.
        Hold();
        _arms?.Reach(Predicted, 0);
    }

    /// <summary>Let go. Nothing here can refuse it (E1-07): a player who cannot drop is fighting the input.</summary>
    public void Release()
    {
        var load = Predicted;
        Clear();

        if (load is not null)
        {
            _director.Release(load);
        }
    }

    /// <summary>Throw what is held, along <paramref name="aim"/>.</summary>
    public void Throw(Vector3 aim)
    {
        var load = Predicted;
        Clear();

        if (load is not null)
        {
            _director.Throw(load, aim);
        }
    }

    /// <summary>
    /// Drags the predicted load to the hands. Visual only: it sets a transform and never a joint.
    /// </summary>
    /// <remarks>
    /// <b>The load is frozen while predicted, and it has to be.</b> The physics server owns an
    /// active <see cref="RigidBody3D"/>'s transform, so assigning a position to a simulating body
    /// is overwritten the same step — measured, and it is why this reads as the box ignoring your
    /// hands. Freezing is legitimate here precisely because a client's copy is not authoritative:
    /// the host's joint is the truth, and this is a picture of it arriving early.
    /// </remarks>
    public override void _PhysicsProcess(double delta)
    {
        Slip();
        Hold();
    }

    /// <summary>
    /// Puts the predicted parcel's <b>picture</b> in the hands. Called from <see cref="Press"/> so
    /// it lands on the press frame, and every frame after to keep it there.
    /// </summary>
    /// <remarks>
    /// <b>It moves <see cref="Carryable.Visual"/> and never the body.</b> That is the whole of
    /// "visual-only" (arch §3.3), and it is not a nicety: writing the body's transform means
    /// writing the property replication owns, and the parcel then flips between the predicted hand
    /// and the authority's position on every packet — measured at about a metre, several times a
    /// second, across the L3 harness. Offsetting a child cannot contend with anything, needs no
    /// freeze, and leaves the body exactly where the host put it.
    /// </remarks>
    private void Hold()
    {
        // Once the host confirms, its joint moves the body and the picture belongs back on it.
        if (Predicted is null || Confirmed)
        {
            return;
        }

        Predicted.Visual.GlobalPosition = _carrier.Anchor.GlobalPosition;
    }

    /// <summary>
    /// Walks a refused parcel's picture back onto its body over <see cref="SlipFrames"/> frames,
    /// instead of snapping it back in one (E1-06: it does not teleport).
    /// </summary>
    /// <remarks>
    /// Arch §3.3 wants a mispredicted grab to read as a teammate yanking the box away, and that
    /// only works if the box is seen to go. Dropping the offset in a single frame is a teleport of
    /// however far apart the two players were standing.
    /// </remarks>
    private void Slip()
    {
        if (_slipping is null)
        {
            return;
        }

        _slipFrame++;

        if (_slipFrame >= SlipFrames)
        {
            // Home. The picture is back on the body and the body was never disturbed.
            _slipping.Visual.Position = Vector3.Zero;
            _slipping = null;
            return;
        }

        _slipping.Visual.Position = _slipFrom.Lerp(
            Vector3.Zero, (float)_slipFrame / SlipFrames);
    }

    private void OnDenied(string loadPath, GrabVerdict verdict)
    {
        if (Predicted is null || Predicted.GetPath().ToString() != loadPath)
        {
            return;
        }

        LastRefusal = verdict;

        // The parcel snaps out of the hands and that is the end of it: no error state, no stuck
        // animation, and above all no re-request loop (E1-05). Asking again on refusal is how a
        // denied grab becomes a client hammering the host for the rest of the shift.
        Clear();
    }

    private void OnHoldsChanged()
    {
        if (Predicted is null)
        {
            return;
        }

        var me = _director.Multiplayer.GetUniqueId();
        var mine = _director.HoldersOf(Predicted.GetPath().ToString());

        if (mine.Contains(me))
        {
            // Confirmed. The host's joint now moves the body to the same hands the picture was
            // already in, so the offset is retired - easing it would be easing toward where the
            // picture already is.
            Confirmed = true;
            Predicted.Visual.Position = Vector3.Zero;
            _slipping = null;
            return;
        }

        // Somebody else has it, or nobody does. Either way this prediction is dead, and it does not
        // wait for GrabRefused to say so.
        //
        // That matters more than it looks. A loser that keeps pinning the parcel to its own hand is
        // also being written to by replication, so the parcel flips between the predicted hand and
        // the authority's position on every packet - about a metre, several times a second. It reads
        // as the box vibrating between two players. Measured across the L3 harness, 2026-08-25.
        // GrabResolved naming someone else is the earliest honest moment to let go, so this uses it.
        if (Confirmed || mine.Count > 0)
        {
            Clear();
        }
    }

    private void Clear()
    {
        if (Predicted is not null && !Confirmed)
        {
            // Handed back over the next few frames rather than dropped on this one.
            // Local offset, so easing it to zero is easing the picture back onto the body -
            // wherever the body has got to by then, and with nothing to track.
            _slipping = Predicted;
            _slipFrom = Predicted.Visual.Position;
            _slipFrame = 0;
        }

        Predicted = null;
        Confirmed = false;
        _arms?.Reach(null, 0);
    }
}
