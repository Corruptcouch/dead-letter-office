namespace Dlo.Domain;

/// <summary>
/// Domain's own three-component vector, in Godot's axis convention: Y is up and -Z is forward,
/// so a Game-layer conversion is a field copy and never a remap.
/// </summary>
/// <remarks>
/// Not <c>Godot.Vector3</c>, because no Domain signature may name a Godot type (arch §2,
/// standards §0). Not <c>System.Numerics.Vector3</c> either: the Game layer writes
/// <c>using Godot;</c>, and a second <c>Vector3</c> in scope makes every boundary call site
/// <c>CS0104: ambiguous reference</c>. A distinct name cannot collide, which is worth more here
/// than SIMD acceleration Domain has nothing to spend on.
/// </remarks>
/// <param name="X">Metres right.</param>
/// <param name="Y">Metres up.</param>
/// <param name="Z">Metres back; forward is negative.</param>
public readonly record struct Vec3(float X, float Y, float Z);
