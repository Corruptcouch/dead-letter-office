# Dead Letter Office — Epic Breakdown

**Status:** Draft v0.2 · Companion to the Vision and Architecture documents

> **v0.1 → v0.2 — owner decision pass, 2026-08-24.** All five of the vision's §17 open
> questions are resolved, plus three of this document's own. Eight epics changed.
>
> | Was open | Resolved | Lands in |
> | :-- | :-- | :-- |
> | Termination consequence (Q1) | **The employee record.** You lose your name, not your progress | E5, E11 |
> | Shift length (Q2) | **8–12 min × 3–6 shifts**, provisional, curve as data | E5 |
> | Does inspection survive four players (Q3) | **Build as designed, measure at Gate 2.** Mandated rotation is the pre-approved held fix | E3, E19 |
> | Horror ceiling (Q4) | **Confirmed as recommended** — architecture only, no agent, no evidence of agency | E6, E9 |
> | Price point (Q5) | **$9.99 Early Access** | E18 |
> | Voice transport | **Native capture + pure-C# Opus**, not Steam Voice | E7 |
> | Culpability window | **Never expires** — last toucher owns it | E6, E8 |
> | Telemetry provider | **Local-only + an export button.** No servers at all | E20 |
>
> **Two of these changed more than their own epic.** The voice decision removes E7 from the
> GodotSteam blast radius, so E7 no longer waits on E0's finding — a real unblocking. And the
> termination decision gives E11 its first piece of non-cosmetic content, which matters
> because E11 was the epic §14 nominates to starve.
>
> **Derived from** [dead-letter-office-vision.md](dead-letter-office-vision.md) §13–§15 and
> [dead-letter-office-architecture.md](dead-letter-office-architecture.md).
>
> **E0–E13 keep the vision's numbering exactly.** E14–E20 are appended for work the vision
> does not mention and that consequently had no owner: repo foundation, UI, audio, asset
> pipeline, shipping, playtest operations, telemetry.
>
> **Numbering is not order.** E14 (Foundation) is a Tier 0 prerequisite that blocks
> everything including E0, despite its number. Renumbering the vision's epics to fix that
> would break every reference in a signed-off document, which is a worse trade.

---

## How to use this document

Each epic carries a **Decisions already made** section. That section exists so a developer
working a story does not need to interrupt the product owner. **If a story raises a question
that isn't answered there, that's a gap in this document — record it and get it answered
once, at the epic level, rather than answering it privately inside a story.**

**Definition of Ready** for any story: acceptance criteria are testable, the required test
level is stated, and no open question blocks a start.

**Definition of Done** for any story: code merged, tests at the stated level passing, CI
green, XML docs on new Domain public members, and any `ponytail:` shortcut carries its
ceiling and upgrade path.

Test levels are Architecture §10: **L1** xUnit no engine · **L2** GdUnit4 in engine ·
**L3** headless multi-peer.

---

## The gates

The vision does not name gates, but §15 states a validation question that is one, and
building past it without answering it is the single largest risk in the plan.

| Gate | Question it answers | Exit |
| :-- | :-- | :-- |
| **Gate 0 — Feel** | Does grabbing, carrying and dropping a parcel feel *tight but awkward* — for one player, locally? | A written go/no-go. On fail, fix E1 before any other feature work |
| **Gate 1 — The Wire** | *"Does manipulating a shared physical object still feel believable when three other people are doing it too, over real internet?"* (vision §15, verbatim) | A written go/no-go. **On fail, nothing above this line matters** |
| **Gate 2 — The Job** | Do four players separate to four posts and talk, or clump into a blob? Does any player feel time pressure from *scrutiny*, or only from *volume* (vision Q3)? | A written go/no-go on the asymmetric-information design. On a dilution finding, build E3's held fix rather than reopening the design |
| **Gate 3 — The Clip** | Does a session generate a story worth retelling? Does the report get screenshotted? | Evidence for or against pillar §3.4 before content scale-up |

Gate 1 is the MVP line. **No epic past Tier 2 starts before Gate 1 passes**, and a pass on
Gate 0 does not authorise Gate 1's scope.

---

## Tier 0 — Spine

*Everything blocks on these.*

### E14 — Foundation and conventions
**Tier 0 · No dependencies · Blocks everything (see the numbering note above)**

**Goal:** a repo any developer can clone, build, and test in under ten minutes.

**In:** solution and project structure per Architecture §1.3. `.editorconfig`,
`Directory.Build.props`, `global.json`. xUnit and GdUnit4 harnesses. GitHub Actions CI. The
architecture test (Arch §10.5). `.gitignore` and `.gitattributes` for Godot 4.7 with Git LFS
for binary assets. README with setup steps. **The `.gitignore` is already rewritten for this
project** — names, paths and stale epic citations corrected ahead of the epic, so what remains
here is `.gitattributes` and the LFS configuration.

**Out:** any gameplay. Any Steam dependency.

**Decisions already made:**
- **Godot 4.7.2-stable-mono**, .NET 10 SDK, C#, **`net10.0` for every project including
  `Dlo.Domain`.** The editor version is pinned; on the development machine it lives at
  `D:\work\Godot\Godot_v4.7.2-stable_mono_win64\`, and that path is machine-local — the README
  is where each machine records its own.
- **Godot generates `net8.0` and E14 overrides it, deliberately.** Verified end to end (Arch
  §1.4): `net10.0` builds against GodotSharp, Godot's own `--build-solutions` builds it, the
  editor does not rewrite a setting that already has a value, and it runs. It *does* re-add the
  line as `net8.0` when it is missing, which is why `Dlo.Game.csproj` keeps its own copy rather
  than deferring to `Directory.Build.props` (Arch §1.4, corrected in E14-03). The runtime is
  `.NET 10` at either TFM
  because Godot's host rolls forward, so `net8.0` would only mean compiling against an older BCL
  than the one executing — and one that leaves support in November 2026. **Export is the one leg
  not yet verified**, and E18 owns it.
- **`Dlo.Domain` is `net10.0`, not `netstandard2.1`.** It has no consumer outside this repo, and
  `netstandard2.1` was what forced a hand-declared `IsExternalInit` for `readonly record struct`.
  Deleting a workaround beats documenting one.
- Multi-project solution at the repo root; `project/solution_directory="../.."` in
  `project.godot` (Arch §1.4).
- Nullable enabled everywhere; warnings-as-errors in Domain only.
- Git LFS for `.png`, `.wav`, `.ogg`, `.glb`. **Not** for `.tres` — those must stay diffable.
- Jolt physics, **set explicitly** in `project.godot` as `physics/3d/physics_engine="Jolt
  Physics"`. A fresh 4.7.2 project leaves it at `DEFAULT`, which is a resolution order and not
  an engine — confirmed by probing the editor (Arch §1.4).

