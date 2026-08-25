# Dead Letter Office — Technical Architecture

**Status:** Draft v0.2 · Godot 4.7.2 · .NET / C# · Companion to the Vision document
**Audience:** developers picking up epics. This document is the answer to "how should I build this?" — if it doesn't answer your question, that's a defect in this document. Raise it rather than guessing.

> **Derived from** [dead-letter-office-vision.md](dead-letter-office-vision.md). Where this
> document and the vision disagree about *what* the game is, the vision wins. Where they
> disagree about *how* to build it, this document wins.
>
> **v0.1 → v0.2 — owner decisions, 2026-08-24.** Four of this document's eight open technical
> items are closed and two sections changed shape as a result.
>
> **Voice is not a Steam dependency** (§6.5, §8). Native capture plus a pure-C# Opus encoder,
> routed over the `IGameTransport` abstraction that already exists for gameplay. This
> decouples E7 from open item 1 entirely — the project's largest risk no longer has two
> epics hanging off it — at the cost of ~27 KB/s of host upstream, which is why §8's budget
> is now split rather than a single number.
>
> **Culpability never expires** (§4.6). The owner took the simplest of the four options: last
> toucher owns it until another actor overwrites it. `CulpabilityWindow` is deleted, along
> with the `ponytail:` shortcut that named it — there is no timer, no constant, and no
> per-event-kind table. `UNATTRIBUTED` survives as a narrower but real category: events on
> objects **no player has ever touched**, which is the facility's own damage. The recorded
> risk is stale attribution (you touched a box in minute 2, it falls in minute 11); it is
> partly self-limiting, because objects that cause events are objects being handled.
>
> Also closed: **the shift-length target** (§11 item 8 → 8–12 min × 3–6 shifts, provisional)
> and **the horror ceiling** (architecture only, no agent — see the epics document's E6/E9).
> Newly recorded: an Opus package becomes the project's one new runtime dependency, and
> peer-to-peer voice routing is an available optimisation rather than a requirement.

---

## 1. The one decision everything else follows from

Vision §18 states the principle negatively: *do not build a physics game and then network it.*
Stated positively, as a rule you can apply while writing a line of code:

> **A fact about the shift becomes true only when the host's domain layer says so.
> Physics proposes. The domain disposes.**

A parcel is not misrouted because it fell down chute 3. It is misrouted because chute 3
told the host "parcel 41 entered me", and the host's `RoutingRules.Evaluate` decided that
parcel 41 belonged in chute 7. The rigid body is how parcel 41 *got* there, and what it
*looked like* getting there. It is not the reason anything is true.

This single rule resolves what would otherwise be the central tension in the build: the
obvious objection to an engine-free domain is that Dead Letter Office is a physics game —
physics *is* the gameplay, so how can the rules be engine-free?

They can, because **physics is an input device.** It is a very expensive, very funny
keyboard. The domain consumes events from it the same way it consumes a button press.

```
┌──────────────────────────────────────────────────────────────┐
│  Dlo.Game  (Godot 4.7, runs on every peer)                   │
│  Character controller · grab joints · parcel bodies ·         │
│  conveyors · chutes · doors · VFX · PA audio · HUD            │
└───────────┬──────────────────────────────────┬───────────────┘
            │ physical events                  ▲ replicated state
            │ (host peer only)                 │ (host → clients)
            ▼                                  │
┌──────────────────────────────────────────────────────────────┐
│  Dlo.Domain  (pure C#, no Godot import, ticks on host only)  │
│  Parcel records · manifests · routing rules · policy ·        │
│  shift clock · quota · wrongness table · culpability ·        │
│  the blame ledger · PA line gating                            │
└──────────────────────────────────────────────────────────────┘
```

**The dependency arrow never reverses**, and it is enforced by an architecture test
(§10.5), not by discipline — discipline fails at 11pm.

### 1.1 What this buys, specifically

Three things, and none of them is architectural purity:

1. **The two epics that make it *this* game become the easiest things to test.**
   Vision §14's honest read is that E0–E6 produce a competent chaos co-op indistinguishable
   from its competitors, and that **E8 (blame) and E9 (wrongness) are the differentiators.**
   Both are pure data transformations. Under this boundary they are `dotnet test` material —
   no engine, no four peers, no parcel to drop. The differentiator is the cheap part to
   verify. That inversion is the whole argument.

2. **Host authority is structural rather than remembered.** The domain only exists on the
   host (§3.2). There is no code path where a client decides a parcel was misrouted, because
   there is no client-side thing that could decide it.

3. **The report can be trusted.** Vision §7 requires that every consequential action carry an
   actor attribution. A ledger fed by one authoritative decision-maker is correct by
   construction. A ledger assembled from four peers' opinions is a distributed-consensus
   problem nobody signed up for.

### 1.2 Where the boundary explicitly does *not* go

The house pattern pushes further: an engine-free simulation of the whole game, and a headless
harness that balances it overnight. **That is the wrong target here, and we are not doing it.**

That project is a survivors-like — a spreadsheet with particle effects, so simulating it is
meaningful. Dead Letter Office lives or dies on whether a cardboard box feels heavy, awkward,
and *responsive* in four pairs of hands over real internet (vision §15). No headless
simulation can answer that, and building one would create two models of the same conveyor belt
that must agree — a second source of truth, which is the defect this boundary exists to
prevent.

**So: there is no abstract model of the belt.** There is one belt, made of Jolt bodies, and a
domain that is told what happened on it.

### 1.3 Project layout

Mirrors the house layout so the tooling knowledge transfers.

```
/dlo.sln
  /src/Dlo.Domain/            class library, net10.0, no Godot reference
  /src/Dlo.Game/              the Godot project (project.godot lives here)
  /tests/Dlo.Domain.Tests/    xUnit — fast, no engine (L1)
  /tests/Dlo.Game.Tests/      GdUnit4 — engine required (L2)
  /tests/Dlo.Net.Tests/       headless multi-peer harness (L3, §10.4)
  /tools/Dlo.ContentTool/     validates and generates authored content (E13, E17)
```

### 1.4 Known gotchas — read before you fight the tooling

The first four are inherited from the house project, and each cost someone an afternoon already.
**The next three came from probing the pinned 4.7.2 install directly** (2026-08-24), before they
could cost anyone one — so they are findings rather than scar tissue, and they are stated as
what the binary does rather than as what the release notes say.

**The last three came from actually building E14-03** (2026-08-24), and they are the ones to read
if the tooling is fighting you: a probe tells you what the binary does when you ask it nicely,
and building tells you what it does when you are not looking. Two of them **correct a bullet
above** rather than adding to it, which is the honest reason they are worth the space.

- **Godot regenerates `Dlo.Game.csproj`.** Project references to `Dlo.Domain` survive it;
  custom MSBuild properties do not. Keep custom config in `Directory.Build.props` at the
  repo root.
- **`project/solution_directory="../.."`** must be set in `project.godot`. Godot's exporter
  otherwise looks beside `project.godot`, finds no solution, and refuses every C# source
  during export. **Amended by E14-03:** with the setting in place, Godot searches that
  directory for *any* solution containing a project whose **assembly name** matches — it is
  not looking for a file called `Dlo.Game.sln`. That is why `dlo.sln` serves, and it is what
  makes the one-solution layout of §1.3 possible at all. See the last three bullets.
- **C# hot-reload does not reliably pick up changes in referenced assemblies.** When you
  change Domain code, expect to restart the editor. This is normal; don't spend an afternoon
  on it.
- **The Domain assembly must be copied to the export.** It is, via project reference — verify
  this in the first export build (E18), not the week of launch.
- **`project.godot` must set `physics/3d/physics_engine="Jolt Physics"` explicitly.** A fresh
  4.7.2 project leaves that setting at the string `DEFAULT` — probed in the editor, not assumed —
  and `DEFAULT` means *whatever this build registers first*, which is a resolution order rather
  than a promise. Naming Jolt outright is the only way the file states which engine you actually
  got, and **every tuning number in §8 assumes Jolt.** A project migrated from an older template
  is the second way this goes wrong silently.
- **The editor is pinned at 4.7.2-stable-mono**, and export templates are versioned with it. On
  the development machine the editor lives at
  `D:\work\Godot\Godot_v4.7.2-stable_mono_win64\` — that path is machine-local, and the README
  (E14) is where each machine records its own. **Templates matching the exact editor version must
  be installed before E18 can export anything**; a missing or mismatched set fails at export time,
  not at build time, which is the expensive end to discover it.
- **Godot generates `net8.0`. We target `net10.0` and override it — that is a decision, not an
  oversight, and the next person to "fix" it should read this bullet first.** Godot 4.7.2's
  project generator hardcodes `net8.0` (the only TFM string in `GodotTools.ProjectEditor.dll`)
  and GodotSharp ships against it. That is Godot's floor, not a ceiling. Measured on the
  development machine, 2026-08-24, all four separately:
  - `net10.0` **builds** against GodotSharp — a net8.0 library referenced from a net10.0 project
    is ordinary, not a workaround.
  - Godot's own `--build-solutions` **builds it**, so the in-editor build path is not bypassed.
  - The editor **does not rewrite a `<TargetFramework>` that already has a value** — opening the
    project headless left the csproj byte-identical. The regeneration warning above applies to
    custom *properties*, not to the TFM. **Read the correction below before acting on this one.**
  - It **runs**, reporting `.NET 10.0.11`.

  **And the runtime was `.NET 10.0.11` at `net8.0` too**, because Godot's host reads
  `GodotPlugins.runtimeconfig.json` — 8.0.0 with `rollForward: LatestMajor` — and takes the
  newest major installed. So `net8.0` would mean **compiling against an older BCL than the one
  actually executing**, and adopting a framework that leaves support in November 2026, before
  this project ships. There is no runtime cost either way; the only thing the TFM buys or denies
  is compile surface.

  **The one piece still unverified is export** (E18): it needs 4.7.2 export templates, which are
  not installed yet. E18-01 verifies `net10.0` survives a real export, and that is the story to
  revisit this bullet from if it does not.

- **Correction from E14-03: "does not rewrite it" is not the same as "leaves it alone."** Godot
  **re-inserts `<TargetFramework>net8.0</TargetFramework>` whenever it finds the line missing.**
  So the obvious reading of the bullet above — delete the line, let `Directory.Build.props`
  supply `net10.0`, keep one authority — **does not work.** The line returns on the next editor
  run, and because MSBuild imports `Directory.Build.props` *before* the project body, the
  returned `net8.0` wins outright.

  `src/Dlo.Game/Dlo.Game.csproj` therefore carries an explicit `net10.0` of its own, and that
  is what makes the override stick — verified byte-identical across a full editor session.
  `Directory.Build.props` stays the authority for the five hand-authored projects;
  **`Dlo.Game` is the documented exception, and it has to be.** Godot only fills in an absent
  line; it never touches one that has a value.

  (Godot also generated a second TFM line, `net9.0` conditioned on `GodotTargetPlatform ==
  'android'`. We ship three desktop targets (E18-01), so it is deleted rather than carried.
  Restore it there if Android ever becomes real.)

- **Exactly one solution under `solution_directory` may contain the `Dlo.Game` assembly.**
  *Project → Tools → C# → Create C# solution* writes its own `Dlo.Game.sln` into that directory.
  Leave it there **and** add `Dlo.Game` to `dlo.sln`, and Godot's editor plugin refuses to start:

  > `ERROR: Multiple solutions containing a project with assembly name 'Dlo.Game' were found`

  It fails in `GodotSharpDirs.DetermineProjectLocation()`, which means **no build, no export and
  no C# in the editor at all** — not a warning. Godot's `Dlo.Game.sln` is deleted; `dlo.sln` is
  the one solution, holding all six projects (§1.3).

  A consequence worth naming, because it is silent until export time: **`dlo.sln` has to declare
  `ExportDebug` and `ExportRelease`.** Godot builds exports with those configurations and its own
  generated solution carried them; `dotnet new sln` creates only `Debug` and `Release`, so
  `dotnet build dlo.sln -c ExportRelease` fails with `MSB4126` until they are added by hand. Only
  `Dlo.Domain` and `Dlo.Game` get a `Build.0` under them — an export has no business building the
  test suites.

- **Godot cannot create the C# project from the command line.** There is no flag for it:
  `--build-solutions` is a silent no-op when no `.csproj` exists, adding a `.cs` file does not
  trigger it, and opening the project in the editor does not either. It is strictly
  *Project → Tools → C# → Create C# solution*, once, by hand. This costs CI nothing, because the
  csproj is committed — it strands whoever bootstraps the *next* Godot project, which is why it
  is written down here rather than rediscovered. (Godot also strips `"C#"` back out of
  `config/features` on each run and does not appear to need it; leave that alone.)

---

## 2. Coding standards

Normative in [`CODING-STANDARDS.md`](CODING-STANDARDS.md) — this project's own document,
derived from the house conventions rather than inherited verbatim. The house rules assume a
deterministic GDScript simulator, and this project is neither: §4.2 makes us explicitly not
determinism-dependent, so that document's central rule does not transfer. Read ours before your
first PR. It is short, and it carries the review checklist.

Three points are restated here because the examples in this document depend on them:

- **No `Godot` type in a Domain signature, including `Vector3`.** Domain has its own `Vec3`.
  Yes, converting at the boundary is annoying. It is annoying exactly once per event kind, and
  it is what keeps the L1 suite engine-free. **This is the rule most likely to be broken by
  accident.**
- **Nullable enabled.** Warnings-as-errors in `Dlo.Domain` only; the Game layer builds
  Godot's noisy generated glue and failing on it means nobody can build.
- **`ponytail:` marks intentional simplifications**, and names both the ceiling and the
  upgrade path:

```csharp
// ponytail: railed parcels extrapolate from a constant belt speed (§3.4).
// Ceiling: a client shows a railed parcel in the wrong place for one RTT after any
// belt speed change, and nothing currently changes belt speed.
// Upgrade: send speed with the rail packet and interpolate — the field already
// exists on BeltState, so this is a serialisation change and not a design one.
```

Naming, XML docs, file-scoped namespaces, the network rules, the test discipline and §8's
budgets-as-rules live in the standards document rather than here, so there is one place to
change them.

---

## 3. The network model

### 3.1 Authority

**One host, full authority, no exceptions and no prediction of gameplay state.** Vision §3.2
sets the goal precisely: *not synchronized physics — all four players see the same meaningful
gameplay state.* Guardrails (§16) explicitly exclude rollback, deterministic physics, and
dedicated servers.

Godot's high-level multiplayer API gives us this directly.

| Concern | Mechanism |
| :-- | :-- |
| Who owns a node | `SetMultiplayerAuthority()` / `IsMultiplayerAuthority()` |
| State host → clients | `MultiplayerSynchronizer`, host-authoritative |
| Object lifetime | `MultiplayerSpawner` with a custom spawn function (§5.2) |
| Client → host intent | `[Rpc(MultiplayerApi.RpcMode.AnyPeer)]` |
| Host → client commands | `[Rpc]` (defaults to `Authority`) |

The C# attribute's defaults are `[Rpc(RpcMode.Authority, CallLocal = false, TransferMode =
Reliable, TransferChannel = 0)]`. **Two of those defaults are traps in this game:**

- **`Reliable` carries, in the engine's own words, "a significant performance penalty."**
  It is correct for *decisions* (a stamp was applied, a parcel was destroyed, the shift
  ended) and wrong for *streams*. Anything positional is `Unreliable` or
  `UnreliableOrdered`.
- **`CallLocal = false`** means the host does not run its own RPC. For an intent RPC that
  the host must also honour when the host presses the button, that is a silent no-op on one
  of the four machines. Set it deliberately every time.

The interaction rule, stated once so no story has to re-derive it:

> **Clients send intent. The host sends outcomes. A client never sends a fact.**

`RequestGrab`, `RequestStamp`, `RequestOpen` — never `IWasMisrouted` or `ITookDamage`.

### 3.2 Where the domain lives

`Dlo.Domain` is referenced by every peer, because clients need its *types* to deserialize
replicated manifest and report data. Its *systems* tick only on the host.

This is one seam, deliberately, so there is one place to get it right:

```csharp
// Dlo.Game/net/SessionRoot.cs
public override void _Ready()
{
    if (Multiplayer.IsServer())
        _host = new HostSession(new ShiftDirector(...), new ShiftLedger(), _random);
    // Clients construct nothing. There is no client-side ShiftDirector to drift.
}
```

`SessionRoot._Ready` is the only place in the codebase that constructs a domain system, and it
does so behind exactly one branch. `HostSession` owns them from there; nothing else creates one,
and the systems are passed in rather than built internally so the L1 suite can substitute stubs.
An architecture test cannot catch a second `new ShiftDirector()`; a `grep` in review can, and
the review checklist asks for it.

### 3.3 The grab protocol, and why it may lie

Vision §3.1 is emphatic: **awkward ≠ unresponsive. The parcel is the problem, not the
input.** Get it backwards and the game reads as broken rather than funny.

Strict host authority conflicts with this directly. At 80ms RTT, a grab that waits for host
confirmation is a grab that feels broken. So the grab is the **one place where the client
acts before it is allowed to**:

```
client                                     host
  │ press grab
  ├─ hand animation plays NOW
  ├─ local visual joint attaches NOW  ─────► RequestGrab(parcelId)
  │                                            │ validate: in range?
  │                                            │ already held? policy-locked?
  │  ◄──────────────────────────────────────── GrabResolved(parcelId, holder)
  ├─ confirmed → nothing visibly changes
  └─ denied    → the parcel snaps out of your hands
```

**The mispredicted case is a feature.** In a shooter, a rolled-back grab is jank. Here it is
a teammate yanking the box away half a second before you got it, which is vision §3.5's
comedy engine firing for free. This is the only optimistic path in the build, and it is
justified by that specific property — do not generalise it to stamping, opening, or
incinerating, where a rollback would un-decide something the report already recorded.

The **real** joint only ever exists on the host. Clients hold a visual-only attachment.
Two-player cooperative carry (E1) is two host-owned joints on one body; the domain declares
which parcels exceed one-person capacity and Jolt enforces the consequence.

### 3.4 Replication budget — the belt never stops

This is the hard performance problem, and it comes straight from the design keystone. "The
belt never stops" (vision §2) means parcels accumulate on purpose, so the naive approach
scales badly:

> 80 awake bodies × 28 bytes of transform × 30 Hz × 3 clients ≈ **200 KB/s upstream from a
> domestic connection.** Not viable.

Every parcel is therefore in one of **three replication classes**, and the common case costs
almost nothing:

| Class | When | Replicated | Cost |
| :-- | :-- | :-- | :-- |
| **Railed** | Riding a conveyor or in a tube | `(beltId, distanceAlong, lane)` once on entry | ~6 bytes, once |
| **Dynamic** | Loose, thrown, held, falling | Transform, `UnreliableOrdered` | Full rate |
| **Sleeping** | At rest, Jolt reports asleep | Nothing. Final transform on sleep | Zero |

A railed parcel is kinematic on a known spline at a known speed, so clients extrapolate it
with **no ongoing traffic at all**. Since the belt is always full, the majority of parcels in
a shift are railed or asleep. Knock one off the belt and it promotes `Railed → Dynamic`; let
it settle and it demotes `Dynamic → Sleeping`.

`Railed` is the reason this design survives its own keystone. Treat any story that puts a
belt parcel into `Dynamic` as a bug against this section.

Two consequences worth stating:
- **Tube transit does not replicate a body.** A parcel in a pneumatic tube is
  `(tubeId, eta)`. Its Godot node may be freed entirely; the domain record persists (§5.1).
- **`MultiplayerSynchronizer.replication_interval` is set per class**, not globally.

### 3.5 Transport, and the one real project risk

Vision E12 requires one-click Steam invites, which means Steam P2P — `SteamNetworkingSockets`
behind Godot's `MultiplayerPeer`. **The C# path to that is the least mature dependency in the
whole plan**, and pretending otherwise now would be expensive later:

- GodotSteam's separate `multiplayerpeer` repository is retired, merged into the main
  GodotSteam 4.x branch.
- The widely-referenced GodotSteam **C# bindings (LauraWebdev) have not been updated for
  GodotSteam 4.11+**; a community fork is what actually works.
- The C# `SteamMultiplayerPeer` port is a **seven-commit project that states channels are
  not implemented.**

None of that is disqualifying — 4 peers, low bandwidth, no channels needed. But it is not a
dependency to discover at export time. So:

```csharp
public interface IGameTransport   // Dlo.Game, not Domain — it is Godot-facing
{
    MultiplayerPeer CreateHost(int maxPeers);
    MultiplayerPeer CreateClient(string address);
}
```

Two implementations: `EnetTransport` (development, automated tests, no Steam client needed)
and `SteamTransport` (shipping). **E0 builds both and proves both in week one**, then
`Dlo.Net.Tests` runs against ENet forever. If the Steam C# path turns out to be unworkable,
we learn it while there is a game to redesign around it rather than a launch date.

Add a third for development only:

```csharp
// ponytail: LatencyPeer delays and reorders packets in a decorator over MultiplayerPeer.
// Ceiling: a fixed delay plus jitter, no bandwidth cap or congestion modelling.
// Upgrade: only if a real bug needs it — clumsy/netem on the host is closer to truth.
```

Vision §15's validation question is *"over real internet"*. A lag harness is therefore
**required infrastructure, not a nicety** — without it the MVP answers an easier question
than the one that matters.

---

## 4. Domain layer design

### 4.1 Composition and data, not inheritance

No `FragileParcel : Parcel : Entity`. A parcel is a record plus components; variation is
data. Adding a parcel archetype, a hazard, or a mutation must never require a new class —
that is the concrete form of open/closed here, and it is what makes E13 possible at all.

### 4.2 Determinism where it is cheap, and only there

**All domain randomness goes through an injected `IRandom`.** No `GD.Randi()`, no
`Random.Shared`, no static RNG in Domain.

We are explicitly *not* determinism-dependent — guardrails exclude deterministic multiplayer
physics, and Jolt's stability is not a promise we build on. `IRandom` earns its place for one
much smaller reason: **a bug report can carry a seed.** "The facility generated a loading
dock inside the break room" is only fixable if you can regenerate that facility.

```csharp
public interface IRandom
{
    int NextInt(int minInclusive, int maxExclusive);
    float NextFloat();
    T Pick<T>(IReadOnlyList<T> items);
    T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weight);
}
```

### 4.3 No fixed timestep for the simulation

A deliberate divergence from the house pattern, recorded because it looks like an omission.

The house project runs a fixed 60Hz domain tick because its damage-over-time and difficulty
scaling would otherwise be frame-rate dependent. Dead Letter Office has almost no continuously
integrating domain state — the domain is **event-driven**. A parcel is scanned, stamped,
routed, or destroyed at an instant. The shift clock is the one continuous quantity, and it is
wall-clock seconds owned by the host.

Physics runs in `_PhysicsProcess` at Godot's own fixed rate, as physics must. The domain
consumes discrete events from it and does not tick alongside it.

### 4.4 The parcel lifecycle

One state machine, host-owned, and every transition is a ledger opportunity:

```
   AUTHORED ──► IN_TRANSIT ──► INSPECTED ──► STAMPED ──► ROUTED ──► RESOLVED
                    │              │            │           │
                    └──────────────┴────────────┴───────────┴──► DESTROYED
                                                                (incinerated,
                                                                 crushed, escaped,
                                                                 lost to Layer 3)
