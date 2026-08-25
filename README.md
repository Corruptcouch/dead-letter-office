# Dead Letter Office

A four-player co-op game about sorting mail in a facility that is not quite right.

| Document | Normative for |
| :-- | :-- |
| [Vision](docs/dead-letter-office-vision.md) | what the game is |
| [Architecture](docs/dead-letter-office-architecture.md) | how it is built |
| [Epics](docs/dead-letter-office-epics.md) · [Stories](docs/dead-letter-office-stories.md) | in what order |
| [Coding standards](docs/CODING-STANDARDS.md) | how the code should look |
| [AGENTS.md](AGENTS.md) | the laziness ladder — read before writing anything |

---

## Get to green

**From nothing to "built, both suites green" in under ten minutes** — timed against someone
following only this file, 2026-08-24. That is a measurement, not an aspiration, which means it
can regress: if it takes you longer, that is a defect in this file. Say so.

### 1. Install the two pinned tools

| | Version | Where |
| :-- | :-- | :-- |
| .NET SDK | **exactly 10.0.400** | <https://dotnet.microsoft.com/download/dotnet/10.0> |
| Godot | **4.7.2-stable-mono** (the .NET build, not the standard one) | <https://godotengine.org/download/archive/> |

Both versions are pinned, and neither pin is a preference:

- `global.json` names the SDK exactly, with `rollForward: disable`. This machine has four SDKs
  installed (8.0.424, 9.0.315, 10.0.204, 10.0.400) and without the pin `dotnet` silently takes
  the highest. A build that differs between two developers because nobody chose is the failure
  that file exists to prevent — so if `dotnet build` says the SDK is missing, install that
  version rather than editing `global.json`.
- The editor version is pinned because export templates are versioned with it, and because a
  mismatch shows up as `project.godot` churn and export failures rather than as a build error.

Godot needs no installer. Unzip it somewhere and remember the path — **that path is
machine-local and belongs here, not in a committed file.**

> **On this machine:** `D:\work\Godot\Godot_v4.7.2-stable_mono_win64\`
> Add your own line when you set the project up somewhere else.

### 2. Clone and build

    git clone https://github.com/Corruptcouch/dead-letter-office.git
    cd dead-letter-office
    dotnet build dlo.sln

No `git lfs pull` needed — see [Git LFS](#git-lfs) for why, and for what it looks like when
that stops being true.

### 3. Point the tests at Godot

The L2 and L3 suites both launch real Godot processes, so they need to know where the editor
is:

    # Windows (bash / Git Bash)
    export GODOT_BIN="D:/work/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"

    # Windows (PowerShell)
    $env:GODOT_BIN = "D:\work\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"

    # Linux / macOS
    export GODOT_BIN="/path/to/Godot_v4.7.2-stable_mono_linux.x86_64"

**Use the `_console` executable on Windows.** The plain one detaches from the terminal and the
test runner's output goes nowhere, which reads as a hang.

Like the editor path itself this is machine-local, which is why it is an environment variable
and not a committed `.runsettings`.

### 4. Run the suites

    dotnet test dlo.sln

Expect **31 passing tests** across three suites in 15–20 seconds. That is the whole check.

---

## Tests

Three levels ([architecture §10.1](docs/dead-letter-office-architecture.md)). **A missing
invocation here is an E14 defect** (standards §8) — raise it rather than reinventing the
command.

| Level | Scope | Command | Budget |
| :-- | :-- | :-- | :-- |
| **L1** | Domain, no engine | `dotnet test tests/Dlo.Domain.Tests` | **< 5 s** |
| **L2** | GdUnit4, in engine | `dotnet test tests/Dlo.Game.Tests` | seconds |
| **L3** | Four headless peers, real socket | `dotnet test tests/Dlo.Net.Tests` | **< 30 s** |
| **both** | L1 + L2 together | `dotnet test dlo.sln` | — |

L2, L3 and `dotnet test dlo.sln` need `GODOT_BIN` set. L1 does not — it never starts an engine,
which is the entire point of the Domain boundary. L2 also runs from an IDE test explorer, with
breakpoints — see [Running L2 from the editor](#running-l2-from-the-editor).

The **architecture test** ([arch §10.5](docs/dead-letter-office-architecture.md) — `Dlo.Domain`
does not reference `GodotSharp`) is an L1 test and needs no invocation of its own. CI also runs
it by name, so a reversed dependency arrow shows up as an unmistakable red step rather than as
one line inside another suite's output.

**L1 baseline, measured 2026-08-24 with 3 tests:** 24 ms of test execution; 2.4 s wall clock
from a cleaned `bin`/`obj`, 1.3 s with `--no-build`. Recorded so the next measurement is a
comparison rather than an argument.

### Four L2 gotchas, each of which costs an afternoon alone

1. **`[RequireGodotRuntime]` on every test class that touches a Godot type.** Without it
   GdUnit4 picks its "Default Test Runner", executes the suite in the plain VSTest host with no
   engine behind it, and the first native call dies as `0xC0000005` with a stack trace that
   names no cause. Nothing tells you what is wrong.
2. **`tests/Dlo.Game.Tests/` is its own Godot project**, because the adapter runs
   `godot --path .` from wherever it finds the `.csproj`. Its `res://` is therefore that
   directory — **the game's scenes are not reachable from an L2 test**, so tests build nodes in
   code. *ponytail: the ceiling is that a `.tscn` under `src/Dlo.Game/` cannot be loaded in L2,
   which E1-02's controller scene will want. The upgrade is to emit the test assembly into
   `src/Dlo.Game`'s output so the adapter finds the game project instead — deferred because
   nothing needs it yet.*