**DoD:** fresh clone builds and runs both suites via documented commands. CI green on an
empty commit. The architecture test fails if someone adds a Godot reference to Domain.

**Stories:** solution scaffold · editorconfig + build props · Godot project with Jolt
confirmed · xUnit harness + first passing test · GdUnit4 harness · architecture test · CI
workflow · LFS + `.gitattributes` · README.

---

### E0 — Netcode Spine
**Tier 0 · Depends: E14 · Blocks all of Tier 1**

**Goal:** host-authoritative multiplayer from the first line of gameplay code, and an honest
answer on whether the Steam C# path works.

**In:** `IGameTransport` with `EnetTransport` and `SteamTransport` (Arch §3.5). Session
lifecycle: host, join, leave, teardown. `SessionRoot` and the single `HostSession`
construction seam (Arch §3.2). Replication primitives — a `MultiplayerSpawner` wrapper with
custom spawn functions, synchronizer configuration per replication class. `LatencyPeer` dev
decorator. The `Dlo.Net.Tests` headless multi-peer harness (Arch §10.4).

**Out:** lobbies, invites and friend lists — those are E12. Voice — E7. Anything a player sees.

**Decisions already made:**
- Host-authoritative, full authority, no rollback, no deterministic physics, no dedicated
  servers (vision §16). 4 peers maximum.
- **Clients send intent; the host sends outcomes; a client never sends a fact** (Arch §3.1).
- `Reliable` for decisions, `UnreliableOrdered` for streams. Never leave the `[Rpc]`
  transfer mode at its default by accident.
- ENet is the development and test transport permanently; Steam is the shipping transport.
  L3 runs against ENet.
- **The Steam C# path is proven in week one, not at export.** GodotSteam's C# bindings lag
  the extension and the `SteamMultiplayerPeer` C# port is immature (Arch §3.5, open item 1).

**DoD:** four headless peers connect over both transports, exchange an intent RPC and a
replicated value, and survive a client disconnect and a host teardown. L3 harness runs in CI.
**A written finding on the Steam C# path's viability**, with a recommendation.

**Stories:** transport interface + ENet · session lifecycle · spawner wrapper ·
synchronizer classes · Steam transport spike **(do this first)** · LatencyPeer · L3 harness
· host-loss teardown.

---

### E1 — Embodiment
**Tier 0 · Depends: E14, E0 · Gate 0 lives here**

**Goal:** a controller that is tight, in a world that is not.

**In:** first-person character controller — move, look, jump, crouch. Hands. Grab, carry,
throw, drop. Stumble and trip. Two-player cooperative carry. The grab protocol per
Arch §3.3, including the optimistic local attach. IK-driven hands via Godot 4.7's
`TwoBoneIK3D` / `FABRIK3D`.

**Out:** what is being carried having any data on it — that is E2. Damage, hazards — E6.

**Decisions already made:**
- **Awkward ≠ unresponsive** (vision §3.1). The parcel is the problem, not the input.
- **Weight is expressed through the object, never through the input.** Input damping to
  simulate heaviness is banned (Arch §6.1).
- Input is local and immediate on all four machines, always. Zero frames of network wait
  before the hand moves.
- The real joint exists only on the host; clients hold a visual-only attachment.
- **A mispredicted grab is a feature, not a bug** — it reads as a teammate yanking the box
  away (Arch §3.3). This is the only optimistic path in the build; do not generalise it.

**DoD:** **Gate 0.** A single player can pick up, carry, throw and drop a heavy awkward box
and describe it as awkward rather than broken. Two players can carry one object that neither
can lift alone. Grab contention between two clients resolves to exactly one holder (L3).

**Stories:** controller move/look · hands + IK · grab joint (host) · optimistic client attach
· grab contention resolution · carry + throw · two-person carry · stumble · **Gate 0 feel
session**.

---

## Tier 1 — The Job

*Requires E0, E1.*

### E2 — The Parcel
**Tier 1 · Depends: E0, E1**

**Goal:** a data carrier with a physical body, whose identity outlives both.

**In:** `ParcelId`, `ParcelRecord`, `ParcelRegistry` (Arch §5.1). Manifest, address,
destination code, weight, fragility, declared contents, **actual contents**, tamper state.
The lifecycle state machine (Arch §4.4). Openable parcels. Spawn args and the custom spawn
function (Arch §5.2). The three replication classes (Arch §3.4). Object pooling.

**Out:** the desks and tools that read a manifest — E3. What is inside being alive or
dangerous — E6. Investigating a dead letter — E10.

**Decisions already made:**
- **Identity survives the node being freed** (Arch §5.1). A parcel in a tube may have no
  body at all and still be the same parcel.
- **Openable is in scope; the dead-letter loop is not** (vision §4). Opening is a
  host-validated intent, never optimistic, and permanently marks tamper state.
- `ActualContents` exists on the record from authoring time and is replicated to nobody
  until the box is opened.
- `Railed` is the default class for anything on a belt. A belt parcel in `Dynamic` is a bug.
- **Misrouting is recorded silently and revealed on the report** (Arch §4.4). No live
  wrong-chute indicator.

**DoD:** a parcel survives belt → grab → throw → tube → respawn with the same `ParcelId`,
manifest and tamper state, verified at L3. A pooled parcel node carries nothing over from its
previous life. Replication of 40 awake parcels stays inside Arch §8's budget, **measured**.

**Stories:** record + registry · id stability across despawn · manifest model · spawn args ·
replication classes + promotion/demotion · pooling · openable + tamper state · declared vs
actual contents · L3 identity test · **replication budget measurement**.

---

