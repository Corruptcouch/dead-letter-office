using Dlo.Game.Carry;

using Godot;

namespace Dlo.Game;

/// <summary>What a player is asking their body to do this frame. No engine input in it.</summary>
/// <param name="Move">
/// Desired ground movement, body-relative: <c>X</c> right, <c>Y</c> forward. Already normalised
/// by the caller if it needs to be.
/// </param>
/// <param name="Jump">Whether jump was pressed this frame.</param>
/// <param name="Crouch">Whether crouch is being held.</param>
/// <remarks>
/// Separating the intent from the reading of it is what makes E1-02 testable: a test drives the
/// body directly and asserts what one frame did, with no input device, window or peer.
/// </remarks>
public readonly record struct MoveIntent(Vector2 Move, bool Jump, bool Crouch);

/// <summary>
/// The first-person body: move, look, jump, crouch — local and immediate on every machine
/// (E1-02).
/// </summary>
/// <remarks>
/// <b>The owning peer owns this node</b> (stories gap 1, settled 2026-08-25). Input is immediate
/// because the body is <i>owned</i>, not predicted, which keeps arch §3.3's "grab is the only
/// optimistic path" literally true; the host still owns every fact about the shift, and a position
/// is not one. There is no host-side position validation — a cheating client is not in the threat
/// model for a friends-and-invites-only game (vision §16).
/// <para>
/// <b>No input damping anywhere</b> — not movement, not camera, not "only while carrying something
/// heavy". Arch §6.1 bans it and names it as what makes this game read as broken rather than funny:
/// weight is expressed through the object, never through the controller.
/// </para>
/// </remarks>
public partial class PlayerCharacter : CharacterBody3D
{
    /// <summary>Ground speed in m/s. Reached in one frame; there is no acceleration curve.</summary>
    [Export]
    public float Speed { get; set; } = 4.5f;

    /// <summary>Upward speed applied on jump, in m/s.</summary>
    [Export]
    public float JumpSpeed { get; set; } = 4.0f;

    /// <summary>Radians of rotation per unit of look input.</summary>
    [Export]
    public float LookSensitivity { get; set; } = 0.003f;

    /// <summary>Standing eye height, in metres.</summary>
    [Export]
    public float StandHeight { get; set; } = 1.7f;

    /// <summary>Crouched eye height, in metres.</summary>
    [Export]
    public float CrouchHeight { get; set; } = 1.0f;

    /// <summary>
    /// Carrier mass in kilograms. Only used to divide a held load's spring force, so a heavy
    /// parcel pulls a light carrier around more (E1-07).
    /// </summary>
    [Export]
    public float Mass { get; set; } = 80.0f;

    /// <summary>
    /// How much horizontal speed an impact must take before it staggers the body, in m/s
    /// (E1-09). Above a walk, so brushing a doorframe does not read as a trip.
    /// </summary>
    [Export]
    public float StumbleThreshold { get; set; } = 2.0f;

    /// <summary>
    /// What fraction of the world's shove survives each physics frame. At 0.85 a stagger is
    /// spent in about a quarter second, which is recovery the player does not wait for.
    /// </summary>
    [Export]
    public float PushDecay { get; set; } = 0.85f;

    /// <summary>What the carrier is holding, or <c>null</c>. Set by <c>GrabDirector</c>'s decision.</summary>
    public Carry.Carryable? Carried { get; set; }

    /// <summary>The head, which pitches. The body yaws.</summary>
    public Node3D Head { get; private set; } = null!;

    /// <summary>Whether this body is crouched right now.</summary>
    public bool IsCrouched { get; private set; }

    /// <summary>
    /// The kinematic body a held load is jointed to (E1-04). E1-01 measured the whole carry
    /// envelope against an <see cref="AnimatableBody3D"/> hand, so this is one.
    /// </summary>
    public AnimatableBody3D Anchor { get; private set; } = null!;

    /// <summary>
    /// Velocity the <i>world</i> is imposing: a stagger off a wall, a heavy load dragging
    /// behind. Decays on its own and is never subtracted from input (arch §6.1).
    /// </summary>
    public Vector3 Push { get; private set; }

    /// <summary>Head pitch in radians, clamped just short of straight up and straight down.</summary>
    public float Pitch => Head.Rotation.X;

    /// <summary>Body yaw in radians.</summary>
    public float Yaw => Rotation.Y;

    /// <summary>The child a held load is jointed to, by name, so `GrabDirector` can find it.</summary>
    public const string HandAnchorName = "HandAnchor";