3. **Godot rewrites `Dlo.Game.Tests.csproj` if its `<TargetFramework>` line is missing**,
   putting back `net8.0` in CRLF, mid-test-run. Both Godot projects pin `net10.0` in their own
   csproj for this reason. Do not tidy it into `Directory.Build.props`.
4. **GdUnit4 does not run Godot headless for you, and the error says something else entirely.**
   It launches Godot twice: the compile pass passes `--headless`, the test-runner pass does
   not. Anywhere without a display — any CI runner — Godot fails to create a window and exits
   1, and because the process is gone the adapter reports:

       GdUnit4 Godot Runtime Test Runner ends with exit code: 1
       Failed to connect: Connection timeout

   **That message points at the network. The problem is the display.** Anyone who trusts it
   loses the afternoon to firewalls and ports. `.runsettings` supplies `--headless` and
   `Directory.Build.props` wires it in, so the command needs no flag — but if you ever see
   that pair of lines again, read them as "the Godot process died before it could accept a
   connection" and go and find out why it died.

### Running L2 from the editor

The GdUnit4 adapter is a normal VSTest adapter, so **the tests appear in the test explorer of
VS Code, Visual Studio and Rider** with discovery, run and debug — including breakpoints
inside a test running in a live Godot process, which is the reason to use it over the CLI.
Confirmed working 2026-08-24.

Two things it still needs, both of which are easy to forget because the CLI path sets them up
for you:

- **`GODOT_BIN` must be visible to the IDE**, not just to your shell. An IDE launched before
  you exported the variable will not see it; set it machine-wide, or restart the IDE from a
  shell that has it.
- **`.runsettings` is picked up automatically** via `Directory.Build.props`, so the headless
  flag applies here too and no window appears. That is deliberate — see gotcha 4 above.

*(GdUnit4 also ships a panel that runs inside the **Godot** editor, which needs its addon
installed under `tests/Dlo.Game.Tests/addons/`. That is not installed here and nothing needs
it; the test explorer covers the same ground without adding a tracked dependency.)*

### L3 — four headless peers

`dotnet test tests/Dlo.Net.Tests` boots **four separate Godot processes** — one host, three
clients — over ENet on `127.0.0.1:27377`, and asserts that an intent RPC arrives and a
replicated value converges on all three clients.

**Four processes and not four `SceneTree`s** is E0-08's finding, and it is not about speed: in
one process all four peers share one physics world, one CLR and one set of autoloads, which
would make every physics-bearing L3 assertion a lie. The measurements behind that are on
`FourPeerSession`.

- **It costs about 0.6 s**, against arch §10.1's "minutes" budget. There is no reason to avoid
  running it.
- **CI runs it on merge to main only**, as a separate job (arch §10.6). Not because it is
  slow, but because a network suite is the one most likely to go flaky on a shared runner.
- **It fails, loudly, when `GODOT_BIN` is unset.** It does not skip. An L3 suite that skips
  itself reports green, which is worse than not having one.
- **A failure prints every peer's position** — exit code, connection id, what it held, how
  many attempts it took. That transcript is the point of the harness; read it before doing
  anything else.
- **It needs a Debug build.** Godot loads a project's C# from `.godot/mono/temp/bin/Debug`
  whatever configuration you built in, so `dotnet test -c Release` here fails with a message
  telling you so rather than with four timeouts.
- **The peers run `tests/Dlo.Net.Tests` as their Godot project**, not `src/Dlo.Game`, so no
  harness-only code ships. The ceiling is the same one L2 has: `SessionRoot` is built as an
  ordinary node, so its registration as an autoload is not covered.

---

## Working in the editor

Open `src/Dlo.Game/project.godot` in **Godot 4.7.2-stable-mono**.