### E3 — The Work
**Tier 1 · Depends: E2 · Gate 2 lives here**

**Goal:** receive → inspect → scan → stamp → route, split across four posts that cannot be
worked from one spot.

**In:** the four posts (vision §8) — intake dock, scan desk, routing chart, chute floor.
Scanner. Stamps, including `INCINERATE`. Forms. The routing chart as a rendered view of
`PolicyState`. `RoutingRules.Evaluate` (Arch §4.5). Per-post replication filtering so a
client genuinely does not hold a manifest it has not scanned (Arch §5.3).

**Out:** the room the posts are in — E4. Quota and the clock — E5. Voice — E7.

**Decisions already made:**
- **No player can complete a parcel alone** (vision §8). This is enforced by *replication*,
  not by UI gating — the host does not send what a post has not earned (Arch §5.3).
- `RoutingRules.Evaluate` is a pure function of parcel, chute and policy. No engine, no clock.
- `PolicyState` is mutable and replicated, and the PA system can change it mid-shift
  (Arch §4.5). The chart going stale is the antagonist landing a hit.
- The `INCINERATE` stamp is a loaded gun pointed at a teammate's work (vision §3.5) and is
  deliberately available before it is wise.
- Stamping is **never** optimistic — it is a decision the report will record.
- **Build the four posts as §8 designs them and measure dilution at Gate 2** (resolves vision
  Q3). The risk that scrutiny thins into four easy jobs is real but unconfirmed, and
  pre-building a mitigation would be solving a problem we do not yet have — rung 1 of
  AGENTS.md.
- **The held fix is mandated post rotation**, pre-approved and deliberately not built:
  management requires staff to rotate stations mid-shift, announced over the PA. It is
  on-theme (the bureaucracy causing the problem is the antagonist working), small (a policy
  plus a PA line), and it does not disturb the voice loop, since a player still holds only
  their current post's information. **If Gate 2 finds dilution, build this rather than
  reopening the design.**

**DoD:** **Gate 2.** Four players complete parcels correctly and cannot do so from one
position. A client that has not scanned a parcel provably never receives its manifest (L3
anti-assertion). Routing correctness has an L1 suite over a policy matrix.

**Stories:** post model · scanner · stamp tool · `RoutingRules.Evaluate` + L1 matrix ·
`PolicyState` + chart rendering · per-post replication filter · L3 manifest anti-assertion ·
mid-shift policy change · **Gate 2 four-player session**.

---

### E4 — The Facility
**Tier 1 · Depends: E1, E2**

**Goal:** a believable postal facility whose architecture already leaves room for something
stranger.

**In:** conveyors, chutes, pneumatic tubes, doors. Layer 2 greybox at ~30×24m plus adjacent
rooms (vision §8, §12). Layer 1 greybox — lobby, counter, PO boxes, staff door. Layer 3
greybox as an expansion surface only. Signage as authored content. Navigation for anything
that needs it.

**Out:** mutating the building — E9. Hazards in it — E6. Art beyond greybox — E17.

**Decisions already made:**
- **Mundane first, uncanny second** (vision §3.3). It begins as a believable post office.
  Not "haunted post office."
- Facility scale is ~30×24m **plus adjacent rooms**, with posts deliberately distributed
  so a blob cannot form (vision §8).
- **Build for expansion; do not make the product depend on Layer 3** (vision §12).
- Conveyors carry parcels as `Railed` — a spline plus speed, extrapolated by clients with no
  ongoing traffic (Arch §3.4). This is the mechanism that makes "the belt never stops"
  affordable.
- Signage is data from day one, not baked into geometry (Arch §7).

**DoD:** the belt runs continuously and parcels accumulate when nobody clears them. Layer 2
supports four players at four posts without a traversal complaint. A tube moves a parcel
between rooms without replicating a body. **Replication cost of a full belt is measured
against Arch §8.**

**Stories:** conveyor + rails · chutes · pneumatic tubes · doors · Layer 2 greybox · Layer 1
greybox · Layer 3 stub · signage as data · nav · full-belt cost measurement.

---

## Tier 2 — The Pressure

*Requires Tier 1. Gate 1 sits at the entrance to this tier.*

> **Gate 1 — The Wire.** Before any Tier 2 epic starts: does a shared physical object still
> feel believable with four players over real internet? This is vision §15's validation
> question and the MVP line. E14, E0, E1, E2, E3 and E4 plus E12's lobby are what it needs.

### E5 — The Ratchet
**Tier 2 · Depends: E3, E4**

**Goal:** the employment stint — quota that climbs until it kills you.

**In:** quota, shift timer and whistle, the escalation curve, termination, re-hire. The stint
as the roguelite unit (vision §5). What persists and what does not. The clock-in / clock-out
frame around a shift.

**Out:** the report at the end of a shift — E8. Wrongness climbing alongside quota — E9.
Cosmetics and the unlock ladder — E11.

**Decisions already made:**
- **The stint is the roguelite unit, not the shift** (vision §5).
- Quota ratchets each shift; both quota and wrongness reset on termination.
- Persists across stints: cosmetics, unlocked tools, player knowledge, the unlock ladder,
  **and the employee record**. Does not: facility layout, wrongness, quota, mail volume.
- **Spectacular failure, cheap failure** (vision §3.6). Punishment that stings for an hour
  kills the rerun.
- **Shift length: 8–12 minutes, 3–6 shifts per stint.** Provisional — build the escalation
  curve as a data file so Gate 2 can move it without a code change (resolves vision Q2).
- **Termination costs your name, not your progress** (resolves vision Q1). Nothing mechanical
  is lost. On termination the crew's employees are filed permanently with their final stats
  and appear on the Former Personnel wall (E11). The cost is identity: you were somebody for
  six shifts and now you are a line in a filing cabinet, which is the bureaucracy landing its
  last hit. Derived entirely from data `ShiftLedger` already holds (Arch §9).
- **The wall is a trophy case, not a progression store.** Nothing on it may affect a future
  stint — that is what keeps this decision inside §3.6.
- Employee names derive from the player's Steam persona, because vision §10 requires PA lines
  to reference player names and §7's report already prints them.