```

`RESOLVED` splits into `CorrectlyRouted` and `Misrouted` — and critically, **the player is
not told which.** Vision §7 puts breakage and misrouting on the end-of-shift report, so
misrouting is a fact the domain records silently and reveals at the whistle. Resist any story
that adds a live "wrong chute!" indicator; the delayed reveal is the blame engine's ammunition.

### 4.5 Routing correctness is a pure function

```csharp
public static RoutingOutcome Evaluate(ParcelRecord parcel, ChuteId chute, PolicyState policy);
```

No engine, no clock, no side effects. The single most-tested function in the codebase, and
the reason "did the shift score correctly?" never requires four peers and a controller.

`PolicyState` is what makes the bureaucracy the antagonist mechanically rather than
tonally: it is a **mutable, replicated set of active rules that the PA system can change
mid-shift.** The routing chart on the far wall (vision §8) renders `PolicyState`. When
management issues an update, the chart changes and the crew's shared knowledge silently goes
stale. That is the antagonist landing a hit, expressed as a data change.

### 4.6 Culpability — the plumbing under the highest-value feature

Vision §7 requires that every consequential action carry an actor, and that unattributable
events be labelled `UNATTRIBUTED` rather than hidden. This needs a mechanism, and it is
cheap:

```csharp
public readonly record struct Culpability(ActorRef LastToucher, float TouchedAtShiftTime);
```

Every networked physical object carries one. The rules:

1. **Touching sets it.** Grab, stamp, throw, scan — the actor becomes `LastToucher`.
2. **It propagates along the causal chain.** A moving object with live culpability striking
   an at-rest object *transfers* culpability to it. You threw a parcel, it hit the shelf, the
   shelf fell on the anvil, the anvil broke the floor — the floor's damage is yours.
3. **It never expires.** The last toucher owns an object until another actor overwrites
   them. There is no window, no timer and no per-event-kind table — this is the cheapest of
   the four options considered and it has the fewest moving parts.
4. **It never transfers to a *held* object.** Otherwise you launder blame by handing someone
   a live problem — which the design *does* want to be possible physically, but which should
   attribute to whoever started it.

`TouchedAtShiftTime` is therefore recorded for the report and for debugging, **not** for
expiry. Keep it: "you touched this nine minutes ago" is useful when a player disputes an
attribution.

**Where `UNATTRIBUTED` comes from, given rule 3.** Vision §7 requires the label to appear on
the report, and a never-expiring culpability nearly eliminates it. It survives as a narrower
but genuine category: **events on objects no player has ever touched.** The belt jamming
itself, structural settling, wrongness-caused damage, a parcel that arrived already broken —
the facility's own damage, which is exactly what an indifferent institution would file as
nobody's fault. `ActorRef.Unattributed` is the initial value of every object's `LastToucher`,
so this needs no extra mechanism.

**The recorded risk:** stale attribution. You touch a box in shift-minute 2, it falls in
minute 11, and it is still yours. This is partly self-limiting — objects that cause events
are objects being handled, and a busy floor re-touches things constantly — but if playtests
report it reading as a bug rather than a joke, the fix is an expiry window and the mechanism
above is where it goes.

All four rules are pure functions over collision events, so all four are L1 unit tests.
`ShiftLedger` then just accumulates:

```csharp
public readonly record struct LedgerEntry(EventKind Kind, ActorRef Actor, ParcelId? Parcel, float Amount);
```

The report (E8) is a `GroupBy` over that list. Vision §7 calls it the highest
value-per-hour feature in the product; under this design it is close to free, because the
attribution was plumbed from the start rather than retrofitted. **Retrofitting attribution is
what makes it expensive** — that is the reason this section exists this early in a document
whose E8 sits in Tier 3.

### 4.7 Wrongness

A float per stint, climbing per shift, gating a mutation table (vision §11). Sampling is
host-side and seeded.

**Clients receive the resolved mutation id list, not the seed.** Sending the seed would be
cheaper and lets clients derive the same set — and it would be wrong, because mutations
change *level geometry*, which physics and navigation depend on. A table-version mismatch
would desynchronise the building itself. So: the host samples, sends
`ApplyMutations(ids[])` at shift start, and records the seed in the log for reproduction.

Mutations must be **specifically postal** (vision §11). That is a content constraint, not an
architectural one, but it belongs in the table's authoring schema so a reviewer can enforce
it — see E13.

---

## 5. The parcel: data and body

Vision §9's framing — *a data carrier with a physical body* — is an architectural
instruction, and this is the section that implements it.

### 5.1 Identity outlives the node

```csharp
public readonly record struct ParcelId(uint Value);
```

Host-assigned, stable for the parcel's entire life, and **it survives the Godot node being
freed.** `ParcelRegistry` maps `ParcelId → ParcelRecord` in the domain. The node is a *view*
of a record.

This matters concretely: a parcel enters a pneumatic tube, its body despawns (§3.4), and it
reappears three rooms away. Same `ParcelId`, same manifest, same tamper state, same
culpability — a new node. Any design that stores parcel state on the node loses all of that,
and loses it silently at exactly the moment the report needs it.

### 5.2 Spawning

`MultiplayerSpawner` with a **custom spawn function**, because clients need enough
information to build a box that looks right:

```csharp
// spawn arg — deliberately not the whole record. Clients get no manifest data
// they haven't scanned (§5.3).
private record ParcelSpawnArgs(uint Id, byte Archetype, byte Size, byte Condition);
```

The manifest is *not* in the spawn payload. That is not an optimisation.

### 5.3 Asymmetric information is enforced by replication

Vision §8 makes proximity voice load-bearing by splitting information across four physical
posts. The temptation is to implement that as UI — show the manifest only when you are near
the scan desk. **That is a lie the client can trivially see through**, and worse, it means
every client already holds every manifest.

Instead: **the host does not send a manifest to a client that has not scanned it.**

| Post | Client holds | Must ask for |
| :-- | :-- | :-- |
| Intake dock | Physical state, volume | Destination |
| Scan desk | Manifest, declaration — *after scanning* | What the box physically is |
| Routing chart | `PolicyState`, destination → chute | The destination code |
| Chute floor | Chute state, jams | Which chute, and stamp state |

The information asymmetry is therefore a property of the network layer, which makes it
real, cheaper (less data on the wire), and impossible to defeat with an overlay.

### 5.4 Openable parcels

Vision §9 keeps openable parcels and defers only the dead-letter loop (E10) that depends on
them. Architecturally, opening is:

- A **host-validated intent** (`RequestOpen`), never optimistic — it is irreversible and it
  is a policy violation.
- A **tamper state transition** on the record, which is permanent and appears on the report.
- The point where `DeclaredContents` and `ActualContents` can be compared, which is the
  entire mechanical payload: contraband, mismatch, escape, breakage discovered later.

Keep `ActualContents` on the record from authoring time, replicated to nobody until the box
is open. The declaration/reality gap is the game's thesis in miniature (vision §9) and it
costs one extra field.

---

## 6. Presentation layer

### 6.1 The controller is tight; the world is hostile

Vision §3.1's distinction is the highest-risk feel requirement in the build. Two rules:

- **Input never waits for the network.** Look, move, and hand animation are local and
  immediate on all four machines, always.
- **Weight is expressed through the object, not the input.** A heavy parcel is heavy because
  Jolt says its mass is high and the joint is compliant — never because the controller
  slowed down or the camera got sluggish. Input damping to simulate weight is the specific
  mistake that makes the game read as broken. It is banned.

Godot 4.7's new IK framework (`TwoBoneIK3D`, `FABRIK3D`, `CCDIK3D`) is the cheap path to
hands that visibly reach the box they are holding, including the two-person carry. Use it
before writing procedural arm code (AGENTS.md rung 3).

### 6.2 Autoload budget

**Four autoloads, and that is the list:** `SessionRoot`, `AudioDirector`, `SettingsService`,
`SaveService`. Each one added is global mutable state that makes the suite harder to write
and startup order more fragile. The PA system is *not* an autoload — it is a node under
`AudioDirector` that reads replicated wrongness state.

### 6.3 Signals versus C# events

Godot signals across the node boundary; C# events within Domain. Domain must not know what a
signal is.

### 6.4 Object pooling

Parcels are the highest-churn object in the game and the belt never stops. **Pool them.**
`ParcelId` making identity independent of the node (§5.1) is what makes pooling safe — a
recycled node picks up a new record and carries nothing over.

### 6.5 Voice

Vision §4 makes voice **load-bearing**, so it is architecture rather than a feature, and it
is deliberately **not** built on Steam.

```
capture   AudioStreamMicrophone bus → AudioEffectCapture   (Godot native)
encode    Opus @ ~24 kbps mono, pure C# — no native binary
route     IGameTransport (§3.5) — the same abstraction gameplay uses
decode    Opus → AudioStreamGenerator on a positional AudioStreamPlayer3D
```

Three consequences worth stating, because they are the reason for the choice:

1. **Voice does not depend on the GodotSteam C# path.** Open item 1 is the project's largest
   risk, and E12 already rides on it. Putting E7 there too would mean one fragile dependency
   could take out both the social layer and the session layer. It now cannot.
2. **Voice works in development, in CI and under `LatencyPeer`.** A Steam-only voice path
   would be untestable in every environment except a real Steam build, and vision §8's whole
   design rests on voice working.
3. **A pure-C# Opus encoder is the project's one new runtime dependency.** It must be
   managed code with no native binary, so all three desktop export targets keep working
   without per-platform builds. Raw PCM is not an option — 16-bit mono at 24 kHz is
   ~48 KB/s *per speaker*, which alone exceeds the entire gameplay budget in §8.

Voice is relayed through the host, because Godot's high-level multiplayer is a star topology.
That costs ~27 KB/s of host upstream at four speakers, which §8 budgets for explicitly.

```csharp
// ponytail: voice is relayed via the host like every other packet.
// Ceiling: host upstream carries 3 voice streams per client, ~27 KB/s at full
// four-way chatter, on top of gameplay replication.
// Upgrade: we own the codec and the routing, so voice can move to direct peer
// sockets and off the host's budget entirely — Steam P2P or ENet both allow it.
// Do this only if §8's measured budget is actually the constraint.
```

Positional playback is an `AudioStreamPlayer3D`, so proximity falloff is the engine's job and
not ours. Radios (E7) are the same decoded stream on a non-positional player.

---

## 7. Data-driven content

E13's thesis (vision §13) is that this epic determines whether the game is alive twelve
months after launch, and that it should start *early and badly* rather than late and well.
Architecturally that means: **every content type below is authored as data from its first
day, even when there are only two of them.**

| Content | Authored as | Validated by |
| :-- | :-- | :-- |
| Parcel archetypes | `.tres` resource | `ContentTool` — mass/size sane, contents resolvable |
| Manifest / address grammar | data table | address parses to a routable destination |
| Routing policy | `.tres` | every destination maps to exactly one chute |
| Room mutations | `.tres` | threshold in 1–10; **postal, not generic liminal** |
| PA lines | data table | threshold gated; name tokens resolve |
| Signage | data table | referenced destination exists |
| Hazards | `.tres` | — |

`.tres` files are **not** in Git LFS — they must stay diffable. Binary art and audio are.

`ContentTool validate` runs in CI. A content file that breaks an invariant **fails the
build**, which is the only mechanism that keeps a data pipeline honest once people are
authoring under deadline.

---

## 8. Performance budgets

Provisional, to be replaced by measurement. Stated as numbers so a regression is arguable.

| Budget | Target | Why |
| :-- | :-- | :-- |
| Host upstream — **gameplay** | **< 60 KB/s** | Domestic upload; Steam relay overhead on top |
| Host upstream — **voice** | **< 30 KB/s** | §6.5, four speakers relayed to three clients |
| Host upstream — **total** | **< 90 KB/s** | Comfortable on any connection that can host at all |
| Awake parcel bodies | **≤ 40** | Jolt is comfortable well past this; the wire is not |
| Total live parcel records | ~200 | Railed and sleeping cost nothing (§3.4) |
| L1 suite | **< 5 s** | A slow suite is an unrun suite |
| Report readable | **< 4 s at stream resolution** | Vision §7, verbatim |
| Grab → visible hand motion | **0 frames of network wait** | §3.3 |

The two upstream budgets are tracked separately on purpose. Voice is the one that can be
moved off the host entirely (§6.5's upgrade path) without touching gameplay, so conflating
them would hide the cheapest available fix if the total is ever exceeded.

---

## 9. Persistence

Deliberately thin. Vision §5: cosmetics, unlocked tools, and the small unlock ladder persist.
Facility layout, wrongness, quota, and mail volume do not.

**One addition from the termination decision:** the **employee record** persists. Being fired
costs identity rather than progress (epics E5), so every terminated employee is filed
permanently with their final stats and appears on the Former Personnel wall:

```csharp
public sealed record EmployeeRecord(
    string Name,              // derived from the player's Steam persona
    int ShiftsSurvived,
    EndOfService Fate,        // Terminated | Deceased
    LedgerSummary Career);    // rolled up from ShiftLedger at termination