### Gotchas — read before you fight the tooling

These are [arch §1.4](docs/dead-letter-office-architecture.md), abbreviated. Each has already
cost someone an afternoon, or was found by probing specifically so it would not.

- **C# hot reload does not pick up changes in referenced assemblies.** When you change anything
  in `Dlo.Domain`, **restart the editor.** This is normal, it is not your setup, and it is the
  single most common way to lose an afternoon on this project.
- **Godot regenerates `Dlo.Game.csproj`.** Project references survive; custom MSBuild
  properties do not. All custom config lives in `Directory.Build.props` at the repo root.
- **`<TargetFramework>net10.0</TargetFramework>` in the two Godot csproj files is deliberate.**
  Godot generates `net8.0` and we override it — and Godot re-adds the line as `net8.0` whenever
  it finds it *missing*, so the override cannot live in `Directory.Build.props` for those two.
  Read the comment in that file before "fixing" this.
- **`project/solution_directory="../.."` stays in `project.godot`.** Without it the exporter
  finds no solution and refuses every C# source at export time.
- **`dlo.sln` is the only solution**, and it must stay that way. If a second one containing the
  `Dlo.Game` assembly appears under the solution directory, Godot's editor plugin refuses to
  start — no build, no export, no C# in the editor at all.
- **`physics/3d/physics_engine="Jolt Physics"` is set explicitly** and must stay. A fresh 4.7.2
  project leaves it at `DEFAULT`, which names a resolution order rather than an engine, and
  every tuning number in arch §8 assumes Jolt.
- **Godot cannot create the C# project from the command line.** It is strictly
  *Project → Tools → C# → Create C# solution*, once, by hand. Only matters if you bootstrap a
  new Godot project; ours is committed.

### Export templates

**Templates are versioned with the editor and must match it exactly.** They are not needed to
build or to run either suite — only to export.

> **On this machine they are missing.** Installed: `4.6.stable.mono`. Required:
> `4.7.2.stable.mono`. Install via *Editor → Manage Export Templates*. **E18-01 fails on its
> first attempt until this is done**, and export is also the one leg of the `net10.0` override
> that has never been verified — it would fail as a silent refusal of every C# source rather
> than as a build error.

---

## Git LFS

`.png`, `.jpg`, `.wav`, `.ogg`, `.glb`, `.fbx`, fonts and friends are LFS-tracked. **`.tres` is
deliberately not** — content files must stay diffable or E13's authoring pipeline loses code
review entirely, and a routing policy you cannot read in a PR is a routing policy nobody checks.

Both halves are verified rather than assumed (E14-08, 2026-08-24): a checked-in `.png` stored
as a 70-byte pointer, and a checked-in `.tres` stored as text whose diff read as
`-mass_kg = 2.5` / `+mass_kg = 3.1`.

Line endings are LF in the repository *and* in the working tree on every platform, via
`* text=auto eol=lf`. `core.autocrlf` is per-machine and this project cannot depend on four
people configuring it identically — and `dotnet format --verify-no-changes` fails against a
CRLF working tree, so without this a fresh Windows clone fails a check CI reports green.

That last claim stopped being an argument on 2026-08-24: the format gate now passes on
ubuntu-latest against a tree authored on Windows, which is the first actual cross-platform
evidence that the normalisation holds. It is re-checked on every push, which is the only place
a line-endings promise can survive.

### What a clone that skipped `git lfs pull` looks like

**Today: nothing. There are no binary assets yet, and a fresh clone builds and runs both suites
with no LFS fetch at all.** CI actively protects that — its checkout step deliberately does
*not* set `lfs: true`, so the promise stays tested rather than assumed.

It stays true longer than you would expect, because placeholders are **generated** rather than
committed (E17-03). So when this does bite it will be rare enough to be baffling, which is
exactly why it is written down in advance:

- Files are present with the right names, but each is **a few hundred bytes of text** beginning
  `version https://git-lfs.github.com/spec/v1`.
- Godot logs import failures for them, or imports them as blank. **Textures render as nothing
  rather than as a missing-texture pattern**, so it reads as a lighting or material bug rather
  than as a missing file.
- The build stays green throughout. Nothing about it looks like a fetch problem.

The fix is `git lfs install` once per machine, then `git lfs pull`.

---

## Formatting and CI

`.editorconfig` is the formatting authority, and this is the gate:

    dotnet format --verify-no-changes

If a formatting question ever reaches a human, `.editorconfig` is missing a rule — fix the
file, not the PR.

[CI](.github/workflows/ci.yml) runs restore, build, format, L1, the architecture test and L2 on
every push. L3 arrives with E0-09 and will run on merge only; `ContentTool validate` arrives
with E13-06.