    /// <inheritdoc/>
    public override void _Ready()
    {
        Head = GetNodeOrNull<Node3D>("Head") ?? Attach();
        Head.Position = new Vector3(0, StandHeight, 0);

        Anchor = GetNodeOrNull<AnimatableBody3D>(HandAnchorName) ?? AttachAnchor();
    }

    /// <inheritdoc/>
    public override void _PhysicsProcess(double delta)
    {
        // Only the owner drives the body. Everyone else is watching a replicated transform, and
        // running this on their copy would mean four machines fighting over one position.
        if (!IsMultiplayerAuthority())
        {
            return;
        }

        ApplyCarryPull(delta);
        Step(delta, Read());
    }

    /// <summary>
    /// Feeds one frame of <see cref="CarryPull"/> for whatever this carrier is holding into
    /// <see cref="Push"/>. Public so a test can drive it without an input device.
    /// </summary>
    public void ApplyCarryPull(double delta)
    {
        if (Carried is null)
        {
            return;
        }

        Stumble(CarryPull(Anchor.GlobalPosition, Carried.GlobalPosition, Carried.Mass, Mass)
            * (float)delta);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Mouse capture belongs to whatever put the player in the world, not to the body: a node
    /// that grabbed the cursor would take it back off every menu the session opens.
    /// </remarks>
    public override void _UnhandledInput(InputEvent @event)
    {
        // The same rule as the physics step: only the owner turns this body.
        if (@event is InputEventMouseMotion mouse && IsMultiplayerAuthority())
        {
            Look(mouse.Relative);
        }
    }

    /// <summary>
    /// Turns the body and the head. Applied whole, on the frame it arrives.
    /// </summary>
    /// <param name="delta">Raw look input — a mouse motion relative, typically.</param>
    /// <remarks>
    /// <b>No smoothing and no acceleration.</b> Two calls with the same delta turn exactly twice as
    /// far as one, and the test asserts it. Camera smoothing is the most commonly added form of the
    /// damping arch §6.1 bans, because it feels like polish while making the game unresponsive.
    /// </remarks>
    public void Look(Vector2 delta)
    {
        RotateY(-delta.X * LookSensitivity);

        // Clamped just inside a right angle. Exactly ±90° lets the forward vector degenerate,
        // and the symptom is a camera that flips when the player looks straight down.
        var pitch = Mathf.Clamp(
            Head.Rotation.X - (delta.Y * LookSensitivity),
            -Mathf.Pi / 2.0f + 0.01f,
            (Mathf.Pi / 2.0f) - 0.01f);

        Head.Rotation = new Vector3(pitch, 0, 0);
    }

    /// <summary>
    /// Advances the body one physics frame from <paramref name="intent"/>.
    /// </summary>
    /// <remarks>
    /// Public and engine-input-free so a test can drive a frame directly. Gravity comes from
    /// the project's own setting rather than a constant, so the body falls at the same rate as
    /// everything else in the facility.
    /// </remarks>
    public void Step(double delta, MoveIntent intent)
    {
        var velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity.Y -= (float)(Gravity * delta);
        }
        else if (intent.Jump)
        {
            velocity.Y = JumpSpeed;
        }
        else
        {
            // Not zero: a small downward bias is what keeps IsOnFloor() true walking down a
            // ramp, and without it the body ticks between grounded and airborne every frame.
            velocity.Y = -0.1f;
        }

        // Full speed on the frame the key goes down, and a dead stop on the frame it comes up.
        // An acceleration curve here would be the banned damping wearing a physics costume.
        var direction = (Transform.Basis * new Vector3(intent.Move.X, 0, -intent.Move.Y))
            .Slide(UpDirection)
            .Normalized();

        // Input is ADDED to what the world is doing, never scaled by it. A shove displaces the
        // body; it does not reduce the player's authority over it, which is the whole difference
        // between awkward and unresponsive (arch §6.1, vision §3.1).
        var wanted = direction * Speed;
        velocity.X = wanted.X + Push.X;
        velocity.Z = wanted.Z + Push.Z;

        Velocity = velocity;
        MoveAndSlide();

        Impact(new Vector2(velocity.X, velocity.Z), new Vector2(Velocity.X, Velocity.Z));

        // Spent here rather than on a timer, so nothing is holding a countdown against the player.
        Push *= PushDecay;

        Crouch(intent.Crouch);
    }

