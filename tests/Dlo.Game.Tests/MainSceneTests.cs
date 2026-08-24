using GdUnit4;
using Godot;

using static GdUnit4.Assertions;

namespace Dlo.Game.Tests;

/// <summary>
/// The L2 harness check (E14-05). Its job is to prove the level is real: a Godot runtime is
/// running, this repo's <c>Dlo.Game</c> assembly is loaded inside it, and the scene tree is
/// live. L1 can answer none of that — it never starts an engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>[RequireGodotRuntime] is not optional and its absence is not survivable.</b> Without
/// it GdUnit4 picks its "Default Test Runner", which runs the suite in the plain VSTest host
/// with no engine behind it, and the first native call dies with <c>0xC0000005</c> and a
/// stack trace that names no cause. Cost to diagnose from scratch: an afternoon (E14-05).
/// </para>
/// <para>
/// This project is its own Godot project, so its <c>res://</c> is <c>tests/Dlo.Game.Tests/</c>
/// and the game's scenes are not reachable from here — hence a node built in code rather than
/// <c>Main.tscn</c> loaded. See the README for the ceiling and what it costs.
/// </para>
/// </remarks>
[TestSuite]
[RequireGodotRuntime]
public class MainSceneTests
{
    [TestCase]
    public void Main_is_a_Node3D_and_enters_the_live_scene_tree()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var main = AutoFree(new Main())!;

        // One assertion per fact (standards §8). These fail for different reasons: a changed
        // base class, versus an engine whose scene tree never came up.
        AssertThat(main).IsInstanceOf<Node3D>();

        tree.Root.AddChild(main);
        AssertBool(main.IsInsideTree()).IsTrue();

        tree.Root.RemoveChild(main);
    }
}
