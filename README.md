# Dead Letter Office

A four-player co-op game about sorting mail in a facility that is not quite right.

What the game is: [the vision](docs/dead-letter-office-vision.md). How it is built:
[the architecture](docs/dead-letter-office-architecture.md). In what order:
[the epics](docs/dead-letter-office-epics.md) and
[the stories](docs/dead-letter-office-stories.md). How the code should look:
[the coding standards](docs/CODING-STANDARDS.md).

> **This file is a stub.** E14-09 owns the real README — setup from nothing in under ten
> minutes, the pinned editor path per machine, and arch §1.4's gotchas. What is here is the
> part standards §8 refuses to let wait: **the exact test invocations**. A missing invocation
> is an E14 defect, not a convention to reinvent.

## Toolchain

| | |
| :-- | :-- |
| .NET SDK | **10.0.400**, pinned exactly in `global.json` with `rollForward: disable` |
| Godot | **4.7.2-stable-mono** — the version is pinned; the install path is machine-local |
| Physics | Jolt, set explicitly in `project.godot` (arch §1.4) |

Every project targets `net10.0`, overriding the `net8.0` Godot generates. That is a decision,
not an oversight — `Directory.Build.props` carries the reasoning.

**This machine's Godot:** `D:\work\Godot\Godot_v4.7.2-stable_mono_win64\`. Export templates for
4.7.2 are **not yet installed**; E18-01 needs them.

## Build

```
dotnet build dlo.sln
```

## Tests

Three levels (arch §10.1). Run from the repo root.

| Level | Scope | Command | Budget |
| :-- | :-- | :-- | :-- |
| **L1** | Domain, no engine | `dotnet test tests/Dlo.Domain.Tests` | **< 5 s** (arch §8) |
| **L2** | GdUnit4, in engine | `dotnet test tests/Dlo.Game.Tests` | seconds |
| **L3** | Headless host + 3 clients | *arrives with E0-09* | minutes |

**L2 needs `GODOT_BIN` set, per machine.** It is the path to the pinned editor, and like the
editor path itself it is machine-local, so it is an environment variable rather than a
committed `.runsettings`. On this machine:

```
export GODOT_BIN="D:/work/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
```

Use the `_console` executable: the plain one detaches from the terminal and the runner's
output goes nowhere.

**In-editor L2 is not wired up yet.** E14-05 asks for both invocations; only the CLI one
above exists and is verified. Running GdUnit4 from inside the editor additionally needs its
Godot addon installed in `tests/Dlo.Game.Tests/`, which is a click in the editor and has not
been done. Nothing depends on it — CI uses the CLI — but the story is not closed until it is.

### Three L2 gotchas, each of which costs an afternoon alone

1. **`[RequireGodotRuntime]` on every test class that touches a Godot type.** Without it
   GdUnit4 picks its "Default Test Runner", executes the suite in the plain VSTest host with
   no engine behind it, and the first native call dies as `0xC0000005` with a stack trace that
   names no cause. It does not say what is wrong. Nothing does.
2. **`tests/Dlo.Game.Tests/` is its own Godot project**, because the adapter runs
   `godot --path .` from wherever it finds the `.csproj`. Its `res://` is therefore that
   directory — **the game's scenes are not reachable from an L2 test.** Tests build nodes in
   code. *ponytail: the ceiling is that a `.tscn` under `src/Dlo.Game/` cannot be loaded in
   L2, which E1-02's controller scene will want. The upgrade is to emit the test assembly into
   `src/Dlo.Game`'s output so the adapter finds the game project instead — deferred because
   nothing needs it yet.*
3. **Godot rewrites `Dlo.Game.Tests.csproj` if its `<TargetFramework>` line is missing**,
   putting back `net8.0` in CRLF, mid-test-run. Both Godot projects pin `net10.0` in their own
   csproj for this reason. Do not tidy it into `Directory.Build.props`.

The **architecture test** (arch §10.5 — `Dlo.Domain` does not reference `GodotSharp`) is an
L1 test and needs no invocation of its own. It runs with the command above.

**L1 baseline, measured on the development machine 2026-08-24 with 3 tests in the suite:**
24 ms of test execution; 2.4 s wall clock for `dotnet test` from a cleaned `bin`/`obj`,
1.3 s with `--no-build`. The budget is the suite, and the suite has 4.97 s of headroom left.
The number is recorded here so the next measurement is a comparison rather than an argument.

## Formatting

`.editorconfig` is the authority, and this is the gate:

```
dotnet format --verify-no-changes
```

If a formatting question ever reaches a human, `.editorconfig` is missing a rule — fix the
file, not the PR.
