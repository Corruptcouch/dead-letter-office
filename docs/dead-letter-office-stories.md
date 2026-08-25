# Dead Letter Office — Story Breakdown

**Status:** Draft v0.1 · Companion to the Vision, Architecture and Epic documents

> **Scope of this pass: everything the MVP line needs, decomposed in full.** That is E14, E0,
> E1, E2, E3, E4 and E12, plus the slices of E13, E15, E17, E18 and E19 that must run
> *alongside* them rather than after them. **Tier 2 and later stay at the epic level** — E5, E6,
> E7, E8, E9, E10, E11 and the remainder of the appended epics keep the one-line story lists the
> epics document already gives them, and get decomposed when Gate 1 has reported.
>
> That boundary is deliberate, and it is rung 1 of `AGENTS.md`: **Gate 1 can invalidate a Tier 2
> story, so writing acceptance criteria for one now is work done twice.** Gate 2 carries a
> pre-approved held fix precisely because the design past that line is not settled yet.
>
> **Derived from** [dead-letter-office-epics.md](dead-letter-office-epics.md), which remains the
> authority on what an epic contains and why. Where this document and the epics disagree about
> *what* a story is for, the epics win. This document adds only acceptance criteria, test level,
> dependencies, and the traps.

---

## Status at a glance

**Updated 2026-08-25**, and verified against the working tree rather than from memory: every
story marked Done below had its acceptance criteria re-checked, and the ones that fall short are
marked as such instead of being rounded up.

| | Stories | Where it stands |
| :-- | :-- | :-- |
| **E14** Foundation | **8 done**, 1 partial | Complete bar E14-07's deliberate-failure run |
| **E0** Netcode spine | **8 done**, 2 blocked | Spine is in and L3-proven. Steam is the only hole |
| **E1** Embodiment | **9 done**, 1 blocked | Feature work complete. **Only Gate 0 itself is left** |
| **E2 · E3 · E4** Tier 1 | **7 done** of 30 | E2's spine and E4's belt. **E2-05 and E4-03 are unblocked today**; E3 is untouched |
| **Alongside** E12·13·15·17·18·19 | **3 done**, 2 partial of 29 | E13's spine is in and validated in CI. **E15-04 and E19-01/02 still block Gate 0** |

**35 of 88 decomposed stories are done.** Suite: **L1 66 · L2 105 · L3 41**, all green,
`dotnet format` clean, and `ContentTool validate` green on the authored content.

The five that fall short, all named rather than rounded up:

- **E1-10 — Gate 0.** Every E1 story under it is done, but the gate is a playtest: it needs a
  person who has not seen the game, plus **E15-04** and **E19-01 → E19-02**. It is now the single
  thing standing between this project and its first real verdict.
- **E14-07** — CI runs everything it should and is green, but nobody has watched it go **red**.
  A pipeline never seen to fail is not known to work as a gate.
- **E13-01** — archetypes load, validate and are addressable; an unknown id is inert and an L1
  test proves it. **Logged is owed**, and cannot be paid until something looks one up: Domain has
  no output, so the log line belongs to **E4-01**, the first caller.
- **E13-06** — the CI step is wired in and the validator exits non-zero on a broken file locally,
  but nobody has watched CI go red for content either. **Same push closes this and E14-07.**
- **E0-01 / E0-03** — the Steam path. Blocked on Steam accounts on separate machines, not on any
  decision. The seam around it is finished, so nothing else waits on it.

**Gate 0 is the next milestone and its code is finished.** What remains is the kit around it:
**E15-04** (look sensitivity, invert-Y, FOV) and **E19-01 → E19-02** (the protocol and the
instrument). A participant who cannot set their own sensitivity is testing your mouse, not your
grab feel — which is why E15-04 was always on this path rather than after it.

---

## How to use this document

**Every story inherits, and therefore does not restate:**

