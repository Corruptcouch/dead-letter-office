using Godot;

namespace Dlo.Game.Carry;

/// <summary>
/// Two arms that reach for whatever their carrier is holding (E1-03), on Godot 4.7's own
/// <see cref="TwoBoneIK3D"/>.
/// </summary>
/// <remarks>
/// <b>Native IK, not procedural arm code</b> (`AGENTS.md` rung 3, standards §10). 4.7 ships a
/// whole modifier family — <see cref="TwoBoneIK3D"/>, <c>FABRIK3D</c>, <c>CCDIK3D</c>,
/// <c>JacobianIK3D</c> — and a shoulder/elbow/hand chain is literally two bones, so the two-bone
/// solver is the one that fits. Writing an analytic elbow solve here would be re-deriving it.
/// <para>
/// <b>Hand pose is derived, never replicated</b> (E1-03). Every peer runs this from the same two
/// inputs: the holder map <c>GrabDirector</c> broadcast as a decision, and the load's own
/// transform. Nothing about a hand goes on the wire — hands move every frame, so replicating them
/// is the easiest accidental bandwidth leak in the build.
/// </para>
/// </remarks>
public partial class CarryArms : Node3D
{
    /// <summary>Setting index of the left arm on the shared modifier.</summary>
    public const int Left = 0;

    /// <summary>Setting index of the right arm.</summary>
    public const int Right = 1;

    private Skeleton3D _skeleton = null!;
    private TwoBoneIK3D _ik = null!;
    private Node3D _leftTarget = null!;
    private Node3D _rightTarget = null!;

    /// <summary>Shoulder half-width in metres: how far apart the two arms are rooted.</summary>
    [Export]
    public float ShoulderWidth { get; set; } = 0.2f;

    /// <summary>Length of each of the two bones in an arm, in metres.</summary>
    [Export]
    public float BoneLength { get; set; } = 0.3f;

    /// <summary>Where the hands rest when nothing is held, relative to this node.</summary>
    [Export]
    public Vector3 RestPose { get; set; } = new(0, -0.2f, -0.25f);

    /// <summary>The solver, exposed so a test can assert it is Godot's and not ours.</summary>
    public TwoBoneIK3D Solver => _ik;

    /// <summary>Where the left hand is currently being asked to reach, in global space.</summary>
    public Vector3 LeftTarget => _leftTarget.GlobalPosition;

    /// <summary>Where the right hand is currently being asked to reach.</summary>
    public Vector3 RightTarget => _rightTarget.GlobalPosition;

    /// <summary>The left hand bone's solved global position.</summary>
    public Vector3 LeftHand => HandAt("Hand.L");

    /// <summary>The right hand bone's solved global position.</summary>
    public Vector3 RightHand => HandAt("Hand.R");

    /// <inheritdoc/>
    public override void _Ready()
    {
        _skeleton = new Skeleton3D { Name = "Arms" };
        AddChild(_skeleton);

        Arm("L", -ShoulderWidth);
        Arm("R", ShoulderWidth);

        _leftTarget = new Node3D { Name = "Target.L" };
        _rightTarget = new Node3D { Name = "Target.R" };
        AddChild(_leftTarget);
        AddChild(_rightTarget);

        // A SkeletonModifier3D only runs as a child of the skeleton it modifies.
        _ik = new TwoBoneIK3D { Name = "IK", SettingCount = 2 };
        _skeleton.AddChild(_ik);

        Aim(Left, "L", _leftTarget);
        Aim(Right, "R", _rightTarget);

        Reach(null, 0);
    }

    /// <summary>
    /// Points both hands at <paramref name="load"/>'s grip for <paramref name="slot"/>, or back at
    /// the rest pose when there is nothing to hold.
    /// </summary>
    /// <remarks>
    /// The two hands straddle the grip point, so they read as a pair of hands on a box rather than
    /// two hands inside each other. Called from the holder map changing, not per frame.
    /// </remarks>
    public void Reach(Carryable? load, int slot)
    {
        if (load is null)
        {
            _leftTarget.Position = RestPose + new Vector3(-ShoulderWidth, 0, 0);
            _rightTarget.Position = RestPose + new Vector3(ShoulderWidth, 0, 0);
            return;
        }

        var grip = load.GlobalGrip(slot);
        var across = load.GlobalTransform.Basis.X.Normalized() * ShoulderWidth;

        _leftTarget.GlobalPosition = grip - across;
        _rightTarget.GlobalPosition = grip + across;
    }

    private void Arm(string side, float offsetX)
    {
        // Shoulder -> Elbow -> Hand. Two bones between three joints, which is exactly what
        // TwoBoneIK3D solves.
        var shoulder = _skeleton.AddBone($"Shoulder.{side}");
        _skeleton.SetBoneRest(shoulder, new Transform3D(Basis.Identity, new Vector3(offsetX, 0, 0)));

        var elbow = _skeleton.AddBone($"Elbow.{side}");
        _skeleton.SetBoneParent(elbow, shoulder);
        _skeleton.SetBoneRest(elbow, new Transform3D(Basis.Identity, new Vector3(0, -BoneLength, 0)));

        var hand = _skeleton.AddBone($"Hand.{side}");
        _skeleton.SetBoneParent(hand, elbow);
        _skeleton.SetBoneRest(hand, new Transform3D(Basis.Identity, new Vector3(0, -BoneLength, 0)));

        _skeleton.ResetBonePoses();
    }

    private void Aim(int setting, string side, Node3D target)
    {
        _ik.SetRootBoneName(setting, $"Shoulder.{side}");
        _ik.SetMiddleBoneName(setting, $"Elbow.{side}");
        _ik.SetEndBoneName(setting, $"Hand.{side}");
        _ik.SetTargetNode(setting, _ik.GetPathTo(target));
    }

    private Vector3 HandAt(string bone)
    {
        var index = _skeleton.FindBone(bone);
        return index < 0
            ? GlobalPosition
            : _skeleton.ToGlobal(_skeleton.GetBoneGlobalPose(index).Origin);
    }
}
