using Godot;

namespace Dlo.Game.Carry;

/// <summary>
/// Builds one carrier's grip: the sprung <see cref="Generic6DofJoint3D"/> that E1-01 measured
/// (arch §11). The only place the recipe is written down.
/// </summary>
/// <remarks>
/// <b>Not a <see cref="PinJoint3D"/>.</b> Jolt does not implement its <c>impulse_clamp</c> — it
/// logs that it is ignoring the value — and a rigid pin to a kinematic hand carries any mass
/// weightlessly, which is the opposite of vision §3.1. A linear spring is the only compliance
/// Jolt honours, so it is the only way weight reaches the player.
/// </remarks>
public static class GripSpring
{
    /// <summary>
    /// Spring stiffness per kilogram held. E1-01's envelope: below ~50 × mass the load
    /// oscillates and sags out of the carriers' hands, above ~1000 × mass it is rigid again.
    /// </summary>
    public const float StiffnessPerKilogram = 100.0f;

    /// <summary>
    /// Damping floor. Below this the softer half of the stable band jitters (E1-01).
    /// </summary>
    public const float Damping = 100.0f;

    /// <summary>Stiffness for a given held mass, at E1-01's reference ratio.</summary>
    public static float StiffnessFor(float mass) => mass * StiffnessPerKilogram;

    /// <summary>
    /// Joints <paramref name="load"/> to <paramref name="hand"/> at <paramref name="at"/>, in
    /// global space. Host only — a client never creates a physics joint (arch §3.3).
    /// </summary>
    /// <remarks>
    /// <b>The joint enters the tree before its ends are named, and that ordering is load
    /// bearing.</b> <c>NodeA</c> and <c>NodeB</c> are <see cref="NodePath"/>s resolved relative to
    /// the joint, so computing them while it is still detached yields paths that resolve to
    /// nothing. Jolt then holds neither body and the load simply falls — no error, no warning.
    /// </remarks>
    public static Generic6DofJoint3D Attach(Node parent, Node3D hand, RigidBody3D load, Vector3 at)
    {
        System.ArgumentNullException.ThrowIfNull(parent);
        System.ArgumentNullException.ThrowIfNull(hand);
        System.ArgumentNullException.ThrowIfNull(load);

        var joint = new Generic6DofJoint3D { Name = "Grip" };
        parent.AddChild(joint);

        // Set after entering the tree, because GlobalPosition on a detached node is meaningless.
        joint.GlobalPosition = at;

        // The hard limit has to go, or it rather than the spring is what holds the load - and a
        // hard limit is the weightless case E1-01 ruled out.
        joint.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearLimit, false);
        joint.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearLimit, false);
        joint.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearLimit, false);

        joint.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearSpring, true);
        joint.SetFlagY(Generic6DofJoint3D.Flag.EnableLinearSpring, true);
        joint.SetFlagZ(Generic6DofJoint3D.Flag.EnableLinearSpring, true);

        var stiffness = StiffnessFor(load.Mass);
        foreach (var (param, value) in new[]
        {
            (Generic6DofJoint3D.Param.LinearSpringStiffness, stiffness),
            (Generic6DofJoint3D.Param.LinearSpringDamping, Damping),
            (Generic6DofJoint3D.Param.LinearSpringEquilibriumPoint, 0.0f),
        })
        {
            joint.SetParamX(param, value);
            joint.SetParamY(param, value);
            joint.SetParamZ(param, value);
        }

        joint.NodeA = joint.GetPathTo(hand);
        joint.NodeB = joint.GetPathTo(load);
        return joint;
    }
}
