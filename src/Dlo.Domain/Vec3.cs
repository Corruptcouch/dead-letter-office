namespace Dlo.Domain;

/// <summary>
/// Domain's own three-component vector, in Godot's axis convention: Y is up and
/// -Z is forward, so a Game-layer conversion is a field copy and never a remap.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not <c>Godot.Vector3</c>: no Domain signature may name a Godot type
/// (arch §2, standards §0). The Game layer converts at the boundary, once per event
/// kind, and that conversion is the whole reason the L1 suite needs no engine.
/// </para>
/// <para>
/// Deliberately not <c>System.Numerics.Vector3</c> either, which would satisfy the
/// BCL-only rule and cost nothing to adopt. The Game layer writes <c>using Godot;</c>,
/// and a second <c>Vector3</c> in scope makes every boundary call site
/// <c>CS0104: ambiguous reference</c> — the collision standards §1 already records for
/// <c>Environment</c>. A distinct name cannot collide, which is worth more here than
/// the SIMD acceleration Domain has nothing to spend on.
/// </para>
/// </remarks>
/// <param name="X">Metres right.</param>
/// <param name="Y">Metres up.</param>
/// <param name="Z">Metres back; forward is negative.</param>
public readonly record struct Vec3(float X, float Y, float Z);