**DoD:** a stint runs shift 1 → N → termination → re-hire with the correct things reset.
Quota arithmetic across a stint has an L1 suite. The curve is a data file, not a constant. A
terminated employee is filed with correct career stats and survives a restart.

**Stories:** shift clock · quota model · escalation curve as data · whistle + clock-out ·
termination · **employee record + career rollup** · re-hire + reseed · persistence boundary ·
L1 stint arithmetic.

---

### E6 — Chaos
**Tier 2 · Depends: E2, E4**

**Goal:** the weather. Hazards that are environmental, not adversarial.

**In:** jams, spills, fire, ticking ordnance, the heavy anvil, live contents that escape.
Fragility and breakage discovered later. Damage to the facility. Culpability propagation
through collision chains (Arch §4.6).

**Out:** anything that hunts a player. Anything that reads as a monster.

**Decisions already made:**
- **The anomaly is only weather** (vision §1). The enemy is the bureaucracy. Hazards are
  environmental; nothing in this epic is an adversary.
- **Horror ceiling, LOCKED** (resolves vision Q4). The ceiling is "this building is
  measurably larger than its footprint." No entity, **and no evidence of an agent either** —
  nothing follows you, nothing moves behind your back, nothing undoes what you did. Weather
  does not leave footprints. All menace is institutional; the PA reading your name is the
  scariest thing in the game.
- Breakage is frequently discovered on the report rather than in the moment (vision §9).
  Culpability is stamped at the **causing** impact, not at discovery, so the delay costs
  nothing.
- **Culpability never expires** (Arch §4.6, rule 3). Last toucher owns it until another actor
  overwrites them. This makes arson attribute correctly for free — the player who lit a fire
  owns everything it destroys, however long it burns.
- Live contents are physical, mobile and unhelpful. Not dangerous in a combat sense, and not
  an agent — see the ceiling above.

**DoD:** one hazard is enough for Gate 1; the full set is Tier 2. Culpability propagation has
an L1 suite over synthetic collision chains. A fire started by a player attributes to that
player on the report regardless of how long it burns.

**Stories:** jams · spills · fire + spread · ticking ordnance · the anvil · live contents ·
fragility + delayed breakage · structural damage · culpability propagation (L1) ·
facility-caused damage attributes to `UNATTRIBUTED` (L1).

---

## Tier 3 — The Social Layer

*Requires Tier 1. E8 requires E5.*

### E7 — Presence
**Tier 3 · Depends: E3, E4 · No longer blocked by E0's Steam finding**

**Goal:** make voice load-bearing rather than ambient.

**In:** proximity voice with falloff. Handheld radios. The PA system. Deliberate separation
as a level-design constraint. PA lines as data, gated by wrongness threshold, able to
reference player names.

**Out:** voice moderation at production grade (vision §16). Voice acting — E16.

**Decisions already made:**
- **Voice is load-bearing, not ambient** (vision §4), and it is enforced by asymmetric
  information (E3), **not by falloff alone**.
- PA lines are data-authored, threshold-gated, and can interpolate player names
  (vision §10).
- **Text-to-speech or typed placeholder early; voice acting late** (vision §10).
- The PA is not an autoload — it is a node under `AudioDirector` reading replicated
  wrongness (Arch §6.2).
- **Voice transport is decided: native capture plus pure-C# Opus, over `IGameTransport`**
  (Arch §6.5). `AudioStreamMicrophone` + `AudioEffectCapture` for the mic, Opus at ~24 kbps,
  decoded into an `AudioStreamGenerator` on a positional `AudioStreamPlayer3D`. **Not Steam
  Voice.**
- **The reason is risk concentration, and it is the point of the decision.** E12 already
  depends on the GodotSteam C# path, which is the project's largest open risk. Putting voice
  there too would let one fragile dependency take out both the social and session layers.
  **This epic therefore no longer waits on E0's Steam finding.**
- Voice also works in development, in CI and under `LatencyPeer` as a result — which matters,
  because vision §8's entire design rests on voice functioning.
- **Raw PCM is not an option.** 16-bit mono at 24 kHz is ~48 KB/s per speaker, which alone
  exceeds the gameplay budget. A codec is mandatory.
- The Opus package must be **managed-only, no native binary**, so all three desktop export
  targets work without per-platform builds. This is the project's one new runtime dependency
  and it needs an explicit AGENTS.md rung-4 justification in review.
- Voice is relayed via the host (~27 KB/s at four speakers), budgeted in Arch §8. Moving it to
  direct peer sockets is a documented optimisation, not a requirement.
- Proximity falloff is `AudioStreamPlayer3D`'s job, not ours. Radios are the same decoded
  stream on a non-positional player.

**DoD:** four players at four posts must talk to complete parcels, and observers confirm they
do. A PA line fires on a wrongness threshold and names a player. Voice works over both
transports, and voice traffic stays inside Arch §8's 30 KB/s budget under four-way chatter,
**measured**.

**Stories:** Opus package selection **(first)** · mic capture · encode/decode · route over
`IGameTransport` · positional playback + falloff · voice bandwidth measurement · radios ·
PA line data schema · threshold gating · name interpolation · placeholder TTS.

---

### E8 — The Blame Report
**Tier 3 · Depends: E5, E6 · Gate 3 lives here**

**Goal:** the screenshot. Vision §7 calls it the highest value-per-hour feature in the
product, and §14 says it is what makes the game clippable.

**In:** the end-of-shift screen. `ShiftLedger` aggregation. `UNATTRIBUTED` labelling.
Employee of the Shift. The institutional voice.

**Out:** the attribution plumbing itself — that is built in E2 and E6 by design, because
retrofitting attribution is what makes it expensive (Arch §4.6).

**Decisions already made:**
- **Not a score screen — a blame ledger, in the voice of an indifferent institution**
  (vision §7).
- **Every consequential action carries an actor. Unattributable events are labelled
  `UNATTRIBUTED`, never hidden** (vision §7).
- **`UNATTRIBUTED` means "no player has ever touched this object."** Because culpability
  never expires (Arch §4.6), the label no longer covers "we lost track" — it covers the
  facility's own damage: the belt jamming itself, structural settling, wrongness-caused
  damage, a parcel that arrived already broken. That is a narrower category than the original
  design assumed, but a truer one, and it is exactly what an indifferent institution would
  file as nobody's fault.
