using Godot;

namespace Dlo.Game;

/// <summary>
/// The root scene. Session lifecycle lives in the <c>SessionRoot</c> autoload (arch §3.2, §6.2),
/// not here.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        // Without this line `godot --headless --quit` exits 0 whether or not the Dlo.Game assembly
        // loaded at all, so E14-03's "an empty scene runs" would pass for the wrong reason.
        // Printing the physics engine confirms Jolt is what actually resolved, not just what
        // project.godot asks for: a fresh 4.7.2 project leaves that setting at DEFAULT, which names
        // a resolution order rather than an engine (arch §1.4).
        // System.Environment is spelled out because `using Godot;` brings Godot.Environment into
        // scope and the two collide - CS0104 (standards §1).
        GD.Print(
            $"Dlo.Game up. .NET {System.Environment.Version}, " +
            $"physics: {ProjectSettings.GetSetting("physics/3d/physics_engine")}");
    }
}
