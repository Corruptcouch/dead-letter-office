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

    /// <summary>The head, which pitches. The body yaws.</summary>
    public Node3D Head { get; private set; } = null!;

    /// <summary>Whether this body is crouched right now.</summary>
    public bool IsCrouched { get; private set; }

    /// <summary>Head pitch in radians, clamped just short of straight up and straight down.</summary>
    public float Pitch => Head.Rotation.X;

    /// <summary>Body yaw in radians.</summary>
    public float Yaw => Rotation.Y;

    /// <inheritdoc/>
    public override void _Ready()
    {
        Head = GetNodeOrNull<Node3D>("Head") ?? Attach();
        Head.Position = new Vector3(0, StandHeight, 0);
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

        Step(delta, Read());
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

        velocity.X = direction.X * Speed;
        velocity.Z = direction.Z * Speed;

        Velocity = velocity;
        MoveAndSlide();

        Crouch(intent.Crouch);
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
