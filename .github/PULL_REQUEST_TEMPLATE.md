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
  grep -rn "new ShiftDirector\|new ShiftLedger" src/
  ```

  Expect **exactly one line**, in `SessionRoot._Ready`, behind one `Multiplayer.IsServer()`
  branch. A second one is a client-side system that will drift from the host's, and it fails
  as disagreement between peers rather than as an error. *(Zero lines is also correct until
  E0-04 lands.)*

- [ ] Anything asymmetric-information-shaped has a **negative** test (arch §10.4, standards
  §8). Nothing looks broken when a client knows too much, so this is the one that regresses
  silently.

- [ ] Every `ponytail:` added names a **ceiling and an upgrade path**. One half is a TODO in
  a costume, and Definition of Done rejects it.