```

This is append-only, is derived entirely from data `ShiftLedger` already holds, and is the
only thing termination writes. It is a **trophy case of failure**, not a progression store —
nothing on it affects a future stint, which is what keeps it inside vision §3.6.

Two files, then: the profile (cosmetics, unlocks, ladder, employee records) and the local
shift log (E20). Both atomic write (temp + move), both with a version field from day one and
an **empty migration chain**. The house project's experience is worth copying exactly:
shipping the chain empty at version 1 meant the first real migration was an append to a
tested mechanism rather than a system written under release pressure.

The shift log is **local-only and never uploaded.** E20's decision is that players can export
it on request, which means this file is the entire telemetry pipeline and the product has no
server component of any kind.

No accounts, no cloud, no server-side profile — guardrails §16.

---

## 10. Testing

### 10.1 The pyramid

| Level | Tool | Scope | Speed |
| :-- | :-- | :-- | :-- |
| **L1** | xUnit, no engine | Domain: routing, quota, wrongness, culpability, ledger, policy, PA gating | < 5 s |
| **L2** | GdUnit4, engine | Grab joint, conveyor rails, spawner, pooling | seconds |
| **L3** | Headless multi-peer | Host + 3 clients, real transport | minutes |

### 10.2 What must be L1-tested

Everything the report depends on. `RoutingOutcome.Evaluate` against a policy matrix;
culpability propagation through a synthetic collision chain; ledger aggregation;
wrongness sampling staying inside its threshold band; PA line gating; quota arithmetic across
a stint.

If §1's boundary is doing its job, this list is also the list of things that make the game
distinctive. That is the payoff, and it is the check on whether the boundary is still real.

### 10.3 What cannot be tested and must be playtested

Whether a parcel feels heavy. Whether awkward reads as funny or as broken. Whether four
players separate to four posts or clump into a blob (vision §8's stated risk). Whether
inspection survives four players (open question 3). **No test in this repo can answer any of
these**, which is why E19 exists as an epic rather than as a task.

### 10.4 The network smoke test is the important one

`Dlo.Net.Tests` boots a headless host and three headless clients over `EnetTransport` and
asserts:

- A parcel's `ParcelId` survives tube transit and node recycling.
- Two clients grabbing the same parcel in the same frame resolves to exactly one holder,
  and the loser's client releases cleanly.
- The ledger on the host and the report every client renders **agree exactly**.
- A client that has not scanned a parcel **never receives its manifest** (§5.3) — this one
  is an anti-assertion and it is the easiest to regress.

This suite is the difference between "worked in the editor" and "worked in the playtest".

### 10.5 The architecture test

```csharp
[Fact]
public void Domain_does_not_reference_Godot() =>
    Assert.DoesNotContain("GodotSharp",
        typeof(ParcelRecord).Assembly.GetReferencedAssemblies().Select(a => a.Name));
