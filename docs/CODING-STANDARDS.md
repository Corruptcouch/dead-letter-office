# Dead Letter Office — Coding Standards

**Status:** normative · Godot 4.7.2 · .NET 10 SDK · C#
**Audience:** anyone, human or agent, adding code to this repo.

Where this document and habit disagree, this wins. Where this document and the
[architecture](dead-letter-office-architecture.md) disagree about a *mechanism*, the
architecture wins and this document is defective — fix it here rather than working around it.

Read alongside:

- [`AGENTS.md`](../AGENTS.md) — the laziness ladder. Still applies: the best code is the code
  never written. This document says *how* the code you do write should look.
- [the vision](dead-letter-office-vision.md) — normative for **what the game is**.
- [the architecture](dead-letter-office-architecture.md) — normative for **how it is built**.
- [the epics](dead-letter-office-epics.md) — normative for **in what order**, and the source of
  Definition of Ready and Definition of Done.

**This document does not re-argue the architecture.** Nearly every rule below is one line and a
citation. The reasoning lives in the cited section, is longer than the rule, and is worth
reading once before you dispute the rule.

### Where these rules come from

The house project's standards document could claim every rule was scar tissue — its code had
already broken each one, and each rule named the failure it prevented. **This repo has no code
yet**, so that claim would be a lie here, and a rule presented as a postmortem that never
happened gets ignored the first time it is inconvenient. So rules carry their origin:

- **(house)** — a failure the house project actually had. Inherited because the team and the
  toolchain are the same. The story is the justification.
- **(arch)** — derived from a decision in the architecture or the vision. Not yet scar tissue;
  the citation is the argument.

When a rule earns its own postmortem here, replace its marker with the story. That is an
upgrade, and this section is why there is room for one.

---

## 0. The non-negotiable

Architecture §1, stated as something you can apply to the line you are writing:

> **A fact about the shift becomes true only when the host's domain layer says so.
> Physics proposes. The domain disposes.**

A parcel is not misrouted because it fell down chute 3. It is misrouted because the host's
`RoutingRules.Evaluate` said parcel 41 belonged in chute 7. The rigid body is how it *got*
there and what it *looked like* getting there. It is not the reason anything is true.

`Dlo.Domain` is pure C# and the dependency arrow never reverses. This is enforced by a test,
not by discipline — discipline fails at 11pm (arch §10.5).

Hard rules inside `src/Dlo.Domain/`:

| Rule | Why |
| :-- | :-- |
| No `using Godot`. No Godot type in any signature — **including `Vector3`.** | Domain has its own `Vec3`. Converting at the boundary is annoying exactly once per event kind, and it is what keeps the L1 suite engine-free. (arch §2) |
| No `GD.Randi()`, no `Random.Shared`, no static RNG. Inject `IRandom`. | So a bug report can carry a seed. "The facility generated a loading dock inside the break room" is only fixable if you can regenerate that facility. (arch §4.2) |
| No wall clock. No `DateTime.Now`, no `Time.GetTicksMsec()`, no delta time. | The shift clock is host-owned wall-clock seconds, passed **in**. Domain reads shift time as a parameter, never as ambient state. (arch §4.3) |
| No node, `NodePath`, scene, or signal. | Domain must not know what a signal is (arch §6.3). A node is a *view* of a record (arch §5.1), and a record that knows about its view cannot outlive it — outliving it is the whole point. |
| No file or network I/O. | Domain is a transformation layer. Persistence belongs to the Game layer (arch §9), transport to `IGameTransport` (arch §3.5). |
| No `static` mutable state, anywhere, for any reason. | The L1 suite runs in parallel, and a static field is how one test starts depending on another. It is also a second source of truth by definition. |