- **Readable at streaming resolution in under four seconds** (vision §7). This is a hard
  budget (Arch §8).
- The report also feeds the **employee career rollup** that E5 files on termination, so its
  aggregation is reused rather than duplicated.
- The report is a `GroupBy` over `ShiftLedger`. If it is more than that, the plumbing in
  E2/E6 is wrong and the fix belongs there.
- The host's ledger and every client's rendered report **agree exactly** (Arch §10.4).

**DoD:** **Gate 3.** The report names people, is passive-aggressive about it, reads in under
four seconds, and playtesters screenshot it unprompted. Ledger aggregation has an L1 suite.
Host and client reports agree at L3.

**Stories:** ledger aggregation (L1) · report layout · four-second readability check ·
`UNATTRIBUTED` presentation · Employee of the Shift · institutional copy pass · L3
host/client agreement · **Gate 3 clip session**.

---

### E12 — Session Ops
**Tier 3 · Depends: E0 · Needed by Gate 1**

**Goal:** four friends in a game in one click.

**In:** lobby. One-click Steam invite. Drop-in / drop-out. Graceful host loss. Player
name/identity plumbing for the report and the PA.

**Out:** matchmaking, dedicated servers, persistent accounts, anti-cheat (vision §16).

**Decisions already made:**
- **No custom matchmaking.** Steam friends and invites only (vision §16).
- Graceful host loss means a clean end and an honest message, **not** host migration.
  Migration is not in scope.
- The lobby is needed for Gate 1 and is therefore MVP scope (vision §15), while the rest of
  this epic is not.
- Blocked on E0's Steam finding. If the C# path is unworkable, this epic changes shape
  before it starts (Arch open item 1).

**DoD:** a Steam invite puts a friend in the game with no address typed. A client leaving
mid-shift does not corrupt the shift; a host leaving ends it cleanly on all peers (L3).

**Stories:** lobby · Steam invite · join in progress · leave handling · host-loss teardown ·
player identity for report/PA.

---

## Tier 4 — The Identity

*Requires Tier 2.*

### E9 — Wrongness
**Tier 4 · Depends: E4, E5**

**Goal:** the thing that makes it *this* game (vision §14).

**In:** the wrongness float. The mutation table with threshold gating and per-stint sampling
(vision §11). Layer 3 access gated by wrongness. Applying mutations to live geometry and
navigation.

**Out:** a scripted escalation sequence. Procedurally generated liminal space (vision §16).

**Decisions already made:**
- **Sampled from a table, gated by threshold — never a scripted fixed sequence**
  (vision §4, §11). A scripted sequence is spent after one run.
- **Mutations must be specifically postal** (vision §11). Familiar postal architecture
  behaving incorrectly, not generic liminal-space aesthetics. This is enforced in the
  authoring schema (Arch §7).
- **Clients receive the resolved mutation id list, not the seed** (Arch §4.7) — mutations
  change geometry that physics and nav depend on, so a table-version mismatch would
  desynchronise the building.
- Layer 3 access is gated by wrongness, not by shift number (vision §12).
- **Horror ceiling, LOCKED** (resolves vision Q4): cap at "architecturally impossible."
  Never an entity, **and never evidence of an agent** — nothing follows you, nothing moves
  behind your back, nothing undoes what you did. A mutation may make the building wrong; it
  may not make the building *act*. See E6 for the same bound applied to hazards.
- The mutation authoring schema enforces both constraints — postal, and agentless — so a
  reviewer can point at a rule rather than have an argument (E13).

**DoD:** two stints escalate differently from the same table. Sampling stays inside its
threshold band (L1). A mutation applies identically on all four peers, including navigation.
A seed in a log reproduces a reported facility.

**Stories:** wrongness model · mutation table schema · threshold sampling (L1) · mutation
application + nav rebuild · mutation replication (L3) · Layer 3 gating · first postal
mutation set · seed logging.

---

### E10 — Dead Letters
**Tier 4 · Depends: E2 · DEFERRED, not cut**

**Goal:** the second loop — open → investigate → classify → live with it.

**In:** deferred. Recorded so the dependency stays cheap.

**Out:** everything, for now.

**Decisions already made:**
- **Deferred to Tier 4, not cut** (vision §4). Its expensive dependency — openable
  packages — is kept in E2, which is what keeps this cheap to add later.
- Do not build this before Gate 3. If the report and wrongness are landing, this is the next
  identity feature; if they are not, this does not save the game.

**DoD:** n/a until authorised.

**Stories:** none decomposed. Deliberately.

---

### E11 — Between Shifts
**Tier 4 · Depends: E5, E8 · Starve the cosmetics, keep the wall**

**Goal:** the small unlock ladder, the Former Personnel wall, and somewhere to stand while
both happen.

**In:** break room, lockers, cosmetics, the unlock ladder, persistence of what persists, and
**the Former Personnel wall** — the physical surface that displays the employee records E5
files on termination.

**Out:** anything that affects a shift's difficulty. Any second economy.

**Decisions already made:**
- **The starve guidance now has an exception.** §14 says starve this epic first, and that
  still holds *for the cosmetics*. It does not hold for the wall: the termination decision
  makes the wall the entire consequence of being fired, so cutting it cuts the answer to
  vision Q1. **Split this epic when prioritising** — the wall ships, the lockers can wait.
- **The wall is a trophy case, not a progression store.** Nothing displayed on it may affect
  a future stint. That constraint is what keeps termination inside §3.6's cheap-failure rule.
- The wall is derived from `EmployeeRecord` (Arch §9), which is itself rolled up from E8's
  ledger. No new data collection — this is a presentation surface over facts that already
  exist.
- Employee names derive from the player's Steam persona (E5).
- Cosmetics and unlocked tools persist; nothing here resets wrongness or quota (vision §5).
- No accounts, no cloud saves (vision §16). Local files, atomic write, empty migration chain
  at version 1 (Arch §9).

**DoD:** an unlock survives a restart. A terminated crew appears on the wall with correct
career stats and survives a restart. The migration chain exists and is empty.

**Stories:** save file + atomic write · empty migration chain · `EmployeeRecord` persistence ·
**the Former Personnel wall** · unlock ladder model · break room · lockers · cosmetics.

