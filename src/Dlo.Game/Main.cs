using Godot;

namespace Dlo.Game;

/// <summary>
/// Empty root scene. E0-04 replaces this with <c>SessionRoot</c>, which is the one
/// place in the codebase that constructs a domain system (arch §3.2).
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        // Without this line `godot --headless --quit` exits 0 whether or not the
        // Dlo.Game assembly loaded at all, so E14-03's "an empty scene runs" would
        // pass for the wrong reason. Reporting the physics engine at the same time
        // confirms Jolt is what actually resolved, not just what project.godot asks
        // for - a fresh 4.7.2 project leaves that setting at DEFAULT, which names a
        // resolution order rather than an engine (arch §1.4).
        // System.Environment is spelled out because `using Godot;` brings
        // Godot.Environment (the 3D world environment resource) into scope and the
        // two collide - CS0104. Expect this from any BCL name Godot also uses.
        GD.Print(
            $"Dlo.Game up. .NET {System.Environment.Version}, " +
            $"physics: {ProjectSettings.GetSetting("physics/3d/physics_engine")}");
    }
}