**And the rule that is deliberately *not* here.** The house standards make byte-identical
deterministic output *the* non-negotiable. **That rule does not apply to this project and must
not be copied in.** The vision's guardrails (§16) exclude deterministic multiplayer physics,
and arch §4.2 states we are explicitly not determinism-dependent. Floats are fine — wrongness
is one, and so is `TouchedAtShiftTime`. **Host authority is what keeps four peers agreeing
here** (arch §3.1), and `IRandom` earns its place so that a bug is reproducible, not so that a
replay is byte-identical. If you find yourself adding a determinism rule, you are solving a
problem this architecture already solved a different way.

---

## 1. Toolchain and language baseline

**Godot 4.7.2-stable-mono**, .NET 10 SDK, C#, and **`net10.0` for every project** (epics E14).

**Godot generates `net8.0` and we override it, deliberately.** Godot 4.7.2's project template
hardcodes `net8.0` and GodotSharp ships against it, but that is Godot's floor, not a ceiling —
`net10.0` was measured working end to end: it builds, Godot's own `--build-solutions` builds it,
the editor does not rewrite a setting that already has a value, and it runs. The runtime is
`.NET 10` either way, since Godot's host rolls forward to the newest major installed (arch §1.4).
Targeting `net8.0` would mean compiling against an older BCL than the one actually executing, and
adopting a framework that leaves support in November 2026 — before this project ships.

**Where the override lives is not a style choice.** `Directory.Build.props` holds it for the five
hand-authored projects; `Dlo.Game.csproj` holds its own copy because Godot re-adds a *missing*
TFM line as `net8.0` and the project body beats the props file (§2, arch §1.4).

**The editor version is pinned.** On the development machine it lives at
`D:\work\Godot\Godot_v4.7.2-stable_mono_win64\`; **that path is machine-local and belongs in the
README, not in a rule** — each machine records its own (epics E14).

- **Nullable enabled everywhere. Warnings-as-errors in `Dlo.Domain` only** — the Game layer
  compiles Godot's noisy generated glue, and failing the build on it means nobody can build
  (arch §2).
- **C# standard naming, matching Godot's own PascalCase C# API.** `_camelCase` private fields.
  Do not carry `snake_case` across from GDScript examples, including for `[Export]` fields.
- **File-scoped namespaces. No `#region`.** A file that needs regions needs splitting (§2).
- **XML docs on every public Domain type and member.** Domain is the API the Game layer is
  written against, and there is nowhere else that documents it. Definition of Done requires it.
- **Formatting is `.editorconfig`'s job, not review's.** If a formatting question reaches a
  human, the `.editorconfig` is missing a rule. Fix the file, not the PR.
- **`dotnet format --verify-no-changes` gates CI, and its autofix is not always right.** IDE0031
  ("null check can be simplified") on `if (x is not null) { x.Prop = v; }` fixes to
  `x?.Prop = v`, **which does not compile** — a property cannot be assigned through a
  null-conditional. Rewrite as a guard clause instead; the analyser accepts it and the code
  builds. Run the build after `dotnet format`, not just the formatter. (E1-07)
- **`using Godot;` shadows BCL type names, and the compiler reports it as your mistake.**
  `Environment` is the one that bites first — `Godot.Environment` is the 3D world environment
  resource, so bare `Environment.Version` is `CS0104: ambiguous reference`. Spell out
  `System.Environment`. Expect the same from any short BCL name Godot also uses; reach for the
  `System.` prefix rather than deleting `using Godot;`. (E14-03)
- `dotnet build` and the test suites are the gates. There is no CLI warning gate in the Game
  layer, so the enforced gate there is the suite — do not let the IDE warning list accumulate
  noise, because that is where a real warning goes to hide.

*(The `IsExternalInit` workaround that used to live here is gone with `netstandard2.1`. `net10.0`
supplies the attribute, so `readonly record struct` just compiles. If you find that hack in a
tutorial, you do not need it.)*

### 1.1 The wrong idiom is the one most likely to be suggested

Godot 3 predates the current shape of the C# API, and search results, tutorials and language
models have seen far more Godot 3 than 4.7. These are the ones to reject on sight:

| Do not write | Write |
| :-- | :-- |
| `Godot.Object` | `GodotObject` |
| `scene.Instance()` | `scene.Instantiate()` |
| `Connect("pressed", this, nameof(OnPressed))` | `button.Pressed += OnPressed` |
| `GetNode("Path") as Thing` | `GetNode<Thing>("Path")` |
| `PoolStringArray`, `PoolByteArray` | `PackedStringArray`, `PackedByteArray` |
| `[Signal] delegate void Thing();` | `[Signal] delegate void ThingEventHandler();` |
| `Godot.Collections.Dictionary` as a data payload | a typed record; Godot collections only at the engine boundary |
| `_Process` for anything physical | `_PhysicsProcess` |
| `parcel.QueueFree()` | return it to the pool (§10) |
| `export var thing`, `onready var thing` | `[Export] public Thing Thing`, resolved in code |

---

## 2. One file, one responsibility

The layout (arch §1.3), and what each project is allowed to see:

| Project | Holds | May reference |
| :-- | :-- | :-- |
| `src/Dlo.Domain/` | Parcel records, manifests, routing rules, policy, shift clock, quota, wrongness table, culpability, the ledger, PA gating | the BCL, and nothing else |
| `src/Dlo.Game/` | Controller, grab joints, parcel bodies, conveyors, chutes, doors, VFX, PA audio, HUD, transport, replication | Godot + Domain |
| `tests/Dlo.Domain.Tests/` | **L1** — xUnit, no engine | Domain |
| `tests/Dlo.Game.Tests/` | **L2** — GdUnit4, engine required | Game |
| `tests/Dlo.Net.Tests/` | **L3** — headless host + 3 clients over ENet | Game |
| `tools/Dlo.ContentTool/` | Content validation, placeholder generation | Domain |

Rules:

- **The test for a new file:** can you state its job in one sentence with no "and"? If not, it
  is two files. (house — a 529-line god object holding eleven jobs, where nothing could be
  tested or changed in isolation.)
- One public type per file, named for the file. Nested types only where the outer type is the
  only legitimate user.