---

## Tier 5 — Longevity

*Runs alongside from Tier 2 onward.*

### E13 — Authoring Pipeline
**Tier 5 · Depends: E2, E4 · Start early and badly**

**Goal:** parcels, anomalies, room mutations, PA lines and signage as data, no code required.

**In:** `.tres` schemas for every content type in Arch §7. `Dlo.ContentTool` with a
`validate` command in CI. Authoring documentation aimed at whoever is writing content at 11pm.

**Out:** a visual editor. Modding support.

**Decisions already made:**
- **This epic determines whether the game is alive twelve months after launch. Start it early
  and badly rather than late and well** (vision §13).
- Every content type is data from its first day, **even when there are only two of them**
  (Arch §7).
- `.tres` stays out of LFS so it stays diffable.
- **A content file that breaks an invariant fails the build.** That is the only mechanism
  that keeps a pipeline honest under deadline (Arch §7).
- The mutation schema enforces "specifically postal" so a reviewer can point at a rule
  rather than have an argument.

**DoD:** a new parcel archetype, a new PA line and a new room mutation each ship without a
code change. `ContentTool validate` fails CI on a broken content file.

**Stories:** parcel archetype schema · manifest/address grammar · routing policy schema ·
mutation schema + postal constraint · PA line table · signage table · `ContentTool validate`
· CI wiring · authoring guide.

---

## Appended production epics

*Work the vision does not mention and that consequently had no owner. Numbering continues
from the vision's E13.*

### E15 — UI, HUD and accessibility
**Tier 2 onward · Depends: E3**

**Goal:** the diegetic surfaces the job is read from, and a game people can actually play.

**In:** HUD. Forms and clipboards. The scanner screen. The routing chart's readable
rendering. Settings. Full accessibility set — subtitles, font scaling, colourblind-safe
signage, remapping, sensitivity, FOV.

**Out:** the report screen — E8.

**Decisions already made:**
- Prefer diegetic surfaces. A clipboard beats a HUD panel where the fiction allows it.
- **Signage and chute colour-coding must be colourblind-safe** — routing is the core verb
  and the game is unplayable otherwise. This is a correctness requirement, not a nicety.
- Accessibility is not lazy-able (AGENTS.md).
- `SettingsService` is one of the four permitted autoloads (Arch §6.2).

**DoD:** the routing chart is legible at 1080p from the far wall. The full accessibility set
ships. Remapping covers every bound action.

**Stories:** HUD · scanner screen · clipboard/forms · chart rendering + legibility check ·
settings · subtitles · font scaling · colourblind-safe signage audit · remapping.

---

### E16 — Audio and the PA voice
**Tier 2 onward · Depends: E7**

**Goal:** punchy stingers, and an institution with a voice.

**In:** `AudioDirector`. Parcel impact, breakage, belt, alarm and jam audio. Punchy failure
stingers per pillar §3.4. PA voice production replacing the TTS placeholder. Mix and ducking
against proximity voice.

**Out:** the PA line *content* and gating — E7 and E13.

**Decisions already made:**
- **The clip is the product** (vision §3.4). Punchy audio stingers are a design constraint,
  not polish — a failure needs a sound that carries in a 30-second video.
- PA voice acting is late; TTS or typed placeholder is the early path (vision §10).
- Ducking must not fight proximity voice — voice is load-bearing and wins the mix.
- `AudioDirector` is one of the four permitted autoloads.

**DoD:** a dropped fragile parcel is audibly distinct from a dropped sturdy one. The PA
audibly cuts through a busy floor. Voice remains intelligible under a full belt.

**Stories:** AudioDirector · impact/breakage set · belt + ambient · alarms · failure stingers
· PA voice pipeline · mix + voice ducking.

---

### E17 — Asset pipeline and the placeholder contract
**Tier 1 onward · Depends: E14**

**Goal:** greybox that is honest about being greybox, and a commission brief that is a build
artefact.

**In:** the placeholder contract — every placeholder asset is generated from a specification,
never hand-made and committed. Generation via `Dlo.ContentTool`. Art direction document.
Commission briefs derived from the spec.

**Out:** the commissioned art itself.

**Decisions already made:**
- **Generated placeholders are build outputs and are not committed.** The house project
  learned this the expensive way: committed placeholders went through LFS (a clone that
  skipped `git lfs pull` booted invisible) and the bytes were toolchain-coupled — a .NET
  version bump rewrote every PNG without changing a pixel.
- `Directory.Build.targets` regenerates placeholders before the game builds, so a fresh
  clone just works.
- **Commissioned art replaces placeholders and *is* committed** — it is authored, not
  derived, and cannot be regenerated.
- **Readable silhouettes are a design constraint** (vision §3.4). A parcel's size, weight
  class and fragility must be legible at a glance and in a compressed video.

**DoD:** a fresh clone builds and runs with generated placeholders and no LFS fetch. The
spec, not the asset folder, is the source of truth. Silhouette legibility is checked at
video-compression quality, not just at 4K.

**Stories:** asset specification · placeholder generator · build-target wiring ·
art direction · silhouette legibility rules · commission brief generation.

---

### E18 — Build, export and Steam
**Tier 3 onward · Depends: E0, E14**

**Goal:** a build a stranger can run, on all three desktop targets.

**In:** export presets for Windows, Linux, macOS. CI export. Steamworks app setup, depots,
build upload. `export_presets.cfg` committed with credentials split into
`export_credentials.cfg`. Crash reporting.

**Out:** console platforms. Storefronts other than Steam.

**Decisions already made:**
- **Verify export targets on the first Phase-1 story, not the week of launch.** Confirming
  the Domain assembly reaches the export is part of this (Arch §1.4).
- **Export templates are versioned with the editor and must match it exactly.** The pin is
  4.7.2-stable-mono; the development machine currently has **4.6 templates installed and no
  4.7.2 set**, so the first export fails until they are downloaded. This fails at export time
  rather than at build time, which is why it is recorded here rather than discovered.
