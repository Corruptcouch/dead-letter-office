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
| **L2** | GdUnit4, in engine | *arrives with E14-05* | seconds |
| **L3** | Headless host + 3 clients | *arrives with E0-09* | minutes |

**L1 baseline, measured on the development machine 2026-08-24 with 2 tests in the suite:**
20 ms of test execution; 2.5 s wall clock for `dotnet test` from a cleaned `bin`/`obj`,
1.3 s with `--no-build`. The budget is the suite, and the suite has 4.98 s of headroom left.
The number is recorded here so the next measurement is a comparison rather than an argument.

## Formatting

`.editorconfig` is the authority, and this is the gate:

```
dotnet format --verify-no-changes
```

If a formatting question ever reaches a human, `.editorconfig` is missing a rule — fix the
file, not the PR.