- **When two modules need the same type, extract it — do not reach across.** (house: two
  modules referencing each other's inner classes. There it was a global class-registry failure;
  in C# it is a project-reference cycle MSBuild simply refuses. Same fix either way.)
- **Custom MSBuild config lives in `Directory.Build.props`, never in `Dlo.Game.csproj`.** Godot
  regenerates that file: project references survive, custom properties do not. (house,
  arch §1.4)
- **The one exception is `<TargetFramework>`, and it is not optional.** Godot re-adds that line
  as `net8.0` whenever it is missing, and MSBuild imports `Directory.Build.props` *before* the
  project body — so the returned `net8.0` would win. `Dlo.Game.csproj` carries an explicit
  `net10.0`; everything else takes it from the props file. Do not "tidy" it away (arch §1.4,
  E14-03).
- **`project/solution_directory="../.."` stays in `project.godot`.** Without it the exporter
  looks for the solution beside `project.godot` and refuses every C# source during export.
  (house, arch §1.4)
- **One construction site for domain systems** — `SessionRoot._Ready`, behind one
  `Multiplayer.IsServer()` branch (arch §3.2). Clients construct nothing, so there is no
  client-side `ShiftDirector` to drift, and the systems are passed in rather than built
  internally so L1 can substitute stubs. No architecture test catches a second construction
  site; the review checklist greps for it.

---

## 3. Types, nullability, composition

- **No `Dictionary<string, object>`, no `Godot.Collections.Dictionary`, and no loose blob as a
  data payload.** Define a record. (house: untyped dictionary payloads everywhere, so a typo in
  a field name was a silent runtime default instead of a compile error, and a weapon quietly
  stopped working.)
- **A dictionary is fine as an *index*** (`ParcelId → ParcelRecord`), never as a *record*.
- **Composition and data, not inheritance** (arch §4.1). No `FragileParcel : Parcel : Entity`.
  Adding a parcel archetype, a hazard or a mutation must never require a new class — that is the
  concrete form of open/closed here, and it is what makes E13 possible at all.
- **Model category differences with data or with distinct component records**, not with optional
  fields that most instances leave null. Nothing should have to read a fragility rating off a
  hazard and hope the default is sensible. (house)
- **`readonly record struct` for every identifier** — `ParcelId`, `ChuteId`, `ActorRef` — never
  a bare `uint` or `string`. Two bare `uint` ids are assignment-compatible, the compiler will
  not save you, and the report is where you find out.
- **Nullable annotations are load-bearing in Domain, where they are errors.** A `!` is a claim
  you are making to the next reader. If you cannot justify it in a comment, restructure.
- `var` where the type is obvious from the right-hand side; the explicit type where it is not.

---

## 4. The network rules

The section most likely to be violated by code that works perfectly on one machine.

> **Clients send intent. The host sends outcomes. A client never sends a fact.** (arch §3.1)

`RequestGrab`, `RequestStamp`, `RequestOpen` — never `IWasMisrouted`, never `ITookDamage`.

**Set every RPC's mode deliberately.** The C# defaults are `[Rpc(RpcMode.Authority,
CallLocal = false, TransferMode = Reliable, TransferChannel = 0)]`, and two of them are traps
in this game (arch §3.1):

- **`Reliable` carries, in the engine's own words, "a significant performance penalty."** It is
  right for *decisions* — a stamp landed, a parcel was destroyed, the shift ended — and wrong
  for *streams*. Anything positional is `Unreliable` or `UnreliableOrdered`.
- **`CallLocal = false` means the host does not run its own RPC.** On an intent RPC the host
  must also honour when the host presses the button, that is a silent no-op on one machine out
  of four — and it is the host's, so it presents as "the game is broken for whoever hosts."

**The three replication classes are not advisory** (arch §3.4). `Railed` sends
`(beltId, distanceAlong, lane)` once on entry and then nothing; `Dynamic` sends transforms;
`Sleeping` sends nothing. **A belt parcel in `Dynamic` is a bug against the design keystone** —
the belt never stops (vision §2), so the common case has to cost nothing. Set
`replication_interval` per class, not globally.

**Grab is the only optimistic path in the build** (arch §3.3). It acts before the host permits
it, because a grab that waits 80ms feels broken and vision §3.1 is emphatic that the parcel is
the problem, not the input. A mispredicted grab reads as a teammate yanking the box away — the
comedy engine firing for free. **Do not generalise it.** Stamping, opening and incinerating are
host-validated, because a rollback there would un-decide something the ledger already recorded.

**The real joint only ever exists on the host.** Clients hold a visual-only attachment.
Two-player carry is two host-owned joints on one body, and the domain — not the joint — decides
which parcels exceed one-person capacity (arch §3.3).

**Asymmetric information is a property of the network layer, not of the UI** (arch §5.3). The
host does not send a manifest to a client that has not scanned it. Hiding it in the UI while
every client already holds the data is a lie the client can trivially see through, and it makes
vision §8's entire design cosmetic.

**All transport goes through `IGameTransport`** (arch §3.5). No Steam type outside
`SteamTransport`; the suites run on `EnetTransport` forever. The GodotSteam C# path is the least
mature dependency in the plan, and it is the one thing that must not become load-bearing in a
thousand places.

---

## 5. Identity, state, and who owns what

- **`ParcelId` is the identity; the node is a view** (arch §5.1). Host-assigned, stable for the
  parcel's whole life, and it survives the Godot node being freed — a parcel enters a tube, its
  body despawns, and it comes back three rooms away with the same manifest, tamper state and
  culpability.
- **No gameplay state on the node.** If a field would be wrong after pooling recycles the node,
  it belongs on the record. Storing it on the node loses it silently, at exactly the moment the
  report needs it (arch §5.1) — and it is what makes pooling unsafe (arch §6.4).
- **`ActorRef.Unattributed` means "no player has ever touched this."** It is the facility's own
  damage — the belt jamming itself, structural settling, a parcel that arrived broken (arch
  §4.6). **Never use it as a fallback for "we did not plumb attribution here."** Vision §7
  requires unattributable events to be *labelled*, and a lazy `Unattributed` turns a real
  category into a shrug.
- **`TouchedAtShiftTime` is for the report and for debugging, never for expiry.** Culpability
  does not expire; the last toucher owns an object until another actor overwrites them (arch
  §4.6, ADR 0006). Code that compares that field against a threshold is reintroducing a decision
  that was deliberately deleted, along with the constant that named it.
- **Culpability propagates along the causal chain and never transfers to a *held* object** (arch
  §4.6, rules 2 and 4). Otherwise you launder blame by handing someone a live problem. Both
  rules are pure functions over collision events, so both are L1 tests.
- **The player is not told whether a parcel was routed correctly** (arch §4.4). Misrouting is
  recorded silently and revealed at the whistle. A live "wrong chute!" indicator is a feature
  request to refuse, not a UX improvement — the delayed reveal is the blame engine's ammunition.
- **Wrongness sends resolved mutation ids, never the seed** (arch §4.7). Mutations change level
  geometry, which physics and navigation depend on, so a table-version mismatch would
  desynchronise the building itself. The seed goes in the log for reproduction.
- **`PolicyState` is mutable and replicated on purpose** (arch §4.5). The bureaucracy landing a
  hit *is* a data change, so nothing may cache a routing answer across a policy update.

---

## 6. Naming follows the vision

The vision is the shared vocabulary. Code that renames its concepts forces every reader to
translate, and translation is where a rule gets subtly restated wrong.

- Use the document's word: parcel, manifest, stint, shift, quota, wrongness, culpability,
  policy, post, chute, rail, dead letter, termination.
- **Cite the section in the doc comment when implementing a stated rule:** `(Arch §3.4)`,
  `(Vision §7)`. When a document changes, the citations are how you find the code.
- **`Request*` for client→host intent; past tense or `*Resolved` for host→client outcomes** —
  `RequestGrab` / `GrabResolved` (arch §3.3). The convention is what makes an authority
  violation visible in review without reading the body.
- Booleans read as assertions: `IsRailed`, `HasBeenScanned`, `CanAcceptParcel`.
- Something stays private until a test or caller genuinely needs it. **If a test needs it, make
  it public and document why** — do not add `InternalsVisibleTo` to reach past the design.
  (house)

---

## 7. Comments explain why

The code says what it does. A comment that restates it is noise that goes stale.

```csharp
// No.
// set the last toucher
culpability = new Culpability(actor, shiftTime);
```

Write the things the code cannot say:

- **The reason a rule exists**, especially where the obvious change is wrong: why the manifest
  is not in the spawn payload, why the real joint is host-only, why `TouchedAtShiftTime` is
  never compared to anything.
- **A deliberate simplification, in the `ponytail:` form from `AGENTS.md`** — name the ceiling
  *and* the upgrade path. Arch §2 and §6.5 are the models. A `ponytail:` with only one half is
  a TODO in a costume, and Definition of Done rejects it.
- **A known divergence** from the vision or the architecture: what the code does instead, what
  that costs, and what implementing the stated version would require. Silent divergence is how
  a design document becomes fiction. (house)
- **Do not comment out code.** Delete it. Git remembers, and a commented-out block is a claim
  that someone intends to come back.

XML docs (`///`) are required on public Domain types and members (§1). Use `<see cref="..."/>`
so the references resolve and the Game layer gets working hover text.