- `export_presets.cfg` is committed; secrets live in `export_credentials.cfg`, which is not.
- **Price: $9.99, launching in Early Access** (resolves vision Q5). Matches Lethal Company and
  R.E.P.O. exactly and keeps a four-pack at ~$40 — which is the number that matters, since the
  game is unplayable alone.
- **The price sets the content bar, and the bar is deliberately low.** $9.99 is what makes
  §14's "starve E11 if anything slips" survivable without becoming a review liability. If the
  price ever moves up, that guidance has to be revisited with it.
- Early Access is the category norm and it makes Gate 3's clip evidence a launch input rather
  than a post-launch discovery.
- Worth re-checking comparable pricing at store-page time; the niche has trended toward
  $7.99–$9.99 rather than the $9.99–$14.99 the vision cites.

**DoD:** all three desktop targets export from CI and launch. A Steam build uploads to a
test branch and a friend outside the team plays it.

**Stories:** export presets · CI export · Domain-in-export verification · Steamworks setup ·
depot + upload · crash reporting.

---

### E19 — Playtest operations and the gates
**Runs from Gate 0 onward · Depends: nothing structural**

**Goal:** the four gate decisions, made from evidence rather than from vibes.

**In:** a facilitation protocol. An observation instrument per gate. Recruitment. Written
go/no-go decisions. The findings register.

**Out:** anything that requires code.

**Decisions already made:**
- **Gate 1's question cannot be answered by any test in the repo** (Arch §10.3), which is
  why this is an epic and not a task.
- Each gate needs players **who have not seen the game.** Participants do not carry across
  gates.
- Gate 2's specific risk is named in the vision: four posts may dilute the Papers-Please
  pressure into four easy jobs (Q3), and the crew may clump into a blob (vision §8). Both are
  observation targets, not assumptions.
- **Gate 2 carries a named observation target and a pre-approved fix.** The question is "does
  any player report time pressure from *scrutiny*, or only from *volume*?" If dilution is
  found, the answer is E3's held fix — mandated post rotation — **not** a design reopening.
  Recording it this way is what stops a gate failure from becoming a redesign.
- **Shift length is refined here, not decided here.** 8–12 min × 3–6 shifts is already the
  build target (E5); this epic moves it inside that envelope on evidence.
- The termination consequence is **no longer an open question** — it is the employee record
  (E5). Gate 3 should still observe whether losing a name actually stings.
- **On a Gate 1 fail, feature work stops.** Vision §15: if the answer is no, nothing above
  that line matters.

**DoD:** four written gate decisions, each citing observations rather than opinions.

**Stories:** protocol · Gate 0 instrument · Gate 1 instrument + recruitment · Gate 1
decision · Gate 2 instrument + decision · Gate 3 clip-capture instrument + decision ·
findings register.

---

### E20 — Telemetry
**Tier 4 onward · Depends: E5, E8**

**Goal:** know what a shift actually looks like when nobody is watching.

**In:** a local event log. Shift outcomes, quota met/missed, parcel throughput, misroute rate,
incinerations, deaths, wrongness reached. An **"export my shift log"** button that writes a
shareable file.

**Out:** any upload. Any endpoint. Any third-party SDK. Anything identifying.

**Decisions already made:**
- **Local-only, and there is no upload path at all** (resolves the provider question). The
  player exports a file and shares it if they choose; your Discord is the pipeline.
- **The reason is bigger than telemetry.** §16 rules out dedicated servers, and this product
  otherwise has none — 4-player P2P, no accounts, no backend. An upload endpoint would be the
  *only* server infrastructure in the entire game, which is a disproportionate thing to add
  for balance data.
- **Consequently: no consent flow, no privacy policy, no DPA, no GDPR surface.** We are not a
  data controller, because we collect nothing. The player affirmatively hands over a file or
  does not.
- `ShiftLedger` already holds most of what telemetry wants (Arch §4.6) — this epic is a
  consumer of it, not a second collection path.