- The **Definition of Ready** and **Definition of Done** from the epics document.
- The **Decisions already made** section of its epic. If a story raises a question that section
  does not answer, **that is a gap in the epics document** — record it in
  [Gaps this decomposition surfaced](#gaps-this-decomposition-surfaced) and get it answered once,
  at the epic level. Do not answer it privately inside a story.
- The review checklist in [`CODING-STANDARDS.md`](CODING-STANDARDS.md) §12, in full.
- Arch §8's budgets. They are rules, not advice.

**Test levels** are Arch §10.1: **L1** xUnit no engine · **L2** GdUnit4 in engine · **L3**
headless multi-peer. A story marked *spike* produces a written finding and throwaway code; one
marked *build only* is proved by the build, per `AGENTS.md` on trivia.

**`(post-MVP)`** on a title means the story belongs to its epic but is **not** required to reach
Gate 1 — vision §15 defers it. Build it when the epic is otherwise done and no gate is waiting.

**Mark a story off in the same PR that finishes it.** A checkbox is flipped when its criterion
is *verified*, not when the code that should satisfy it is written — and a story's `**Status:**`
goes to Done only when every one of its boxes is ticked. Anything short of that is 🔶 Partial with
the unmet box left open and one line saying what is owed, which is the state E14-07 is in and the
reason it is still findable. A story quietly left unmarked is indistinguishable from one nobody
started, and re-deriving the difference costs an afternoon of reading the tree.

**Dependencies are story-level, and they are the only sequencing signal here.** There are no
estimates, deliberately. Walk the graph — [the first ten](#the-first-ten-stories--all-walked) are
walked and done, and [what to work on next](#what-to-work-on-next) is read off the same graph.

---

## Tier 0 — Spine

### E14 — Foundation and conventions

*A repo any developer can clone, build and test in under ten minutes.*

#### E14-01 · Solution scaffold
**Depends:** — · **Test:** build only · **Status:** ✅ **Done**

Create `dlo.sln` and the project layout of Arch §1.3 exactly: `src/Dlo.Domain`,
`tests/Dlo.Domain.Tests`, `tests/Dlo.Game.Tests`, `tests/Dlo.Net.Tests`, `tools/Dlo.ContentTool`.

- [x] `dotnet build dlo.sln` succeeds on a fresh clone with no manual step.
- [x] `Dlo.Domain` targets `net10.0` and has zero package references.
- [x] The layout matches Arch §1.3, including `tools/`, which nothing uses yet.
- [x] **`dotnet new sln --format sln`, not the bare command.** The .NET 10 SDK now defaults to the
      newer `.slnx` format, and Godot 4.7.2's tooling reads classic `.sln` — it even prints advice
      about removing an unused `.sln` after migrating, which is the wrong instruction here. Take
      the classic format and do not revisit it until Godot does.

*Write `global.json` (E14-02) before running any `dotnet new` here. It is a five-line file, and
without it the scaffold is generated by whichever SDK happens to be highest — which is the exact
failure E14-02 exists to prevent, arriving one story early.*

*`Dlo.Game.csproj` is not hand-authored — Godot generates it in E14-03 and regenerates it forever
after (Arch §1.4).*

#### E14-02 · `.editorconfig`, `Directory.Build.props`, `global.json`
**Depends:** E14-01 · **Test:** build only · **Status:** ✅ **Done**

- [x] `global.json` pins **an exact .NET 10 SDK version**, not "10". The development machine has
      four SDKs installed — 8.0.424, 9.0.315, 10.0.204 and 10.0.400 — and with no pin, `dotnet`
      silently takes the highest. A build that differs between a developer and CI because nobody
      chose is the failure this file exists to prevent.
- [x] **`net10.0` is set in `Directory.Build.props`, with a comment saying why it overrides
      Godot.** Godot 4.7.2 generates `net8.0` for `Dlo.Game`; we override it (Arch §1.4, verified
      working). The comment is the acceptance criterion, not a nicety — without it the next person
      sees the override, assumes it was a mistake, and reverts it back to what Godot generates.
- [x] **`Dlo.Game.csproj` carries its own explicit `net10.0`, and that is correct.** This bullet
      originally read *every* project, and that turned out to be unachievable: Godot **re-adds a
      missing TFM line** as `net8.0`, and MSBuild imports `Directory.Build.props` before the
      project body, so the returned line wins. The props file is the authority for the five
      hand-authored projects; Dlo.Game is the exception (Arch §1.4, corrected in E14-03).
- [x] The override survives Godot: open the project in the editor, then confirm
      `<TargetFramework>` is still `net10.0`. **This one cannot run until E14-03 exists** — check
      it there, not here, and check it by comparing a checksum across a full editor session rather
      than by eye.
- [x] Nullable is enabled everywhere; warnings-as-errors in **`Dlo.Domain` only** — proved by
      introducing one warning in each project and observing exactly one failure. The Game layer
      builds Godot's noisy generated glue, and failing on it means nobody can build.
- [x] `dotnet format --verify-no-changes` passes and is the formatting authority.
- [x] Every custom MSBuild property lives in `Directory.Build.props` at the repo root, because
      Godot destroys anything put in `Dlo.Game.csproj` (Arch §1.4).

#### E14-03 · Godot project, with Jolt set explicitly
**Depends:** E14-01 · **Test:** build only · **Status:** ✅ **Done**

- [x] `src/Dlo.Game/project.godot` opens in **Godot 4.7.2-stable-mono** and the generated
      `Dlo.Game.csproj` carries a project reference to `Dlo.Domain`.
- [x] **The csproj is bootstrapped by hand, once, from the editor menu** — *Project → Tools → C#
      → Create C# solution*. There is no CLI flag for it: `--build-solutions` is a silent no-op
      when no csproj exists, and opening the project does not trigger it either. Budget for the
      click; it is not automatable (Arch §1.4).
- [x] `project/solution_directory="../.."` is set, or the exporter refuses every C# source at
      export time (Arch §1.4).
- [x] **Godot's own `Dlo.Game.sln` is deleted, leaving `dlo.sln` as the only solution.** Creating
      the C# solution writes one into the solution directory, and Godot refuses to start its
      editor plugin when two solutions there contain the `Dlo.Game` assembly — no build, no
      export, no C# in the editor. Godot finds `dlo.sln` by assembly name, not by filename.
- [x] **`dlo.sln` declares `ExportDebug` and `ExportRelease`**, with `Build.0` on `Dlo.Domain` and
      `Dlo.Game` only. Godot builds exports with those configurations; `dotnet new sln` creates
      neither, and `dotnet build dlo.sln -c ExportRelease` fails with `MSB4126` until they exist.
      This lands here rather than in E18-01 because deleting Godot's solution is what moved the
      burden onto `dlo.sln` — and Arch §1.4 is explicit that export failures stay silent until
      they are expensive.
- [x] `project.godot` contains `physics/3d/physics_engine="Jolt Physics"` **as a literal line in
      the file.** A fresh 4.7.2 project leaves the setting at `DEFAULT`, which names a resolution
      order rather than an engine — so "I checked and it looked fine" is not the criterion; the
      string being in the file is. Every number in Arch §8 assumes Jolt.
- [x] An empty scene runs from the editor *and* from `godot --headless --quit`.

*The editor is pinned at 4.7.2-stable-mono. On the development machine it is at
`D:\work\Godot\Godot_v4.7.2-stable_mono_win64\`; the path is machine-local, and E14-09's README is
where each machine records its own.*

#### E14-04 · xUnit harness and the first real test
**Depends:** E14-01, E14-02 · **Test:** L1 · **Status:** ✅ **Done**

- [x] `dotnet test tests/Dlo.Domain.Tests` runs green, and the invocation is in the README.
- [x] The suite completes in **under 5 s** (Arch §8) — measured now, while it is empty, so the
      number has a baseline to regress from.
- [x] The first test asserts a real value on a real Domain type. `Assert.True(true)` is not a
      harness check, it is a harness lie.

#### E14-05 · GdUnit4 harness
**Depends:** E14-03 · **Test:** L2 · **Status:** ✅ **Done**

- [x] A GdUnit4 test runs both in-editor and headless from the CLI; both invocations are in the
      README.
- [x] The first test makes one real assertion against a node.

#### E14-06 · The architecture test
**Depends:** E14-04 · **Test:** L1 · **Status:** ✅ **Done**

- [x] Arch §10.5's assertion, verbatim: `Dlo.Domain` does not reference `GodotSharp`.
- [x] **Proved by breaking it** — add the reference, watch it go red, revert (Standards §8).
- [x] The PR template carries the check no test can do: a `grep` for a second `new ShiftDirector`
      or `new ShiftLedger` outside `SessionRoot` (Arch §3.2).

#### E14-07 · CI workflow
**Depends:** E14-04, E14-05, E14-06 · **Test:** build only · **Status:** 🔶 **Partial**

- [x] On every push: restore, build, L1, L2, architecture test, `dotnet format --verify-no-changes`.
- [x] CI is green on an empty commit **and red on a deliberately failing test** — push one, watch
      it fail, revert. A pipeline nobody has seen fail is not known to work.
      **Green half done; the red half is still owed** and `ci.yml` says so in its own header.
      This is the cheapest open item in the repo: one scratch branch, one bad assert, one delete.
- [x] L3 is wired in and runs **on merge to main only** (Arch §10.6), as its own job so a broken
      L3 is unmistakable in the checks list. `ContentTool validate` is still absent and arrives
      with E13-06.

#### E14-08 · Git LFS and `.gitattributes`
**Depends:** E14-01 · **Test:** build only · **Status:** ✅ **Done**

- [x] `.png`, `.wav`, `.ogg`, `.glb` are LFS-tracked.
- [x] **`.tres` is provably not** — check one in and confirm the diff is readable text (Arch §7).
      This is the criterion a careless wildcard breaks, and it breaks E13 with it.
- [x] Line endings are normalised, so a Windows clone and a Linux clone produce identical diffs.
- [x] The README describes what a clone that skipped `git lfs pull` looks like — E17's
      generated-placeholder decision makes that state rare enough to be baffling when it happens.

*The `.gitignore` is already written for this project and is not part of this story.*

#### E14-09 · README
**Depends:** E14-04, E14-05, E14-07 · **Test:** build only · **Status:** ✅ **Done**

- [x] A developer with nothing installed reaches "built, both suites green" in **under ten
      minutes** following only this file — timed against someone who has not done it before.
- [x] Every test-level invocation is named: L1, L2, and L3 once it exists. Standards §8 makes a
      missing invocation **an E14 defect**, which is why it has an owner rather than a convention.
- [x] Arch §1.4's gotchas are listed, including that C# hot reload does not pick up Domain changes
      and the fix is restarting the editor. That one costs an afternoon per developer who has to
      discover it alone.
- [x] **The pinned editor version is stated — 4.7.2-stable-mono — and the local install path is
      where each machine records its own.** The development machine's is
      `D:\work\Godot\Godot_v4.7.2-stable_mono_win64\`. A version mismatch between developers shows
      up as `project.godot` churn and export failures, not as a build error.
- [x] **Export templates for the pinned version must be installed**, and the README says so —
      this is the step that is missing on the current machine (epics open item 10).

---

### E0 — Netcode Spine

*Host-authoritative from the first line of gameplay code, and an honest answer on Steam.*

#### E0-01 · Steam C# path spike — **do this first**
**Depends:** E14-03 · **Test:** spike · **Answers:** epics open item 1, Arch open item 1 · **Status:** ⛔ **Blocked**

The largest open risk in the project, and the only one whose answer changes the shape of an epic.
Timebox it, and write the finding down where someone can find it in six months.

- [ ] Four peers connect over the C# `SteamMultiplayerPeer` path and exchange an RPC in both
      directions, against **app id 480 — Spacewar, Valve's public test app**. It initialises the
      real Steamworks API and gives real SteamID64s and real P2P over Valve's relay, which is
      everything this spike asks about; nothing here is a question about *our* entitlement. A
      dedicated app id costs the Steam Direct fee and partner onboarding, and **waiting for
      that is what kept this story unstarted through week one** (corrected 2026-08-25).
- [ ] **Budget four Steam accounts on four machines.** This, not the app id, is what the story
      actually costs: Steam runs one client per machine, one account at a time. **Two peers on
      two machines still answers most of it** — whether the bindings work and against which
      fork — and that is the half ADR 0004 needs. Split the story rather than waiting for four
      boxes.
- [ ] A client disconnect and a host teardown are both survived.
- [ ] The finding names **the exact fork and commit** of the bindings that worked, and states
      whether the missing channels implementation matters at our packet volume and shape.
- [ ] A written recommendation: use it, fork and maintain it, or change E12's shape — and on a
      negative finding, **what E12 becomes**, not merely that it is a problem.
- [ ] Filed as the input to ADR 0004 (Arch §12).

**Blocked on hardware, not on a decision.** `SteamTransport` is a stub that throws rather than
falling back (asserted by `Steam_transport_refuses_rather_than_falling_back_to_enet`), and the
seam around it is finished, so nothing else in the build is waiting on this. What it needs is
Steam accounts on separate machines — this one has no Steam client installed. **Two peers on two
machines unblocks ADR 0004**, and that is the version to run.

*Blast radius is E12 and E18 only. The voice decision removed E7 from it (epics E7), which is the
entire reason that decision was worth making.*

#### E0-02 · `IGameTransport` and `EnetTransport`
**Depends:** E14-03 · **Test:** L2 · **Status:** ✅ **Done**

- [x] The interface is Arch §3.5's two methods. Not more — a transport that also knows about
      lobbies is E12 leaking downward.
- [x] ENet host and client both work locally.
- [x] **No file outside the two transport implementations names an ENet or a Steam type** — a grep
      in review proves it, and it is what makes E0-03 a drop-in rather than a migration.

#### E0-03 · `SteamTransport`
**Depends:** E0-01, E0-02 · **Test:** L2 + one manual four-peer check · **Status:** ⛔ **Blocked**

- [ ] Satisfies `IGameTransport` with no gameplay code change anywhere.
- [ ] Transport is selected by configuration: **ENet is the development and test default; Steam is
      the shipping default** (epics E0).
- [ ] Four peers exchange an intent RPC and a replicated value over Steam — manual, recorded once,
      because L3 runs against ENet forever.

#### E0-04 · Session lifecycle and the `SessionRoot` seam
**Depends:** E0-02 · **Test:** L2, later L3 · **Status:** ✅ **Done**

- [x] Host, join, leave and teardown all work over `IGameTransport`.
- [x] `SessionRoot._Ready` is **the only place in the codebase that constructs a domain system**,
      behind exactly one `Multiplayer.IsServer()` branch (Arch §3.2).
- [x] `HostSession` receives its systems as constructor arguments so L1 can substitute stubs; it
      never builds them internally.
- [x] `grep -rn "new ShiftDirector\|new ShiftLedger" src/` returns exactly one line, and that grep
      is in the review checklist because no test can catch the second one.

#### E0-05 · `MultiplayerSpawner` wrapper with a custom spawn function
**Depends:** E0-04 · **Test:** L2 · **Status:** ✅ **Done**

- [x] Spawning takes a small explicit args struct, never a whole record — the payload is a
      deliberate decision at every call site (Arch §5.2). `NetworkSpawner.Payload` is `[key,
      args]` and nothing else, asserted.
- [x] A client builds a node from spawn args alone, with no additional round trip. The builder
      is a closure over nothing; **the cross-process half is E2-04's**, which inspects the
      serialised bytes — one process cannot prove a client had no second source.
- [x] Adding a spawnable type requires no change to the wrapper. Proved with two types
      registered in the same test.

#### E0-06 · Replication classes — synchronizer configuration
**Depends:** E0-04 · **Test:** L2 · **Status:** ✅ **Done**

The mechanism only. Parcels start using it in E2-05.

- [x] Three named configurations exist with **distinct `replication_interval` values**, set per
      class rather than globally (Arch §3.4). `Dynamic` 1/30 · `Railed` 0 · `Sleeping` 3600.
      Railed's zero is not "off" but "look every tick": its rail tuple is *watched*, so the
      interval governs how fast a change is noticed, and nothing changes after entry. Sleeping's
      hour is a deliberate absurdity — if a property is ever left on `Always` by mistake the
      result is one stray packet an hour, not a silent 30 Hz stream from every parcel at rest.
- [x] A node can be promoted and demoted between classes at runtime without respawning. Both
      directions asserted, with the node's and synchronizer's instance ids unchanged.
- [x] Every RPC this story introduces states `TransferMode` and `CallLocal` deliberately.
      **This story introduces no RPC** — everything rides `MultiplayerSynchronizer`. Recorded
      rather than skipped, because "no RPCs were added" and "nobody thought about the RPC
      defaults" look identical in a diff.

#### E0-07 · `LatencyPeer` development decorator
**Depends:** E0-02 · **Test:** L2 · **Status:** ✅ **Done**

Vision §15's question says *over real internet*. Without this, the MVP answers an easier question
than the one that matters — which makes this **required infrastructure, not a nicety** (Arch §3.5).

- [x] Configurable fixed delay plus jitter, wrapping any `MultiplayerPeer`. It delays what
      **arrives**, so a round trip costs twice the setting. Two things are worth knowing:
      the ENet **handshake is not delayed** (connection completes below the packet API this
      decorates), and **only `Unreliable` packets may reorder** — re-breaking an order ENet has
      already guaranteed would simulate a bug that cannot happen and tear scene replication
      apart doing it.
- [x] Enabled by flag, and **impossible to enable in a shipping build** — an export guard or a
      startup assertion, not a convention. `WrapIfConfigured` throws in a non-debug build. The
      rule is a separate pure predicate so it can be asserted from all four directions; the case
      that matters only exists inside a release export, where no test can stand.
- [x] Carries Arch §3.5's `ponytail:` comment verbatim: ceiling *and* upgrade path.

#### E0-08 · L3 harness feasibility spike
**Depends:** E14-05, E0-02 · **Test:** spike · **Answers:** epics open item 5, Arch open item 4 · **Status:** ✅ **Done**

- [x] A written answer to **four processes or four `SceneTree`s**, with a working proof of the one
      chosen.
- [x] The wall-clock cost of a trivial four-peer connect test is measured — a suite that takes
      twenty minutes is a suite that gets skipped, and therefore is not a suite.
- [x] The chosen approach runs headless with no GPU on CI hardware, not only on a developer
      machine with a display attached.

#### E0-09 · The `Dlo.Net.Tests` L3 harness
**Depends:** E0-08, E0-04 · **Test:** L3 · **Status:** ✅ **Done**

- [x] Boots a headless host and three headless clients over `EnetTransport`.
- [x] Asserts an intent RPC arrives and a replicated value converges.
- [x] Tears down leaving **no orphaned processes**. A leaked peer poisons the next run and
      presents as flakiness, which is how a suite loses its credibility.
- [x] A failure message names **which peer disagreed and what it held**. An L3 failure that says
      only `Assert.Equal failed` costs an hour every single time it fires.
- [x] Wired into CI on merge to main (Arch §10.6).

#### E0-10 · Disconnect and host-loss teardown
**Depends:** E0-09 · **Test:** L3 · **Status:** ✅ **Done**

- [x] A client disconnecting leaves the host and the remaining clients running, with no orphaned
      nodes and no exceptions.
- [x] A host teardown ends every client's session cleanly.
- [x] Both asserted at L3, including that the survivors **keep functioning afterwards** — "did not
      crash" is not the assertion; "still works" is.

*This story stops at "the session ended cleanly." The player-facing message and the return to
lobby are E12-05 — see the gap recorded about this split.*

---

### E1 — Embodiment

*A controller that is tight, in a world that is not.* **Gate 0 lives here.**

#### E1-01 · Jolt joint stability spike — two-person carry
**Depends:** E14-03 · **Test:** spike, leaving one L2 regression scene · **Answers:** epics open · **Status:** ✅ **Done**
item 4, Arch open item 3

Do this before E1-04, not before E1-08. If heavy bodies on joints are unstable, the *grab* design
changes, not just the co-op carry.

**Reported 2026-08-25. The risk it was written to find does not exist; a different one does.**
124 configurations measured at Jolt's defaults. Full finding in Arch §11.

- [x] A heavy body held by two joints from two characters does not explode, jitter or tunnel at
      Jolt's default substep count. **Confirmed to 500 kg** — no explosion, no tunnelling, no
      NaN, on rigid or sprung joints. `velocity_steps = 10` / `position_steps = 2` need no
      change, and E1-08's two-joint design is safe as drawn.
- [x] The finding names **the mass and stiffness envelope that stays stable**, and what happens
      outside it. **Sprung `Generic6DofJoint3D` at stiffness ≈ 100 × mass**, damping ≥ 100 —
      ~5 cm of sag at every mass tested. Below ~50 × mass it oscillates, then sags out of the
      carriers' hands entirely. Above ~1000 × mass it is rigid again.
- [x] If it is unstable, the finding names the fix. **It is not.** But two things change the
      design, and both land *before* E1-04 rather than during it:
      **(a) a rigid joint expresses no weight** — two rigid pins to kinematic hands hold any
      mass perfectly and the parcel simply follows, which is the opposite of vision §3.1;
      **(b) `PinJoint3D.impulse_clamp` is unimplemented under Jolt** — the engine logs that it
      is ignoring the value. The only compliance Jolt honours is the 6DOF linear spring.
- [x] One L2 scene is kept as a regression check, so a physics-settings change that breaks this
      fails a test rather than a playtest. **`JointStabilityTests`**, which guards the solver
      settings as well as the carry — proved necessary: detuning to `velocity_steps = 2` leaves
      the carry visibly fine while invalidating every number above.

#### E1-02 · First-person controller — move, look, jump, crouch
**Depends:** E14-03 · **Test:** L2 · **Status:** ✅ **Done**

- [x] Move, look, jump and crouch are local and immediate on all four machines: **0 frames of
      network wait** (Arch §8), measured rather than assumed. Every test asserts the result of a
      *single* `Step` or `Look` call with no peer present at all.
- [x] **No input damping anywhere.** Two calls of `Look` turn exactly twice as far as one;
      movement reaches full speed on the first frame **and stops on the frame the key is
      released** — the forgotten half, since a body that accelerates instantly and glides to a
      halt is still damped. Crouch moves the head in one step, both ways.
- [x] The character body's multiplayer authority is the owning peer. **This is now the ruling**
      (gap 1, settled 2026-08-25), not an assumption.

*The authority test initially passed with the guard deleted — with no keys held, a `Step` that
ran wrote back the velocity the body already had. It now starts from a sideways velocity that
only a `Step` which ran would clear. Standards §8's "a test that cannot fail is worse than no
test", caught by the break check rather than by reading.*

#### E1-03 · Hands and IK
**Depends:** E1-02 · **Test:** L2 · **Status:** ✅ **Done**

- [x] Godot 4.7's `TwoBoneIK3D` / `FABRIK3D` is used **before** any procedural arm code is written
      (`AGENTS.md` rung 3, Standards §10).
- [x] Hands visibly reach the grip point of the held object on every peer, not only the holder's.
- [x] Hand pose is **derived** from held-object plus character state, never replicated per frame.
      Hands are the easiest accidental bandwidth leak in the build. Asserted from both ends:
      `Nothing_in_the_arms_replicates_anything` counts synchronizers in the arms subtree (proved by
      adding one and watching it go red), and `Two_peers_arms_reach_the_same_place_from_the_same_facts`
      shows the pose is a pure function of load and slot — which, with E1-06's proof that the holder
      map agrees across four processes, is the "on every peer" half.

*`TwoBoneIK3D` exists in 4.7.2 and is the right fit — a shoulder/elbow/hand chain is literally two
bones. It sits in a family (`FABRIK3D`, `CCDIK3D`, `ChainIK3D`, `JacobianIK3D`, `SplineIK3D`), all
`SkeletonModifier3D`s, and a modifier only runs as a child of the `Skeleton3D` it drives — parented
anywhere else it silently does nothing. `CarryArmsTests` asserts that parenting for that reason.*

#### E1-04 · The grab joint, host-side
**Depends:** E1-01, E1-03, E0-04 · **Test:** L2 · **Status:** ✅ **Done**

- [x] The real joint exists **only on the host**. A client never creates a physics joint (Arch §3.3).
- [x] `RequestGrab` validates range, current holder and policy lock before resolving.
- [x] `GrabResolved` names the winning holder and is `Reliable` — it is a decision, not a stream.
- [x] Weight is expressed **only** through Jolt mass and joint compliance — which E1-01 has
      now reduced to one mechanism: a `Generic6DofJoint3D` linear spring at stiffness ≈ 100 ×
      mass, damping ≥ 100. **Not a `PinJoint3D`**: Jolt ignores its `impulse_clamp`, and a
      rigid pin to a kinematic hand carries any mass weightlessly. Asserted on the joint itself,
      to the number, in `The_grip_is_the_spring_E1_01_measured_and_never_a_pin`.

**The lift had to be made explicit, and E1-01 could not have found this.** A 6DOF linear spring
drives the offset its two bodies had *at attach time* toward equilibrium — so a spring built while
the box is on the floor holds it **on the floor**, however stiff it is. The spike measured a box
already in the air, so its whole finding is about *holding*; lifting is a separate step. The host
therefore places a fully-crewed load into the carry pose and then builds the grips (`GrabDirector.Crew`).

*That lift is a one-frame snap, and it carries a `ponytail:` naming the ceiling — a visible ~1.9 m
teleport in the L3 rig — and the upgrade: move the hand to the grip, joint there, animate the hand
back, and let the spring drag the load up. It is deliberately left for **Gate 0 to judge**, because
"awkward or broken" is exactly the word E1-10 collects.*

#### E1-05 · Optimistic client attach
**Depends:** E1-04 · **Test:** L2, confirmed at L3 by E1-06 · **Status:** ✅ **Done**

- [x] Hand animation and a **visual-only** attachment happen on the same frame as the button press,
      at any latency — including under `LatencyPeer` at 200 ms.
- [x] On denial the parcel snaps out of the hands: no error state, no stuck animation, no
      re-request loop.
- [x] A comment states this is **the only optimistic path in the build** and why, citing Arch §3.3,
      so the next developer does not generalise it to stamping, opening or incinerating — where a
      rollback would un-decide something the report already recorded.

**"Visual-only" turned out to be load-bearing, not a turn of phrase.** The first version predicted by
moving the parcel *body*, which is the property replication owns — so the parcel flipped between the
predicted hand and the authority's position on every packet, about a metre, several times a second.
It now offsets a `Carryable.Visual` child instead: the body stays exactly where the host put it,
nothing contends, and no freeze is needed. `Carryable.Visual` exists for that reason and is never
replicated.

**Two smaller corrections, both measured:**

- **The host is asked before the prediction is made.** `Grab` does not block — on a client it posts
  an RPC and returns — so nothing waits, but predicting *first* moved the load and the host then
  validated range against the position the prediction had just invented. A grab from across the room
  passed its own range check.
- **A loser lets go on `GrabResolved` naming someone else**, not on waiting for its own
  `GrabRefused`. Earliest honest moment, and it is what stops the flip described above.

#### E1-06 · Grab contention resolution
**Depends:** E1-05, E0-09 · **Test:** L3 · **Status:** ✅ **Done**

- [x] Two clients grabbing the same parcel in the same frame resolve to **exactly one** holder
      (Arch §10.4).
- [x] The loser's client releases cleanly, with no orphaned visual joint.
- [x] The loser **sees the parcel move toward the winner** — it does not teleport, and it does not
      end up inside geometry. The mispredicted case is supposed to read as *a teammate yanked it
      away*, and that only works if it looks like that. The rollback eases over 15 frames
      (`GrabPredictor.SlipFrames`); measured convergence is **0.018 m** from the host's position and
      a largest single-frame move of **0.058 m**, against 0.36–1.24 m before the prediction was made
      visual-only.

*New L3 scenario `contention`, alongside `converge`, `departure` and `hostloss`. The host releases
all three clients with one replicated signal and each answers whether it won, so the run turns on
the host's ordering rather than on a timer — the first version used one and the host outlived the
clients by nothing at all, which read as "nobody won" on a run where somebody plainly had.*

*A client must not simulate a body it does not own. A `RigidBody3D` that is both locally simulated
and transform-replicated fights itself — position is sent, velocity is not, so a client's copy
accelerates downward and settles ~0.25 m below the host's. The harness freezes the parcel on
non-authority peers; **E2-05's replication classes are what answer this properly** (arch §3.4).*

#### E1-07 · Carry and throw
**Depends:** E1-04 · **Test:** L2 · **Status:** ✅ **Done**

- [x] Carrying something bulky obstructs vision and movement **through the object's own geometry
      and mass**, never through an input modifier. Vision: a ray from the head hits the box.
      Movement: `PlayerCharacter.CarryPull` returns the grip spring's own reaction, so it is zero
      while the load rests in the hands and grows as the load lags — and the suite asserts `Speed`
      is untouched, which is the line between this and the modifier arch §6.1 bans. Gravity sag is
      deliberately not charged as a pull; it would be a permanent downward tug.
- [x] Throw impulse derives from mass, so a heavy parcel is a bad projectile without a special case.
- [x] Dropping is always available and never blocked by a state machine. A player who cannot let go
      is a player fighting the input.

*Until E2-05 exists, thrown parcels are ordinary dynamic bodies. Replication-class behaviour
arrives there, not here.*

#### E1-08 · Two-person cooperative carry
**Depends:** E1-01, E1-04 · **Test:** L2, later L3 · **Status:** ✅ **Done**

- [x] An object the domain marks as over one-person capacity **cannot** be lifted by one player and
      **can** be by two — two host-owned joints on one body (Arch §3.3).

**A weaker grip cannot refuse a lift, and that is measured.** The tempting design — give each carrier
a fraction of the stiffness so one is not enough — does not work: a linear spring's force grows
without bound with stretch, so halving it only buys more sag. A lone carrier on a two-person 50 kg
box at half stiffness still lifted it to within 21 cm of their hand. Jolt offers no force cap to fix
that (`PARAM_LINEAR_SPRING` is stiffness, damping and equilibrium, and E1-01 already found
`impulse_clamp` unimplemented). **So an under-crewed load is frozen**, and the code says so rather
than dressing a flag as physics. Each grip stays at E1-01's measured 100 × mass — dividing it would
also have put the build outside the envelope `JointStabilityTests` guards.
- [x] The configuration stays inside E1-01's stable envelope.
- [x] When one carrier lets go, the object **drops**. It does not launch, and it does not teleport
      to the remaining carrier. **E1-01 measured what makes it launch:** grip stiffness. At
      ≈ 100 × mass the release speed is ~1.4 m/s (a drop); in the over-stiff band it reaches
      10 m/s (a launch). If this criterion fails, the grip is too stiff — it is not a bug in
      the release path.

*Capacity is a domain fact. Until `ParcelRecord` exists (E2-01) it lives on a placeholder body;
moving it there is a rename, and E2-01 owns it.*

#### E1-09 · Stumble and trip
**Depends:** E1-07 · **Test:** L2 · **Status:** ✅ **Done**

- [x] Stumbling is caused by **the world** — uneven floor, a collision, an oversized load — never by
      a random timer or an input lockout.
- [x] Recovery is immediate and controllable. A stumble that takes control away for a second is
      unresponsive input wearing a costume, which is exactly the failure vision §3.1 names.

*The world's shove is **added** to input, never scaling it: `A_stagger_never_costs_the_player_any_authority`
asserts that a staggered body still reaches full walk speed on the frame it is asked to, and it fails
if input is multiplied by anything at all. `A_stagger_can_be_steered_against_on_the_very_next_frame`
covers the other half. There is no timer, no lockout and no flag — `IsImpact` refuses to stagger a
body that is already spending one, which is what stops it shoving itself into a wall and staggering
off its own stagger.*

#### E1-10 · **Gate 0 — the feel session**
**Depends:** E1-07, E1-09, E15-04, E19-02 · **Test:** playtest, not a suite (Arch §10.3) · **Status:** ⛔ **Blocked**

- [ ] One player, local, picks up, carries, throws and drops a heavy awkward box.
- [ ] Their words are recorded **verbatim**. The finding is which word arrives unprompted:
      *awkward* or *broken*. Not a score out of five.
- [ ] A written go/no-go, filed in E19-06's register.
- [ ] **On a fail, E1 is fixed before any other feature work starts** (epics gate table). A pass
      here does not authorise Gate 1's scope.

**Every E1 story it depends on is done; the gate itself cannot be coded.** It needs a person who has
not seen the game, and its two remaining dependencies are outside E1: **E15-04** (look sensitivity,
invert-Y and FOV — a participant who cannot set their own sensitivity is testing your mouse, not
your grab feel) and **E19-01 → E19-02** (the protocol, and an instrument that names in advance what
a fail looks like, so the result is not renegotiated afterwards).

**Two things are already waiting for its verdict**, and both are recorded rather than pre-empted:
the one-frame grab snap (E1-04's `ponytail:`), and whether a stumble should be more than a shove.
Deciding either before the session would be deciding it without the only evidence that counts.

---

## Tier 1 — The Job

### E2 — The Parcel

*A data carrier with a physical body, whose identity outlives both.*

#### E2-01 · `ParcelId`, `ParcelRecord`, `ParcelRegistry`
**Depends:** E14-04 · **Test:** L1 · **Status:** ✅ **Done**

- [x] `ParcelId` is a host-assigned `readonly record struct` over a `uint` (Arch §5.1). Ids count
      from one, so a `default` `ParcelId` names no parcel instead of the first one registered —
      proved by breaking it, which reddens exactly one test and no others.
- [x] `ParcelRegistry` maps id → record and is **the only owner of parcel state**. It assigns every
      id, and it never forgets one, because the report is built at the whistle (Vision §7). It is
      constructed at the one site (Arch §3.2) and the review-checklist grep now names it.
- [x] **No gameplay state on any node** — a node is a view of a record (Standards §12). Closed by
      E2-04: capacity is **derived** from the size byte through `ParcelRecord.CarriersRequiredFor`,
      so host and client compute it rather than storing it, and the policy lock left the node
      entirely — it is host-only on the record, and `GrabDirector` reads it from the registry.
      E2-06's recycle test is what proves a node keeps nothing.
- [x] XML docs on every public Domain member, with `<see cref="..."/>` that resolves so the Game
      layer gets working hover text. Verified by the compiler rather than by eye: Domain builds
      clean under `-p:GenerateDocumentationFile=true`, where a missing doc is CS1591 and an
      unresolved `cref` is CS1574, and Domain treats warnings as errors.
- [x] No Godot type in any signature, `Vector3` included — Domain has its own `Vec3` (Standards §0).
      The architecture test now runs Arch §10.5's assertion **verbatim** — `typeof(ParcelRecord)`,
      which stood in as `Vec3` until this story.

#### E2-02 · Identity survives the node being freed
**Depends:** E2-01, E0-05 · **Test:** L2 · **Status:** ✅ **Done**

- [x] Freeing a parcel's node leaves the record intact and addressable by `ParcelId`.
- [x] Re-spawning from that id restores manifest, tamper state and culpability. **What exists is
      restored** — id, archetype, size, condition and derived capacity, all rebuilt out of the
      registry by a node that never met the old one. The three the criterion names do not exist on
      `ParcelRecord` yet (E2-03, E2-07, and Arch §4.6's `ActorRef`), so the mechanism is what is
      proved; the test carries a `ponytail:` saying each story must add its field to that assertion.
- [x] The test **proves the node was actually freed**, not merely hidden — `IsInstanceValid` is
      false before anything else is asserted, so a hidden-but-alive node fails here rather than
      passing for the wrong reason forever.

#### E2-03 · The manifest model
**Depends:** E2-01, E13-02 · **Test:** L1 · **Status:** ✅ **Done**

Manifest, address, destination code, weight, fragility, declared contents.

- [x] Every field is a Domain type, and the whole model serialises to a payload **containing no
      engine type** (Standards §9). Asserted structurally rather than promised: a test walks
      `Manifest`'s properties and fails on any type that is neither a primitive nor from
      `Dlo.Domain`. The architecture test guards the assembly; this guards the shape.
- [x] An address either parses to a routable destination or is **rejected at content load**
      (E13-02's grammar) — never discovered at routing time, mid-shift.
- [x] Variation is data. Adding an archetype does not add a class (Arch §4.1). `Archetype`,
      `Size` and `Condition` are bytes from spawn args and `DeclaredContents` is an authored code,
      so a new kind of parcel is a new `.tres`. **`Destination` is derived from the address**
      rather than stored beside it, because two copies are two things to keep in step.

#### E2-04 · Spawn args and the custom spawn function
**Depends:** E2-01, E0-05 · **Test:** L2 + L3 negative · **Status:** ✅ **Done**

- [x] `ParcelSpawnArgs(uint Id, byte Archetype, byte Size, byte Condition)` — Arch §5.2's shape,
      in Domain, and an L1 test asserts those four members and no others so E2-03 cannot quietly
      add a fifth.
- [x] **The manifest is not in the payload**, and a negative test inspects the serialised bytes to
      prove it. There is no manifest yet, so the assertion available today is the stronger one it
      will become: two records differing **only** in their policy lock serialise to byte-identical
      payloads, and the round-tripped payload is four numbers.
- [x] A client builds a box that looks right — correct size, archetype, visible condition — from
      spawn args alone. Asserted through `VarToBytes`/`BytesToVar`, so the builder cannot cheat by
      reading a record the wire never carried. Capacity comes for free: both sides derive it from
      the size byte, so nothing extra travels.

#### E2-05 · Three replication classes, with promotion and demotion
**Depends:** E2-04, E0-06, E4-01 · **Test:** L2 + L3

- [ ] `Railed` sends `(beltId, distanceAlong, lane)` **once, on entry**, and produces zero ongoing
      traffic; clients extrapolate (Arch §3.4).
- [ ] `Dynamic` replicates transform as `UnreliableOrdered`.
- [ ] `Sleeping` sends one final transform when Jolt reports sleep, then nothing at all.
- [ ] Knocking a railed parcel off the belt promotes it; letting it settle demotes it — **both
      directions asserted.**
- [ ] **A belt parcel in `Dynamic` fails a test.** Arch §3.4 calls that a bug against the section,
      so it earns an assertion rather than a code review.

#### E2-06 · Object pooling
**Depends:** E2-02 · **Test:** L2 · **Status:** ✅ **Done**

- [x] Parcels are pooled, never `QueueFree`d (Standards §10). A released body stays valid, parked
      and invisible, and is taken back under the pool if it was released from the world.
- [x] A recycled node carries **nothing** over — proved by mutating every mutable field before
      release and asserting defaults after acquire. Proved a second time by breaking `Clear`, which
      reddens the test rather than leaving it green.
- [x] Pool growth is bounded and logged. `MaxParcels` is a hard cap that throws rather than growing
      quietly, and every growth prints the new total. **The cap is asserted; the print is not** —
      and the cap carries a `ponytail:` because **E4-01's accumulating belt** is the first thing
      that can reach it, and that story has to choose between raising it and giving the belt an end.

#### E2-07 · Openable parcels and tamper state *(post-MVP)*
**Depends:** E2-03 · **Test:** L1 + L2

- [ ] `RequestOpen` is host-validated and **never optimistic** — it is irreversible and it is a
      policy violation (Arch §5.4). The grab exception does not extend here.
- [ ] Tamper state is permanent, and survives tube transit, pooling and node recycling.
- [ ] Opening writes a ledger entry carrying the actor.

#### E2-08 · Declared versus actual contents *(post-MVP)*
**Depends:** E2-07 · **Test:** L1 + L3 negative

- [ ] `ActualContents` exists on the record from authoring time.
- [ ] **No client receives it before the box is open** — an L3 anti-assertion, and the easiest kind
      of thing in this design to regress silently (Standards §8).
- [ ] A declaration/reality mismatch is representable and detectable **without a new class**.

#### E2-09 · L3 parcel identity test
**Depends:** E2-05, E4-03, E0-09 · **Test:** L3

- [ ] belt → grab → throw → tube → respawn preserves `ParcelId`, manifest and tamper state.
- [ ] **Three separate assertions**, not one conjunction — a chained assert reports one useless fact
      (Standards §8).
- [ ] The test fails if node-held state is reintroduced anywhere along the chain.

#### E2-10 · Replication budget measurement
**Depends:** E2-05 · **Test:** measurement · **Answers:** epics open item 3, Arch open item 2

- [ ] 40 awake parcel bodies plus a full belt, measured against Arch §8's **60 KB/s gameplay**
      budget.
- [ ] The number is **recorded in the repo** with the scenario that produced it, so the next
      measurement is a comparison rather than a fresh argument.
- [ ] If it exceeds budget, the finding names **which replication class is misbehaving**. It does
      not propose a redesign — that is a separate conversation with the owner.

---

### E3 — The Work

*Receive → inspect → scan → stamp → route, across four posts.* **Gate 2 lives here.**

#### E3-01 · The post model
**Depends:** E2-01, E4-05 · **Test:** L1 + L2

- [ ] A post is an authored volume plus **the information class it grants** — not four hard-coded
      classes (Arch §4.1, E13).
- [ ] The host knows which post each player currently occupies, and that fact is what drives E3-06.
- [ ] Working two posts at once is impossible by geometry, and E4-05 owns proving it.

*Read the gap on what a post physically is before starting this one.*

#### E3-02 · The scanner
**Depends:** E3-01, E2-03 · **Test:** L2 + L3

- [ ] Scanning is a `Request*` intent and the host decides (Standards §6).
- [ ] A successful scan grants **the scanning client** the manifest and sets `HasBeenScanned`.
- [ ] Scanning is what makes E3-06's filter observable, so this story and that one are verified
      together or neither is verified at all.

#### E3-03 · The stamp tool, including `INCINERATE`
**Depends:** E3-01 · **Test:** L1 + L2

- [ ] Stamping is **never optimistic** — it is a decision the report will record (epics E3).
- [ ] Every stamp writes a ledger entry with actor and parcel.
- [ ] The stamp is visible on the body to every peer: blame has to be legible in the moment as well
      as on the report.
- [ ] `INCINERATE` is available from the first build with **no confirmation dialog**. It is
      deliberately available before it is wise (vision §3.5), and a safety prompt deletes the joke.

#### E3-04 · `RoutingRules.Evaluate` and the L1 policy matrix
**Depends:** E2-03, E13-03 · **Test:** L1

The single most-tested function in the codebase, and the reason "did the shift score correctly?"
never requires four peers and a controller.

- [ ] Signature exactly `Evaluate(ParcelRecord, ChuteId, PolicyState)` — pure: no clock, no engine,
      no side effects (Arch §4.5).
- [ ] The matrix covers correct route · misroute · unknown destination · a destination whose chute
      changed mid-shift · a parcel with no destination · a policy mapping one destination to two
      chutes, which E13-03 should already make impossible (assert both ends of that).
- [ ] `Misroute_is_not_revealed_until_the_whistle` exists and passes. **No live wrong-chute
      indicator** — the delayed reveal is the blame engine's ammunition (Arch §4.4).
- [ ] Nothing caches a routing answer across a `PolicyState` change (Standards §12).

#### E3-05 · `PolicyState` and the routing chart
**Depends:** E3-04, E15-02 · **Test:** L1 + L2

- [ ] `PolicyState` is mutable and replicated, and the chart is **a rendering of it** — not a
      parallel copy that has to be kept in step.
- [ ] Every destination maps to exactly one chute, or `ContentTool validate` fails the build
      (E13-03).
- [ ] The chart is legible from the far wall. E15-02 owns that check; this story owns the data.

#### E3-06 · Per-post replication filtering
**Depends:** E3-01, E3-02, E2-04 · **Test:** L3

The mechanism that makes voice load-bearing. It is a **network** property, never UI gating — a UI
lie is one a client can trivially see through, and it means every client already holds every
manifest (Arch §5.3).

- [ ] The host does not send a manifest to a client that has not earned it.
- [ ] The filter is applied at the point of **sending**, not at the point of rendering.
- [ ] Each of Arch §5.3's four rows is expressed as data, so a fifth post is content rather than a
      code change.

#### E3-07 · L3 manifest anti-assertion
**Depends:** E3-06, E0-09 · **Test:** L3

- [ ] A client that has not scanned parcel X **provably never receives** X's manifest — the test
      inspects the client's received state, not its UI.
- [ ] **Proved by removal:** delete the filter, watch the test go red, restore it. Standards §8 is
      explicit that this failure is silent, and silence is why it earns the harshest check in the
      suite. Nothing looks broken when a client knows too much.
- [ ] A late joiner is covered, or E12-03 reopens the hole from the other side.

#### E3-08 · Mid-shift policy change
**Depends:** E3-05 · **Test:** L1 + L3

The antagonist landing a hit, expressed as a data change.

- [ ] A policy change updates `PolicyState` and every client's chart within one replication
      interval.
- [ ] The crew is **not** told what changed beyond what the PA and the chart say. Silent staleness
      is the point of the feature.
- [ ] The policy applied to a parcel is the one in force **when it entered the chute** — read the
      gap on in-flight parcels first, and do not settle it privately inside this story.

#### E3-09 · **Gate 2 — the four-player job session**
**Depends:** E3-06, E4-05, E12-01, E19-05 · **Test:** playtest (Arch §10.3)

- [ ] Four players complete parcels correctly, and **cannot** do so from one position — observed,
      not assumed.
- [ ] The instrument answers vision Q3's actual question: does any player report time pressure from
      **scrutiny**, or only from **volume**?
- [ ] Whether the crew separates to four posts or clumps into a blob is recorded (vision §8).
- [ ] A written go/no-go on the asymmetric-information design.
- [ ] **A dilution finding triggers E3-10, not a design reopening** (epics E3, E19).

#### E3-10 · Mandated post rotation — **held fix, do not build**
**Depends:** E3-09 reporting dilution · **Test:** L1 when authorised

Pre-approved and deliberately unbuilt, so that a gate failure costs one story rather than a
redesign.

- [ ] Not started unless Gate 2 finds dilution.
- [ ] When built: management requires staff to rotate stations mid-shift, announced over the PA — a
      policy plus a PA line, nothing more.
- [ ] It must not disturb the voice loop: a player still holds only their **current** post's
      information, which E3-06 already guarantees provided rotation goes through the same path.

---

### E4 — The Facility

*A believable postal facility whose architecture already leaves room for something stranger.*

#### E4-01 · Conveyors and rails
**Depends:** E14-03, E0-06 · **Test:** L2 · **Status:** ✅ **Done**

The mechanism that makes "the belt never stops" affordable.

- [x] A parcel entering a belt becomes `Railed`: a spline, a speed and a lane, sent once (Arch §3.4).
      `Conveyor.Accept` writes `Carryable.Rail` — `(beltId, distanceAlong, lane)` — exactly once and
      applies the `Railed` class. **The running distance lives on the conveyor, not on the parcel**,
      because the tuple is a watched property and advancing it there would turn the cheapest class
      into the most expensive one.
- [x] Clients extrapolate with **no ongoing traffic** — asserted by measuring traffic, not by
      reading the code. `ReplicationMeter` reads the synchronizer's own config, samples the values
      it would send, and reports changes, bytes and streaming properties. Over 30 frames of a
      moving railed parcel: **0 changes, 0 bytes, 0 streaming**, with the parcel's travel asserted
      in the same test so a stationary parcel cannot pass it. A loose parcel is metered alongside
      as the control, so a broken instrument reads as a failure rather than as a pass.
- [x] **The belt does not stop and does not despawn its backlog.** Parcels accumulate at the end,
      because accumulation is the design keystone (vision §2), not an overflow condition to handle.
      Four parcels run to the end, all four are still carried, the front one pins to the belt's
      length, and each queues exactly `Spacing` behind the next. Proved by breaking it: removing
      the spacing ceiling reddens the test.
- [x] Carries Arch §2's `ponytail:` comment on constant-speed extrapolation verbatim, with its
      ceiling and its upgrade path. A second `ponytail:` names the adoption scan's cost, which is
      how a client picks a parcel up from the tuple alone with no second message.

#### E4-02 · Chutes
**Depends:** E4-01, E3-04 · **Test:** L2 + L1

- [ ] A chute reports *"parcel N entered me"* to the host and **decides nothing** (Arch §1). Physics
      proposes; the domain disposes.
- [ ] The routing outcome is recorded silently. **No live wrong-chute indicator** (Arch §4.4).
- [ ] Jam state is representable; E6 fills in what causes one.

#### E4-03 · Pneumatic tubes
**Depends:** E4-01, E2-02 · **Test:** L2 + L3

- [ ] A parcel in transit is `(tubeId, eta)` and **may have no body at all** — its node may be freed
      entirely (Arch §3.4).
- [ ] Arrival rebuilds a node from the record with identity, manifest and tamper state intact
      (E2-09 asserts this end to end).
- [ ] **No transform replication during transit**, proved by traffic measurement rather than by
      inspection.

#### E4-04 · Doors
**Depends:** E14-03 · **Test:** L2 · **Status:** ✅ **Done**

- [x] Open/closed state is host-authoritative. `Open` and `Shut` are no-ops off the host, and
      `IsOpen` is the **only** replicated fact about a door — the leaf's position is derived from
      it on every peer. Metered: a full open costs **one change and nothing streaming**, which is
      arch §3.4's reasoning applied outside the belt.
- [x] A door can be added, moved or removed by E9's mutations **without a new class** (Arch §4.1).
      Travel, speed and opening size are exports, so a sliding hatch and a rising shutter are the
      same type configured differently — asserted, along with removing one mid-travel while the
      rest of the facility keeps running.
- [x] A door cannot permanently trap a player; invalid state recovers rather than disabling the node
      forever (Standards §10). A body in the doorway makes the host reopen on the next frame, and
      **the recovery is not a latch** — it shuts normally once they step out. Proved by breaking
      it. A non-finite `Openness` is walked back into range rather than sticking, because the
      failure this criterion is really about is a door nobody can open again.

#### E4-05 · Layer 2 greybox
**Depends:** E4-01, E4-02, E4-04 · **Test:** playtest evidence, no suite

- [ ] ~30×24 m plus adjacent rooms (vision §8, §12).
- [ ] The four posts are far enough apart that one player cannot work two — **measured in
      walk-seconds**, not in metres, and recorded so E3-09's observation has a baseline.
- [ ] Four players at four posts complete a parcel without a traversal complaint. The distance is
      there to force talking, not to force jogging.

#### E4-06 · Layer 1 greybox *(post-MVP)*
**Depends:** E4-05 · **Test:** build only

- [ ] Lobby, counter, PO boxes, staff door. **Believable post office first, uncanny second**
      (vision §3.3) — this is the layer that sets that expectation, so it is authored straight.

#### E4-07 · Layer 3 stub *(post-MVP)*
**Depends:** E4-05 · **Test:** build only

- [ ] An expansion surface exists, gated by wrongness rather than by shift number (vision §12).
- [ ] **Nothing that ships depends on it.** That is the whole acceptance criterion.

#### E4-08 · Signage as data
**Depends:** E13-04 · **Test:** L1 via `ContentTool`

- [ ] Signage is a data table from day one, **never baked into geometry** (Arch §7).
- [ ] A sign referencing a destination that does not exist **fails the build**.
- [ ] The schema carries the fields E15-03's colourblind-safe audit needs, so that audit is a check
      rather than a migration.

#### E4-09 · Navigation *(post-MVP)*
**Depends:** E4-05 · **Test:** L2

Nothing navigates until E6's live contents exist. This story is recorded so the trigger has a name,
not so it gets built early (`AGENTS.md` rung 1).

- [ ] A navmesh covers Layer 2.
- [ ] Rebuilding after a geometry change is **one call**, because E9 mutates geometry at shift start
      and navigation must follow it identically on all four peers.

#### E4-10 · Full-belt replication cost measurement
**Depends:** E4-01, E2-10 · **Test:** measurement · **Answers:** epics open item 3

- [ ] A full belt measured against Arch §8, and **E4 is not done until the number is recorded** —
      either a pass, or a documented overage with a named cause (epics E4 DoD).

---

## Alongside — the slices Gate 1 depends on

*These epics run from Tier 1 onward. Only the stories the MVP line actually needs are decomposed
here; the rest of each epic stays at the epic level.*

### E12 — Session Ops

#### E12-01 · Lobby
**Depends:** E0-04 · **Test:** L2 + L3

- [ ] A host creates a session; up to three clients join; the lobby shows who is connected.
- [ ] Starting the shift transitions all four peers together.
- [ ] Works over ENet in development and Steam in a shipping build, with no gameplay difference
      between them.

*The lobby is MVP scope because Gate 1 needs it. The rest of this epic is not (epics E12).*

#### E12-02 · One-click Steam invite
**Depends:** E0-01, E0-03, E12-01 · **Test:** manual

- [ ] A Steam invite puts a friend in the game **with no address typed**.
- [ ] No custom matchmaking, now or ever — Steam friends and invites only (vision §16).

#### E12-03 · Join in progress
**Depends:** E12-01, E3-06 · **Test:** L3

- [ ] A client joining mid-shift receives current parcel records **at their post's information
      level**. A late joiner is the easiest place to accidentally hand out every manifest, and
      E3-07's anti-assertion must cover this path explicitly.
- [ ] The shift is not paused, reset or otherwise disturbed by the join.

#### E12-04 · Leave handling
**Depends:** E12-01 · **Test:** L3

- [ ] A client leaving mid-shift does not corrupt the shift.
- [ ] Their held parcels **drop** — they do not vanish, and they do not stay frozen in a
      disconnected hand.
- [ ] **Their ledger entries survive.** The report still names them (vision §7). A leaver whose
      blame evaporates has learned they can escape the report, which is the one lesson this game
      must not teach.

#### E12-05 · Graceful host loss
**Depends:** E0-10, E12-01 · **Test:** L3

- [ ] The host leaving ends the shift cleanly on every peer, with an honest message. **No host
      migration** — it is explicitly not in scope (epics E12).
- [ ] Whether a partial shift produces a report is decided **before this story starts** — see the
      gap recorded below.

#### E12-06 · Player identity for the report and the PA
**Depends:** E12-01 · **Test:** L1 · **Answers:** epics open item 6, earlier than it expects to be
answered

- [ ] Names derive from the Steam persona (epics E5).
- [ ] A fallback generator exists for ENet development builds, CI and the L3 suite — and it is **in
      tone**. `Player 2` on the report is a tone regression (epics open item 6).
- [ ] A name round-trips unchanged: ledger entry → report line → PA name token.

---

### E13 — Authoring Pipeline *(the early-and-badly slice)*

*Vision §13: start it early and badly rather than late and well. These are the content types the
MVP line actually consumes; the mutation, PA-line and hazard schemas arrive with E9, E7 and E6.*

#### E13-01 · Parcel archetype `.tres` schema
**Depends:** E2-01 · **Test:** L1 via `ContentTool` · **Status:** 🔶 **Partial**

- [x] A new parcel archetype ships **with no code change** — proved by adding one in the same PR.
      Five shipped in this one: `envelope`, `ledger_box`, `glassware_crate`, `machine_pallet`,
      `unmarked_carton`, each a `.tres` under `src/Dlo.Game/content/archetypes/` and none of them
      named anywhere in code. `ParcelArchetypeResource` is the authoring face; `ParcelArchetype`
      is the checked one.
- [x] Mass and size are sanity-checked; declared contents resolve. Mass against
      `ParcelArchetype.MinMass`/`MaxMass` — the upper bound is E1-01's stability envelope, so an
      archetype authored past it fails here rather than as jitter in a playtest — size against
      `MaxSize`, and declared contents against the contents table.
- [ ] An unknown id makes the thing **inert and logged, not fatal** (Standards §9).
      **Half done:** `ContentSet.FindArchetype` returns null for an id nobody authored and an L1
      test proves it, so inert is real. **Logged is owed** and cannot be paid here — Domain has no
      output (Standards §0), so the log line belongs to the first caller, and there is none until
      **E4-01** spawns parcels. `FindArchetype`'s own doc comment says so.

#### E13-02 · Manifest and address grammar
**Depends:** E13-01 · **Test:** L1 · **Status:** ✅ **Done**

- [x] Every authored address parses to a routable destination, checked at load. Both halves are
      checked: the grammar, and that the destination is one `routing.csv` actually routes. A row
      naming a destination in no route names the file and the line, rather than surfacing at the
      chart mid-shift where nothing points back to it.
- [x] The grammar is **one schema, not two shapes with a fallback bridging them** (Standards §9).
      One `Pattern`, one parser. **A destination is read through the address grammar rather than
      beside it** — `Address.IsDestination` probes with a unit number and keeps the routable half,
      so there is no second pattern that can drift from the first. A test asserts a destination
      fails for exactly the reasons a full address does.

#### E13-03 · Routing policy schema
**Depends:** E13-02 · **Test:** L1 · **Status:** ✅ **Done**

- [x] Every destination maps to **exactly one** chute; a policy that breaks this fails the build
      rather than producing an unroutable shift. Duplicates are rejected rather than last-one-wins,
      which is what would otherwise leave the chart on the wall and the scoring at the whistle
      disagreeing in silence. Blocks are canonicalised first, so `NORTHGATE-4` and `NORTHGATE-04`
      collide instead of both loading. Proved locally: a duplicated route exits `ContentTool`
      non-zero; **watching CI itself go red is E13-06's, and still owed.**
- [x] A policy change is a data edit, because E3-08 needs the PA to make one mid-shift. The policy
      is `content/routing.csv` and the loader is the only reader. **Mid-shift mutation is not this
      story** — that is E3-05's `PolicyState`, which this schema is the authored input to.

#### E13-04 · Signage table
**Depends:** E13-02 · **Test:** L1

- [ ] Referenced destinations exist, or the build fails.
- [ ] Carries the colour and shape fields E15-03 needs.

#### E13-05 · `ContentTool validate`
**Depends:** E13-01 · **Test:** L1 · **Status:** ✅ **Done**

- [x] A deliberately broken content file fails validation with a message naming **the file and the
      invariant**. Run against five deliberate breakages at once — a bad contents code, a duplicated
      route, an over-mass archetype, a typo'd contents reference and an unroutable address — and
      each named its file, its line where it has one, the rule as a **rule** rather than as its
      violation, and what was found instead. Exit code 1.
- [x] Validation runs in seconds. It runs in well under one: the whole set is five `.tres` and
      three tables, and Domain does no I/O.
- [x] **Every new content type ships its validation rule in the same PR** (Standards §9). All four
      introduced here — archetypes, contents, manifests, routes — arrived with theirs.

#### E13-06 · `ContentTool validate` in CI
**Depends:** E13-05, E14-07 · **Test:** build only · **Status:** 🔶 **Partial**

- [ ] A broken content file **fails the build** — proved by pushing one and watching CI go red
      (Arch §7). This is the only mechanism that keeps a pipeline honest under deadline.
      **The step is wired in** and runs ahead of the Godot install, so a content typo reports in
      seconds rather than after a 100 MB download. **The proof is owed**, and it is the same debt
      E14-07 carries: one scratch branch, one broken row, one delete. Doing both in one push
      closes both.

#### E13-07 · Authoring guide, first pass
**Depends:** E13-05 · **Test:** none

- [ ] Written for whoever is authoring content at 11pm, because that is the actual audience.
- [ ] It is allowed to be bad. It is **not** allowed to be missing — that is the entire thesis of
      this epic (vision §13).

---

### E15 — UI *(the minimum Gate 0, 1 and 2 need)*

*HUD, full settings, subtitles, font scaling and remapping are out of this pass.*

#### E15-01 · Scanner screen
**Depends:** E3-02 · **Test:** L2

- [ ] The scan result is read from a **diegetic surface**; a clipboard or a screen beats a HUD panel
      where the fiction allows it (epics E15).
- [ ] It displays only what this client has actually received. It cannot display a manifest the
      network never sent — which is E3-06 doing its job, visible from the outside.

#### E15-02 · Routing chart rendering and the legibility check
**Depends:** E3-05 · **Test:** L2 + in-engine check

- [ ] The chart is **legible at 1080p from the far wall**, checked at the real post distance in the
      real greybox — not on a monitor two feet away (epics E15 DoD).
- [ ] It re-renders on a `PolicyState` change without a reload.

#### E15-03 · Colourblind-safe chute and signage coding
**Depends:** E4-08, E13-04 · **Test:** audit

- [ ] Chute identity and signage are distinguishable **without colour** — shape, number or pattern
      carries the information, and colour is redundant reinforcement.
- [ ] Audited under deuteranopia, protanopia and tritanopia simulation.
- [ ] This is a **correctness requirement, not a nicety** (Standards §10): routing is the core verb,
      and the game is unplayable without it. Not lazy-able (`AGENTS.md`).

#### E15-04 · Minimum settings for playtests
**Depends:** E1-02 · **Test:** L2

Not in the epics document's story list, and needed **before Gate 0** rather than after it: a
participant who cannot set their sensitivity is testing your mouse settings, not your grab feel —
and that contaminates the one gate whose entire evidence is a single unprompted word.

- [ ] Look sensitivity, invert-Y and FOV, persisted between sessions.
- [ ] Lives in `SettingsService`, one of the four permitted autoloads (Arch §6.2). **No fifth
      autoload** — that is an architecture change, not a PR.

---

### E17 — Asset pipeline *(the placeholder contract)*

#### E17-01 · The asset specification
**Depends:** E14-01 · **Test:** none

- [ ] Every placeholder the game needs is **described in a specification**, and the specification is
      the source of truth — not the asset folder (epics E17 DoD).

#### E17-02 · Placeholder generator in `ContentTool`
**Depends:** E17-01, E13-05 · **Test:** L1

- [ ] Placeholders are **generated from the specification**, never hand-made and committed.
- [ ] Output is deterministic for a given spec, so regeneration produces no spurious churn.

#### E17-03 · `Directory.Build.targets` wiring
**Depends:** E17-02 · **Test:** build only

- [ ] Placeholders regenerate **before the game builds**, so a fresh clone just works.
- [ ] **Generated placeholders are not committed** (epics E17, Standards §9). The house project
      learned this twice: committed placeholders went through LFS, so a clone that skipped
      `git lfs pull` booted invisible; and the bytes were toolchain-coupled, so a .NET bump rewrote
      every PNG without changing a pixel.
- [ ] A fresh clone builds and runs **with no LFS fetch at all**.

#### E17-04 · Silhouette legibility rules
**Depends:** E17-01 · **Test:** audit

- [ ] A parcel's size, weight class and fragility are legible **at a glance**, from the silhouette
      alone (vision §3.4).
- [ ] Checked **at video-compression quality**, not at 4K. "Legible at 4K" is not the test — the
      clip is the product (Standards §10).

---

### E18 — Build and export *(the early verification only)*

*Steamworks setup, depots, CI export and crash reporting are out of this pass.*

#### E18-01 · Export presets for the three desktop targets
**Depends:** E14-03 · **Test:** manual

- [ ] **Export templates for 4.7.2-stable-mono are installed first.** The machine currently has
      4.6 templates and no 4.7.2 set, so this story fails on its first attempt until they are
      downloaded (epics open item 10). Templates are versioned with the editor and must match it
      exactly.
- [ ] Windows, Linux and macOS presets exist and produce a launching build.
- [ ] `export_presets.cfg` is committed; secrets live in `export_credentials.cfg`, which is not
      (epics E18).

#### E18-02 · Domain-in-export verification
**Depends:** E18-01, E2-01 · **Test:** manual, once, and again whenever export config changes

- [ ] An exported build runs a Domain code path and logs a value proving the assembly shipped.
- [ ] **`net10.0` survives the export.** Every other leg of the TFM override was verified up
      front (Arch §1.4); export is the one that could not be, because it needs templates. If it
      fails here, that bullet is where the finding goes — and the fallback is `net8.0`, which
      costs compile surface and nothing else.
- [ ] **Done on the first Phase-1 story, not the week of launch** (Arch §1.4, epics E18). The
      failure mode — `solution_directory` unset, the exporter refusing every C# source — is silent
      right up until it is expensive.

---

### E19 — Playtest operations *(Gates 0 to 2)*

*Gate 3's clip-capture instrument is out of this pass; it arrives with E8.*

#### E19-01 · The facilitation protocol
**Depends:** — · **Test:** none

- [ ] How a session is run, what the facilitator says, and **what they must not say** — leading a
      participant toward the word "awkward" invalidates Gate 0 entirely.
- [ ] **Participants have not seen the game, and do not carry across gates** (epics E19).

#### E19-02 · Gate 0 instrument
**Depends:** E19-01 · **Test:** none

- [ ] Records **verbatim words**, not scores. The finding is which word arrives unprompted:
      *awkward*, or *broken*.
- [ ] Names in advance what a fail looks like, so the result is not renegotiated after the session.

#### E19-03 · Gate 1 instrument and recruitment
**Depends:** E19-01 · **Test:** none

- [ ] The instrument asks vision §15's question **verbatim**: *does manipulating a shared physical
      object still feel believable when three other people are doing it too, over real internet?*
- [ ] Four participants on **real, separate connections** — four machines on one LAN answers an
      easier question and would make the gate worthless.
- [ ] `LatencyPeer` (E0-07) is available as a supplement, never as a substitute.

#### E19-04 · The Gate 1 written decision
**Depends:** E19-03, and everything Gate 1 needs · **Test:** none

- [ ] A written go/no-go **citing observations, not opinions**.
- [ ] It states plainly that **on a fail, feature work stops** (vision §15, epics E19). Nothing above
      that line matters, and the decision document is where that gets said out loud, in advance.

#### E19-05 · Gate 2 instrument and decision
**Depends:** E19-01 · **Test:** none

- [ ] Named observation targets: **scrutiny or volume**, and separately, **blob or crew**.
- [ ] The pre-approved response to a dilution finding is E3-10, recorded **before** the session, so
      a bad result cannot become a redesign argument afterwards.
- [ ] Shift length is **refined here, not decided here**: 8–12 min × 3–6 shifts is already the build
      target, and this moves it inside that envelope on evidence (epics E19).

#### E19-06 · The findings register
**Depends:** E19-01 · **Test:** none

- [ ] One place where every gate decision and every playtest observation lives, so a decision made
      in month two is still findable in month nine.
- [ ] Gate 0's and Gate 1's decisions are filed here, not in a chat.

---

## The first ten stories — all walked

Sequencing came from the dependency graph, and the graph's first walk was unambiguous. The only
judgement call in it was that **two spikes come before the work they de-risk** — which is the
whole point of a spike. Recorded as history now, because the walk is finished.

| # | Story | Status |
| ---: | :-- | :-- |
| 1 | **E14-01** Solution scaffold | ✅ |
| 2 | **E14-02** editorconfig + build props | ✅ |
| 3 | **E14-03** Godot project, Jolt confirmed | ✅ |
| 4 | **E14-04** xUnit + first real test | ✅ |
| 5 | **E0-01** Steam C# spike | ⛔ Blocked on Steam accounts on separate machines |
| 6 | **E14-06** Architecture test | ✅ |
| 7 | **E14-07** CI | 🔶 Green, never seen red |
| 8 | **E0-02** `IGameTransport` + ENet | ✅ |
| 9 | **E1-01** Jolt joint spike | ✅ Reported — found a different risk (arch §11) |
| 10 | **E0-08** L3 harness feasibility | ✅ Four processes, measured |

Both forks past that walk are also done: **E0-04 → E0-09 → E0-10** closed the L3 chain, and
**E1-02** landed the controller. E0-01 is the only one of the ten still open, and it is the only
one whose blocker is a machine rather than a dependency.

---

## What to work on next

Read off the dependency graph, not off preference. Everything here has its dependencies met
**today**.

**To reach Gate 0** — this is now the whole critical path, and none of it is gameplay code:

| Story | Why it is next |
| :-- | :-- |
| **E15-04** Minimum settings | Look sensitivity, invert-Y and FOV, persisted, in `SettingsService` (one of the four permitted autoloads — **no fifth**, arch §6.2). Small, and it gates the gate |
| **E19-01** The facilitation protocol | No code. What the facilitator says and, more importantly, **must not** say — leading a participant toward the word "awkward" invalidates Gate 0 entirely |
| **E19-02** Gate 0 instrument | Records **verbatim words**, not scores, and names in advance what a fail looks like so the result cannot be renegotiated afterwards |
| **E1-10** Gate 0 itself | One player, local, a heavy awkward box. Then a written go/no-go in E19-06's register |

**Unblocked and off that path**, worth picking up alongside:

- **E2-05** Replication classes with promotion and demotion — unblocked by E4-01, and the belt
  already has both halves in reach: `Conveyor.Accept` promotes into `Railed` and `Release` hands a
  parcel back to physics. What is owed is the demotion to `Sleeping` when Jolt reports rest, and
  the L3 proof over a real socket. **`ReplicationMeter` is the instrument it needs**, and it
  exists. Through it: E2-09 and E2-10.
- **E4-03** Pneumatic tubes — unblocked by E4-01 and E2-02. A parcel in transit is `(tubeId, eta)`
  with **no body at all**, which is the strongest test the identity work has: E2-02 proved a
  record outlives its node, and this is the story that relies on it in flight.
- **`GrabDirector`'s scene-path addressing** — E4-01 landed the belt but nothing yet pools a parcel
  into a live shift, so the ponytail's ceiling is still one story away. **E4-05 is the line**: a
  greybox with a running belt is where a recycled body first gets renamed under a grab.
- **E13-04** Signage table — needs only E13-02, which is done, and it is the smallest story left
  in E13. **E13-07**, the authoring guide, needs only E13-05 and is the epic's actual thesis
  (vision §13): it is allowed to be bad, it is not allowed to be missing.
- **E2-05** Replication classes — E1-06 ran into exactly what it exists for: a client that both
  simulates and receives a body fights itself. The L3 harness freezes parcels on non-authority
  peers to work around it, and that workaround is a note in this document rather than a design.
- **E14-07's red run** — one scratch branch, one bad assert, one delete. Closes the last E14 box.
- **E17-01** The asset specification — no dependencies beyond the scaffold, and E1-03's hands have
  nothing to show until placeholders exist.
- **E12-01** Lobby — needs only E0-04, and Gate 1 needs it.
- **E18-01 / E18-02** Export verification — unblocked, but **install the 4.7.2-stable-mono export
  templates first**; the machine has 4.6 templates and this story fails until that is fixed. It is
  also the one unverified leg of the `net10.0` override (arch §1.4).

**Do not start** E0-03 (blocked on E0-01), E3-10 (a held fix, and Gate 2 has not reported), or
anything in Tier 2 — Gate 1 can still invalidate it, which is rung 1 of `AGENTS.md`.

---

## Gaps this decomposition surfaced

The epics document asks that a question a story cannot answer from its epic's **Decisions already
made** be recorded and answered **once, at the epic level**. Decomposing the MVP line surfaced
seven, and building E13 added two more. Four are genuinely open; three are duplications or inconsistencies that need a ruling rather
than a decision.

| # | Gap | Blocks | Suggested owner |
| ---: | :-- | :-- | :-- |
| 1 | **Who owns the player character's transform?** Arch §3.1 says host authority with no prediction of gameplay state; Arch §6.1 says input never waits for the network. For your own body those reconcile only if the character node's authority is the **owning peer** — which is not prediction, and a position is not a fact about the shift. That reading is assumed in E1-02 and needs confirming rather than inferring, because every other network story inherits it | E1-02, and everything downstream | Tech |
| 2 | **Which policy judges a parcel already in flight?** E3-08 changes `PolicyState` mid-shift. A parcel stamped under the old policy and entering a chute after the change is judged by — the policy at stamp time, or at chute entry? Chute entry is assumed (it is crueller, more on-theme, and the only one that needs no cached answer, which Standards §12 forbids anyway), but it is a design call, not a technical one | E3-04, E3-08 | Owner |
| 3 | **Does a host-lost shift produce a report?** E12-05 ends the shift cleanly on all peers. Vision §7 makes the report the highest-value feature and §3.5 makes blame the comedy engine — a rage-quitting host who deletes everyone's report is a real outcome to have an opinion about | E12-05, E8 later | Owner |
| 4 | **What is a post, physically?** A volume you stand in, or a station you interact with? E3-01 and E3-06 both hang off the answer, and so does whether "you cannot work two at once" is enforced by geometry or by state | E3-01, E3-06, E4-05 | Owner + Tech |
| 5 | **Host-loss teardown is listed in two epics.** It is a story in E0 *and* in E12. Split here as E0-10 (session ends cleanly, asserted at L3) and E12-05 (the player-facing message and return to lobby). Confirm or merge | E0-10, E12-05 | Tech |
| 6 | **Scanner and chart appear in both E3 and E15.** Split here as E3 owning the mechanism and E15 owning the surface. E15's header also says Tier 2 while the dependency diagram says it runs from Tier 1 — the diagram is right, since E3 cannot be played without a readable chart | E3-02, E3-05, E15-01, E15-02 | Tech |
| 7 | **Gate 2 needs Gate 1's kit.** Gate 2 lives in E3 (Tier 1), but it needs four players and a lobby, which is Gate 1's line. Sequence is presumably Gate 1 → Gate 2 in the same window; worth stating, because "Gate 2 lives in E3" reads as though it comes first | E3-09, E19-05 | Owner |
| 8 | **Arch §7 says the routing policy is a `.tres`, and it was built as a data table.** Two rules collide: `ContentTool` may reference Domain and nothing else (arch §1.3), and Domain may not reference Godot (arch §2) — so the validator reads `.tres` as text. That is fine for archetypes, which are flat scalars in one file each. A routing policy is a two-column mapping, and its `.tres` form is a multi-line Godot dictionary whose diffs are worse than a table's — which defeats the stated reason `.tres` is kept out of LFS at all. **Built as `content/routing.csv`; needs ratifying or reverting** | E13-03, E3-04 | Tech |
| 9 | **The contents table is a content type arch §7 does not list.** E13-01 requires that "declared contents resolve", and nothing to resolve them against existed. Added as `content/contents.csv`, so an archetype naming contents nobody declared is a build failure rather than a box that says nothing. It wants a row in arch §7's table, or a ruling that declared contents should have been free text | E13-01, E2-03, E2-08 | Tech |

**Gaps 8 and 9 are open by construction, not by oversight.** Both were forced by building E13
and both are recorded rather than decided privately, which is what the epics document asks for.
The content set ships as authored either way — `ContentTool validate` is green on it, and the
L1 suite loads the real files — so ratifying gap 8 costs nothing and reverting it costs one
reader. **Reverting is the expensive direction if it is left late**, because every routing file
authored between now and then is written in the shape that would change.

**Gap 1 is settled — the owning peer owns the character transform.** Ruled 2026-08-25, and it
is the reading this table already assumed. Each character body gets
`SetMultiplayerAuthority(ownerPeerId)`, so a player's own body is authoritative on their own
machine and replicates outward: input is immediate because the body is *owned*, not because it
is predicted. That keeps arch §3.3's "grab is the only optimistic path" literally true, and
leaves §3.1 intact where it matters — the host still owns every fact about the shift: who holds
what, which post is occupied, what the ledger records. **No host-side position validation**, and
that is deliberate rather than overlooked: this is a four-player, friends-and-invites-only game
(vision §16), so a cheating client is not in the threat model. If that ever changes, the fix is
a host validator plus a corrective RPC, and it does not disturb anything built on this ruling.

**Gap 5 is settled** — by building it rather than by ruling on it. E0-10 shipped as the split
this table proposed: it asserts at L3 that a client disconnect leaves the host and the other
clients working, and that a host teardown ends every client's session cleanly, and it stops
there. Nothing in it re-forms a session, because that is E12-05's and E12-05 is explicit that
there is no host migration. The two stories do not overlap, so there is nothing to merge.

**Two existing open items bind earlier than the epics document expects:**

- **Open item 6 — the employee-name fallback** is filed as *"with E5."* E12-06 needs it now, because
  the L3 suite and every ENet development build need a name before E5 exists.
- **Open item 3 — replication measurement** is filed as *"before E4 is done."* E2-10 and E4-10 are
  the same measurement taken from two sides; run it once, record it once, cite it twice.

---

## Not in this pass

Left at the epic level deliberately, with the reason:

| Epic | Why it waits |
| :-- | :-- |
| **E5 — The Ratchet**, **E6 — Chaos** | Tier 2. Gate 1 sits at the entrance to this tier and can reshape both |
| **E7 — Presence** | Unblocked by the voice decision, but its design assumes E3's four posts survive Gate 2 |
| **E8 — The Blame Report** | Depends on E5 and E6; its plumbing is already being built in E2 and E6 by design |
| **E9 — Wrongness**, **E11 — Between Shifts** | Tier 4 |
| **E10 — Dead Letters** | Deferred, not cut. The epics document decomposes no stories **deliberately**, and this document does not either |
| **E15 (rest)**, **E16**, **E17 (rest)**, **E18 (rest)**, **E20** | Production epics whose Gate 1 slice is above; the remainder follows the tier that needs it |
| **E13 (mutation, PA line and hazard schemas)** | Each arrives with the epic that authors that content type — E9, E7, E6 |

Decompose the next tier **after Gate 1 reports**, not before. If Gate 1 fails, most of it would have
been written twice.

---

*Product intent, pillars and scope: see [the vision](dead-letter-office-vision.md). Patterns and
technical decisions: see [the architecture](dead-letter-office-architecture.md). Epics, gates and
sequencing: see [the epics](dead-letter-office-epics.md).*