**Length is part of the rule.** A doc comment is not the place to re-argue a design document, and
a file whose comments outweigh its code is a design document in the wrong file — it will drift
from the real one, and the reader who needs it will be reading the stale copy. Budget: a
`<summary>` is one or two sentences; a `<remarks>` is one short paragraph, and a second `<para>`
only when the first cannot carry it.

- **Cite, do not restate.** The vision, the architecture, the epics and the stories are the
  authorities on why. `(arch §3.4)` is the whole citation — the paragraph explaining arch §3.4
  belongs in arch §3.4. When code and a design doc disagree, one sentence of divergence plus a
  citation beats a retelling.
- **No project history in source.** Dated corrections, "blocked on, in order", what a future
  story will fill in, what a spike cost: that is the stories document's job. Code says what is
  true now. *(An audit of the shipped stories removed 240 comment lines. Two of them were false:
  `Main.cs` said E0-04 would replace it with `SessionRoot` — E0-04 shipped `SessionRoot` as an
  autoload beside it — and `IGameTransport` cited a recorded gap that was never written.)*
- **No doc that restates its own identifier.** `/// <summary>Move forward.</summary>` above
  `Forward = "move_forward"` is the noise this section opens by banning; a doc comment is not
  exempt from it.
- **Keep the note that stops a plausible wrong change.** The `-0.1f` downward bias that keeps
  `IsOnFloor()` true on a ramp, the clamp just inside ±90°, reading packet metadata before taking
  the packet, why `Sleeping` waits an hour. These are the comments worth having, and they are all
  one or two lines.

