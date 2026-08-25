## What, and which story

<!-- One or two sentences, and the story id: E0-04, E2-01, E14-06. -->

## Test level

<!-- L1 / L2 / L3 / spike / build only / playtest. The story states it up front
     (arch §10.1); Definition of Ready means it was never an afterthought. -->

## The checks a test cannot do

`docs/CODING-STANDARDS.md` §12 is the review checklist and it is **not copied here** — a
second copy drifts from the first, and then reviewers follow the stale one. Read it there.

What is repeated here is the one item that is a *command* and that no suite in this repo will
ever catch:

- [ ] **One construction site for domain systems** (arch §3.2):

  ```
  grep -rn "new ShiftDirector\|new ShiftLedger\|new ParcelRegistry" src/
  ```

  Expect **exactly one line**, in `SessionRoot._Ready`, behind one `Multiplayer.IsServer()`
  branch. A second one is a client-side system that will drift from the host's, and it fails
  as disagreement between peers rather than as an error. *(Zero lines is also correct until
  E0-04 lands.)*

- [ ] **The transport seam still holds** (arch §3.5, E0-02) — no engine or SDK networking
  type outside `src/Dlo.Game/Net/`:

  ```
  grep -rn --include=*.cs -E "ENetMultiplayerPeer|ENetConnection|SteamMultiplayerPeer|Steamworks|CSteamID|SteamAPI" src/ tests/ tools/ | grep -v "^src/Dlo.Game/Net/" | grep -v "^tests/Dlo.Game.Tests/LatencyPeerTests.cs" | grep -v "^tests/Dlo.Game.Tests/GrabPredictorTests.cs" | grep -vE "^[^:]+:[0-9]+:[[:space:]]*(//|\*)"
  ```

  Expect **no output**. This is what keeps E0-03 a drop-in instead of a migration, and it is
  the only thing standing between E0-01's Steam risk and the rest of the codebase.

  The last filter drops comment lines — a comment naming `SteamAPI_Init` to explain a failure
  mode is not a seam violation, and it was making this check print.

  Two test files are excluded, and both are named here rather than tolerated as noise:

  - `LatencyPeerTests`, because `LatencyPeer` is a decorator **over** a `MultiplayerPeer`, so
    testing it needs a real one to wrap.
  - `GrabPredictorTests` (E1-05), because it needs a peer that **cannot answer** — a client with no
    host — to prove the optimistic attach does not wait for the network, and a real server peer so
    that host mode is a fact rather than inferred from the absence of one.

  **If this grep ever prints, do not add another exclusion without saying why in the PR.** A check
  whose output reviewers have learned to skim is not a check. The rule it protects is E0-02's: no
  *gameplay* code names a transport type, so E0-03 stays a drop-in rather than a migration.

  The last filter drops comment lines — a comment naming `SteamAPI_Init` to explain a failure mode
  is not a seam violation, and it was making this check print.

- [ ] Anything asymmetric-information-shaped has a **negative** test (arch §10.4, standards
  §8). Nothing looks broken when a client knows too much, so this is the one that regresses
  silently.

- [ ] Every `ponytail:` added names a **ceiling and an upgrade path**. One half is a TODO in
  a costume, and Definition of Done rejects it.