- **The recorded ceiling:** a self-selected, small sample. Fine for balance questions
  ("what was the misroute rate at wrongness 6?"), useless for population questions ("what
  percentage of players quit during shift 2"). If a population question ever genuinely blocks
  a decision, that is when to revisit — and revisiting means adding a server, so it needs to
  be worth it.

**DoD:** a local shift log answers "what was the misroute rate at wrongness 6?" without a
build change. The export button produces a file a developer can read, and the game makes no
network request to any host we control — verifiable, because there is no such host.

**Stories:** local event log · shift summary records · export button · balance queries.

---

## Dependency order

```
                        ┌──────────────┐
                        │     E14      │  Tier 0 — blocks everything
                        │  Foundation  │  (numbering is not order)
                        └──────┬───────┘
                    ┌──────────┴──────────┐
              ┌─────▼─────┐         ┌─────▼─────┐
              │    E0     │         │    E1     │  Tier 0 — Spine
              │  Netcode  │         │   Body    │  ── Gate 0 (feel)
              └─────┬─────┘         └─────┬─────┘
                    └──────────┬──────────┘
           ┌───────────────────┼───────────────────┐
      ┌────▼───┐         ┌─────▼──┐         ┌──────▼──┐  Tier 1 — The Job
      │   E2   │────────►│   E3   │◄────────│   E4    │  ── Gate 2 (the posts)
      │ Parcel │         │  Work  │         │Facility │
      └────┬───┘         └────┬───┘         └────┬────┘
           └──────────────────┼──────────────────┘
                             ═╬═  GATE 1 — THE WIRE (MVP line)
                              │   + E12 lobby & invite
                    ┌─────────┴─────────┐
              ┌─────▼────┐        ┌─────▼────┐  Tier 2 — Pressure
              │    E5    │        │    E6    │
              │ Ratchet  │        │  Chaos   │
              └─────┬────┘        └─────┬────┘
                    │                   │
      ┌─────────────┼───────────────────┼─────────────┐
      │       ┌─────▼────┐       ┌──────▼──┐    ┌─────▼───┐  Tier 3 — Social
      │       │    E8    │       │   E7    │    │   E12   │
      │       │  Blame   │       │Presence │    │ Session │
      │       └─────┬────┘       └────┬────┘    └─────────┘
      │             ═╬═ GATE 3        │
      │              │          ┌─────▼────┐
      │              │          │   E16    │
      │              │          │  Audio   │
      │              │          └──────────┘
      │       ┌──────▼───┐  ┌─────────┐  ┌─────────┐  Tier 4 — Identity
      └──────►│    E9    │  │   E10   │  │   E11   │
              │Wrongness │  │  Dead   │  │ Between │
              └──────────┘  │ Letters │  │ Shifts  │
                            │(deferred)│ │(starve  │
                            └─────────┘  │  first) │
                                         └─────────┘

  E13 Authoring Pipeline ────────────► runs from Tier 1 onward
  E15 UI / accessibility ────────────► runs from Tier 1 onward
  E17 Asset pipeline ───────────────► runs from Tier 1 onward
  E18 Build / export / Steam ───────► runs from Tier 3 onward
  E19 Playtest ops ────────────────► runs from Gate 0 onward
  E20 Telemetry ───────────────────► runs from Tier 4 onward
```

**Honest read on ordering, restated from vision §14 because it should survive contact with a
backlog:** E0–E6 produce a competent chaos co-op indistinguishable from its competitors.
**E8 makes it clippable. E9 and the PA make it *this* game.** If anything gets starved late,
starve E11.

One addition the vision does not make: **E13 is the epic most likely to be quietly starved
and most expensive to add late.** It has no visible output, so it loses every prioritisation
argument. Vision §13 pre-empts this — *start it early and badly* — and that instruction is
the only defence against its own reasonableness.

---

## Open decisions and enabling work

**All five of the vision's §17 questions and all three of this document's are closed.** What
remains is empirical — answered by a spike, a measurement or a gate — plus two small gaps the
decision pass itself surfaced (rows 6 and 7), and two toolchain items that arrived with the
**Godot 4.7.2 pin** (rows 9 and 10).

| # | Item | Blocks | Owner | Needed by |
| ---: | :-- | :-- | :-- | :-- |
| 1 | **Does the GodotSteam C# path work at 4 peers?** Bindings lag the extension; the `SteamMultiplayerPeer` C# port is seven commits and has no channels (Arch §3.5). **Blast radius is now E12 + E18 only** — the voice decision removed E7 from it | E12, E18, shipping | Tech | **E0, week one** |
| 2 | **Select the pure-C# Opus package** — managed only, no native binary, all three desktop targets. The project's one new runtime dependency, so it needs an AGENTS.md rung-4 justification in review | E7 | Tech | E7's first story |
| 6 | **Employee name source when Steam is unavailable.** Names derive from the Steam persona (E5), but ENet dev builds, CI and the L3 suite have no persona. Needs a fallback generator — and since the report and the PA both print these names, "Player 2" is a tone regression | E5, E7's PA lines, E8's report | Tech + Owner | With E5 |
| 7 | **Does the Former Personnel wall need a cap?** Records are append-only and never expire, so the file grows without bound and the wall eventually cannot be read. Probably a roll-off or a "notable employees" filter | E11's wall | Tech + Owner | With E11 |
| 8 | The four gate decisions themselves | Everything downstream of each gate | Owner + E19 | Per gate |
| 11 | **Arch §8's "≤ 40 awake parcel bodies" and "< 60 KB/s host upstream" cannot both hold**, and the measurement that was supposed to confirm them found which one gives. Measured 2026-08-25 (E2-10, arch §11): forty awake bodies cost **63–70 KB/s** to three clients at 30 Hz, and the transform encoding is already *better* than §3.4 assumed, so this is not waste to be trimmed. 60 KB/s buys **~35 awake bodies at 30 Hz, or all 40 at 20 Hz** — and there may be a third answer nobody has costed, such as a distance or interest filter. **`Railed` is exonerated**: a belt backed up to its end costs less ongoing than one loose box | E5's volume curve, E6, E4 sign-off | Owner + Tech | Before E5 sets mail volume |
| 9 | **Install 4.7.2 export templates.** The machine has 4.6 templates only; the first export fails until this is done. Not a decision — a task, recorded so it is not discovered at export time | E18 | Tech | Before E18-01 |

*Item 3 — replication cost against Arch §8 — is answered, and the answer opened row 11 above:
measured 2026-08-25 (E2-10, arch §11), the belt is free and forty awake bodies are not.
Item 5 — the headless multi-peer harness — is answered: **four processes**, built and measured
on 2026-08-24 (E0-08, E0-09, arch §11). **Item 4 — Jolt joint stability — is answered too**, on
2026-08-25 (E1-01, arch §11): the instability it was written to find does not exist at any mass
to 500 kg, but rigid joints turn out to express no weight and Jolt does not implement
`PinJoint3D.impulse_clamp`, so E1-04 must grip with a sprung `Generic6DofJoint3D` at a stiffness
near 100 × mass. The remaining numbers are left where they are, because the stories document
cites them by number.*

### Resolved 2026-08-24

Every product question is closed; what remains above is empirical. Kept visible so the
reasoning is not re-litigated. Where a decision has a **held fix**, that fix is pre-approved
and named — a gate failure should trigger it, not a redesign.

| Question | Resolution | Held fix, if any |
| :-- | :-- | :-- |
| Termination consequence (Q1) | **The employee record.** Identity, not progress | — |
| Shift length (Q2) | **8–12 min × 3–6 shifts**, provisional, curve as data | Gate 2 moves it inside the envelope |
| Inspection at four players (Q3) | **Build as designed, measure at Gate 2** | **Mandated post rotation** |
| Horror ceiling (Q4) | **Architecture only. No agent, no evidence of agency** | — |
| Price point (Q5) | **$9.99, Early Access** | — |
| Voice transport | **Native capture + pure-C# Opus over `IGameTransport`** | Direct peer routing, if Arch §8's budget binds |
| Culpability window | **Never expires.** Last toucher owns it | An expiry window, if playtests call it a bug |
| Telemetry provider | **Local-only + export button. No servers** | A server, only if a population question blocks a decision |

---

*Product intent, pillars and scope: see [the vision](dead-letter-office-vision.md). Patterns
and technical decisions: see [the architecture](dead-letter-office-architecture.md). Acceptance
criteria, test levels and story-level dependencies: see
[the story breakdown](dead-letter-office-stories.md) — decomposed through Gate 1, and holding
seven gaps that want an epic-level answer.*