---

## 8. Every change leaves a runnable check

Three levels (arch §10.1), and **the level is part of the story, not an afterthought** — the
epics' Definition of Ready requires it stated up front:

| Level | Tool | Scope | Target |
| :-- | :-- | :-- | :-- |
| **L1** | xUnit, no engine | Routing, quota, wrongness, culpability, ledger, policy, PA gating | < 5 s |
| **L2** | GdUnit4, in engine | Grab joint, conveyor rails, spawner, pooling | seconds |
| **L3** | Headless multi-peer | Host + 3 clients over ENet | minutes |

The exact invocations live in the README, which E14 owns. If they are not there, that is an E14
defect — raise it rather than reinventing the command.

- **Everything the report depends on is L1** (arch §10.2): `RoutingOutcome.Evaluate` against a
  policy matrix, culpability propagation through a synthetic collision chain, ledger
  aggregation, wrongness sampling staying inside its threshold band, PA gating, quota arithmetic
  across a stint. **That list is also the list of things that make this game distinctive** — if
  it stops being L1-testable, §0's boundary has stopped being real.
- **Name the test after the behaviour**, not the method:
  `Misroute_is_not_revealed_until_the_whistle`, not `TestRouting`. The name is the
  specification; if you cannot name the behaviour, you do not know what you are asserting.
  (house)
- **Assert real values, not presence.** (house: a test asserted a map *contained* a key when the
  delivered amount was the thing that mattered — and the amount was zero.)
- **A test that cannot fail is worse than no test.** (house: a test asserted one of two tied
  candidates, so it was really asserting an RNG draw.) With `IRandom` injected there is no
  excuse — pass a stub and assert the branch you mean.
- **Do not chain assertions so the first failure hides the rest.** `Assert.True(a && b && c)`
  reports one useless fact. One assertion per fact. (house)
- **Prove it: break the code and watch the test go red.** Revert the fix, or hand-edit the bug
  back in, run the level, confirm the failure is the test you just wrote, then restore. It takes
  a minute and it is the only thing that distinguishes a test from a comment. (house)
- **The anti-assertions matter more than the assertions.** "A client that has not scanned a
  parcel never receives its manifest" (arch §10.4) is the easiest thing in this design to
  regress and it fails *silently* — nothing looks broken when a client knows too much. Write the
  negative test.
- **L3 asserts agreement, not behaviour** (arch §10.4): the host's ledger and the report every
  client renders agree exactly; two clients grabbing in the same frame resolve to exactly one
  holder and the loser releases cleanly; a `ParcelId` survives tube transit and node recycling.
  This suite is the difference between "worked in the editor" and "worked in the playtest."