    /// <summary>
    /// The world shoving the body: a collision, an uneven floor, a load that would not come
    /// (E1-09). <b>Never a random timer and never an input lockout</b> — vision §3.1 calls that
    /// unresponsive input in a costume, and it is the failure this whole story guards against.
    /// </summary>
    public void Stumble(Vector3 shove) => Push += new Vector3(shove.X, 0, shove.Z);

    /// <summary>
    /// Whether losing <paramref name="speedLost"/> m/s counts as an impact worth staggering from.
    /// </summary>
    /// <remarks>
    /// <paramref name="pushInPlay"/> is what makes this a rule rather than a state machine: a
    /// stagger that is still being spent cannot generate another one, so the body cannot shove
    /// itself into a wall and stumble off its own stumble. No timer, no lockout, no flag.
    /// </remarks>
    public static bool IsImpact(float speedLost, float threshold, float pushInPlay) =>
        speedLost >= threshold && pushInPlay < threshold;

    /// <summary>
    /// The acceleration a held load imposes on its carrier, from the grip spring's own stretch.
    /// </summary>
    /// <remarks>
    /// <b>This is the object's mass acting through the joint, not an input modifier</b> (E1-07).
    /// It is zero while the load rests in the hands and grows as the load lags behind, so a heavy
    /// parcel drags on a sprint and costs nothing standing still. The vertical component is
    /// dropped deliberately: E1-01 measured ~5 cm of constant gravity sag, and charging the
    /// carrier for that would be a permanent downward tug that is not a pull at all.
    /// </remarks>
    public static Vector3 CarryPull(Vector3 gripTarget, Vector3 loadAt, float loadMass, float carrierMass)
    {
        var stretch = loadAt - gripTarget;
        stretch.Y = 0;

        return stretch * GripSpring.StiffnessFor(loadMass) / Mathf.Max(1.0f, carrierMass);
    }

    private void Impact(Vector2 intended, Vector2 achieved)
    {
        var lost = intended - achieved;
        if (!IsImpact(lost.Length(), StumbleThreshold, new Vector2(Push.X, Push.Z).Length()))
        {
            return;
        }

        // Staggered back the way the world pushed, which is opposite the speed it took.
        Stumble(new Vector3(-lost.X, 0, -lost.Y));
    }

    /// <summary>Gravity, from the project setting so the body matches every other body.</summary>
    private static float Gravity =>
        (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8).AsDouble();

    private void Crouch(bool crouching)
    {
        if (crouching == IsCrouched)
        {
            return;
        }

        IsCrouched = crouching;

        // The head moves in one step. A lerp here would be the camera damping arch §6.1 bans,
        // and crouch-spam is a movement verb rather than an animation to be smoothed over.
        Head.Position = new Vector3(0, crouching ? CrouchHeight : StandHeight, 0);
    }

    /// <summary>Reads the engine's input into an intent. The only place input is touched.</summary>
    private static MoveIntent Read() => new(
        Input.GetVector(Actions.Left, Actions.Right, Actions.Back, Actions.Forward),
        Input.IsActionJustPressed(Actions.Jump),
        Input.IsActionPressed(Actions.Crouch));

    private Node3D Attach()
    {
        var head = new Node3D { Name = "Head" };
        AddChild(head);
        return head;
    }

    /// <remarks>
    /// <b>In no collision layer, and that is deliberate.</b> It exists only to be jointed to, so
    /// giving it contacts would have it shouldering its own carrier and the load it is holding.
    /// Jolt still treats a layerless body as a body, which is all a joint needs.
    /// </remarks>
    private AnimatableBody3D AttachAnchor()
    {
        var anchor = new AnimatableBody3D
        {
            Name = HandAnchorName,
            SyncToPhysics = false,
            CollisionLayer = 0,
            CollisionMask = 0,
            Position = new Vector3(0, StandHeight - 0.35f, -0.55f),
        };

        AddChild(anchor);
        return anchor;
    }

    /// <summary>The input actions this controller reads.</summary>
    public static class Actions
    {
        /// <summary>Move forward.</summary>
        public const string Forward = "move_forward";

        /// <summary>Move back.</summary>
        public const string Back = "move_back";

        /// <summary>Strafe left.</summary>
        public const string Left = "move_left";

        /// <summary>Strafe right.</summary>
        public const string Right = "move_right";

        /// <summary>Jump.</summary>
        public const string Jump = "jump";

        /// <summary>Crouch, held.</summary>
        public const string Crouch = "crouch";
    }
}