```

Plus a review-checklist grep for a second `new ShiftDirector()` (§3.2), which no test catches.

### 10.6 CI

Fresh clone builds, L1 and L2 run, `ContentTool validate` runs, architecture test runs. L3
runs on merge to main, not per-push, because it is minutes rather than seconds.

---

## 11. Open technical items

Ordered by when the answer is needed, not by size. **All four remaining items are empirical —
they are answered by measurement or a spike, not by a decision.**

| # | Item | Blocks | Needed by |
| ---: | :-- | :-- | :-- |
| 1 | **Prove the GodotSteam C# path** — bindings fork, `SteamMultiplayerPeer` without channels, at 4 peers (§3.5). **Blast radius reduced:** E7 no longer depends on this. **Cost corrected 2026-08-25:** it needs no paid app id — 480 (Spacewar) initialises the real API — but it does need four Steam accounts on four machines, and two peers on two machines answers most of it | E12, shipping | **E0, week one.** Not negotiable |
| 2 | Measure real replication cost against §8's budget with a full belt | §3.4's three-class design | Before E4 conveyors are considered done |
| 3 | Jolt joint stability for two-person carry of a heavy body | E1 | First E1 spike |
| 5 | Select the pure-C# Opus package — managed only, no native binary, all three desktop targets (§6.5) | E7 | With E7's first story |

*Item 4 — the headless multi-peer harness — is answered and has moved to the table below. The
remaining numbers are left where they are, because the stories document cites them by number.*

**Resolved in v0.2** — kept visible so the reasoning is not re-litigated:

| Was | Resolution |
| :-- | :-- |
| Steam Voice sample rate and CPU cost | **Moot.** Voice is not built on Steam (§6.5) |
| Does proximity voice need Steam at all? | **No.** Native capture + pure-C# Opus over `IGameTransport` |
| `CulpabilityWindow` per event kind | **No window.** Culpability never expires (§4.6) |
| Shift length | **8–12 min × 3–6 shifts**, provisional, curve is a data file, Gate 2 refines |
| Target framework, now that the .NET 10 SDK is installed | **`net10.0` everywhere, overriding the `net8.0` Godot generates.** Measured, not assumed: it builds, Godot's own build path builds it, the editor leaves an already-set value alone, and it runs. The runtime is `.NET 10` at either TFM, so `net8.0` would only mean compiling against an older BCL than the one executing — and one that leaves support in November 2026 (§1.4). **Held in `Directory.Build.props` for five projects and in `Dlo.Game.csproj` for the sixth**, because Godot re-adds a missing TFM line and the project body beats the props file (§1.4, E14-03). Export is the one leg still unverified, and E18-01 verifies it |
| Godot headless multi-peer harness: 4 processes or 4 `SceneTree`s? (was item 4) | **Four processes**, built and measured 2026-08-24 (E0-08, E0-09). Cost is not what decides it: a trivial four-peer connect-and-exchange took **435 ms** in one process against **663 ms** in four, both far inside §10.1's "minutes". What decides it is that **one Godot process has exactly one physics world** — two bodies in two sibling subtrees shoved each other apart, measured — so four in-process peers would hold four copies of every parcel in one simulation. §10.4's physics-bearing assertions (grab contention, identity through tube transit, ledger agreement) would all have been quietly wrong. Statics, autoloads and `ProjectSettings` are shared for the same reason, and host authority is a claim about separate machines. The harness lives in `tests/Dlo.Net.Tests`, which is its own Godot project so that no harness-only code ships; the whole finding is on `FourPeerSession` |
| Does `Dlo.Domain` stay on `netstandard2.1`? | **No — `net10.0`, same as everything else.** It had no consumer outside this repo, and it was what forced the `IsExternalInit` declaration for `readonly record struct`. That workaround is deleted rather than documented |

---

## 12. Decisions recorded elsewhere

Locked product decisions — player count, run structure, antagonist, facility persistence —
live in vision §4 and are not restated here. Where a technical choice follows from one, this
document cites it rather than re-arguing it.

ADRs to be written as the decisions are implemented rather than up front:

- **ADR 0001** — hybrid domain boundary: engine-free rules, engine-owned bodies (§1)
- **ADR 0002** — host authority with one optimistic exception for grab (§3.3)
- **ADR 0003** — three-class parcel replication (§3.4)
- **ADR 0004** — transport abstraction with ENet development fallback (§3.5)
- **ADR 0005** — voice is native capture plus pure-C# Opus, not Steam Voice (§6.5). Record
  the risk-concentration argument, not just the choice — it is the whole reason
- **ADR 0006** — culpability never expires (§4.6), and what `UNATTRIBUTED` therefore means

---

*Product intent, pillars and scope: see [the vision](dead-letter-office-vision.md). Work
breakdown and sequencing: see [the epics](dead-letter-office-epics.md).*