- **The architecture test is not decoration** (arch §10.5). When it fails, the fix is never to
  relax it.
- **What cannot be tested must be playtested** (arch §10.3). Whether a parcel feels heavy,
  whether awkward reads as funny or as broken, whether four players separate to four posts or
  clump into a blob. **Do not write a test that pretends to answer one of these** — route it to
  E19 and a gate, which is why E19 is an epic rather than a task.
- Trivial one-liners need no test (`AGENTS.md`). Everything else does.

---

## 9. Content, data and persistence

- **Every content type is authored as data from its first day, even when there are only two of
  them** (arch §7, vision §13). A hazard, archetype, mutation, PA line or sign added in code is
  a defect against the epic that decides whether this game is alive twelve months after launch.
- **`ContentTool validate` runs in CI and a broken invariant fails the build** (arch §7). A
  validator nobody enforces is a comment, so every new content type ships its validation rule in
  the same PR.
- **One schema per concept.** (house: a content table grew two shapes with a fallback bridging
  them, so every reader had to handle both, forever.)
- **Unknown ids degrade; they do not crash.** (house) A lookup miss makes the thing inert and
  logs it, and the shift continues — content data outlives the table that described it.
- **Validate at the trust boundary** — content load, save load, and every `Request*` RPC. Reject
  the whole malformed unit rather than partially applying it. (house: a wrong-length array was
  partially applied, and the result was a simulation of a board nobody had described.) Not
  lazy-able (`AGENTS.md`).
- **`.tres` files stay out of Git LFS — they must stay diffable.** Binary art and audio go in
  (epics E14).
- **Generated placeholders are build outputs and are not committed** (epics E17). The house
  project learned this expensively twice over: committed placeholders went through LFS, so a
  clone that skipped `git lfs pull` booted invisible, and the bytes were toolchain-coupled — a
  .NET bump rewrote every PNG without changing a pixel.
- **Persistence is two files, both atomic write (temp + move), both with a version field from
  day one and an empty migration chain** (arch §9). Shipping the chain empty at version 1 is why
  the house project's first real migration was an append to a tested mechanism instead of a
  system written under release pressure. Copy that exactly.
- **Serialised payloads carry no engine type.** Same rule as §0, one layer out: a save file or a
  wire payload holds `{ "x": 2, "y": 2 }`, never a `Vector3`.
- **The shift log is local-only and there is no upload path at all** (arch §9, epics E20). Do not
  add a request to a host we control — there is no such host, and consequently no consent flow,
  no privacy policy and no GDPR surface. The absence *is* the design; one endpoint would be the
  only server infrastructure in the entire product.

---

## 10. Presentation layer

- **Input never waits for the network** (arch §6.1). Look, move and hand animation are local and
  immediate on all four machines, always.
- **Input damping to simulate weight is banned** (arch §6.1). A heavy parcel is heavy because
  Jolt says its mass is high and the joint is compliant. Slowing the controller or the camera is
  the specific mistake that makes the game read as broken rather than funny (vision §3.1), and
  it is the highest-risk feel requirement in the build.
- **Four autoloads, and that is the list:** `SessionRoot`, `AudioDirector`, `SettingsService`,
  `SaveService` (arch §6.2). Each one is global mutable state that makes startup order fragile
  and the suite harder to write. The PA system is a node under `AudioDirector`, not an autoload.
  A fifth is an architecture change, not a PR.
- **Godot signals across the node boundary; C# events inside Domain** (arch §6.3). Domain must
  not know what a signal is.
- **Pool parcels; do not `QueueFree` them** (arch §6.4). They are the highest-churn object in the
  game and the belt never stops. A recycled node picks up a new record and carries nothing over —
  which holds only if §5's no-state-on-the-node rule holds.
- **Reach for the engine before writing the code** (`AGENTS.md` rung 3). Godot 4.7's IK framework
  (`TwoBoneIK3D`, `FABRIK3D`, `CCDIK3D`) before procedural arm code; `AudioStreamPlayer3D`
  falloff before hand-rolled proximity attenuation.
- **Set `physics/3d/physics_engine="Jolt Physics"` explicitly in `project.godot`** (arch §1.4).
  A fresh 4.7.2 project leaves it at `DEFAULT`, which names a resolution order rather than an
  engine. Every tuning number in arch §8 assumes Jolt, and a project migrated from an older
  template is silently still on GodotPhysics.
- **Validate and recover from invalid node state; do not permanently disable the node.** If the
  scene becomes valid again, it should resume normal behaviour. (house)
- **Accessibility is not lazy-able** (`AGENTS.md`, epics E15). **Colourblind-safe signage and
  chute coding is a correctness requirement, not a nicety** — routing is the core verb and the
  game is unplayable without it.
- **Readable silhouettes are a design constraint, checked at video-compression quality** (epics
  E17, vision §3.4). "Legible at 4K" is not the test. The clip is the product.

---

## 11. Budgets are rules

Arch §8's numbers are provisional, and provisional is not advisory — a number you can argue with
beats a vibe you cannot. If a change pushes past one, that is the change's problem.

| Budget | Target |
| :-- | :-- |
| Host upstream — gameplay | **< 60 KB/s** |
| Host upstream — voice | **< 30 KB/s** |
| Host upstream — total | **< 90 KB/s** |
| Awake parcel bodies | **≤ 40** |
| Live parcel records | ~200 |
| L1 suite | **< 5 s** |
| Report readable at stream resolution | **< 4 s** |
| Grab → visible hand motion | **0 frames of network wait** |

The two upstream budgets are tracked separately on purpose: voice can move off the host entirely
(arch §6.5's upgrade path) without touching gameplay, so conflating them would hide the cheapest
available fix. And **a slow suite is an unrun suite** — the 5 s L1 target is what makes the
engine-free boundary worth having at all.

---

## 12. Review checklist

Before opening a PR:

- [ ] Suites pass at the level the story stated; CI green.
- [ ] `Dlo.Domain` has no Godot type in any signature, and the architecture test still passes.
- [ ] No `GD.Randi()`, `Random.Shared`, static mutable state, wall clock, or I/O in Domain.
- [ ] Every new RPC states `TransferMode` and `CallLocal` deliberately; nothing positional is
      `Reliable`.
- [ ] Nothing new sends a *fact* from a client.
- [ ] No new optimistic client-side action outside grab.
- [ ] No belt parcel put into the `Dynamic` replication class.
- [ ] No new gameplay state stored on a node instead of a record.
- [ ] Nothing new compares `TouchedAtShiftTime`; nothing returns `Unattributed` as a fallback.
- [ ] Nothing caches a routing answer across a `PolicyState` change.
- [ ] Domain-system construction still happens in exactly one place, behind one
      `Multiplayer.IsServer()` branch — `grep -rn "new ShiftDirector\|new ShiftLedger\|new ParcelRegistry" src/`.
- [ ] No new autoload.
- [ ] New content type ships with its `ContentTool` validation rule, and nothing that should be
      data was authored in code.
- [ ] XML docs on new Domain public members.
- [ ] **No comment re-argues a design document, and no file's comments outweigh its code** (§7).
      A `<remarks>` past one short paragraph plus one `<para>` is the signal; the fix is a citation.
- [ ] New behaviour has a test named after the behaviour, and it fails if you revert the change.
- [ ] Anything asymmetric-information-shaped has a *negative* test.
- [ ] Every `ponytail:` names a ceiling and an upgrade path.
- [ ] Rules implemented from the vision or the architecture cite their section.
- [ ] No file does two jobs. No `#region`. No commented-out code.
- [ ] Magic numbers are named constants or content data.

---

*What the game is: [the vision](dead-letter-office-vision.md). How it is built:
[the architecture](dead-letter-office-architecture.md). In what order:
[the epics](dead-letter-office-epics.md).*
