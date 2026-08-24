# Dead Letter Office — Product Vision

**Version:** 0.1
**Status:** Draft for review
**Scope:** Finished product. MVP line is marked but not the subject of this document.

---

## 1. Thesis

Every successful game in the chaos-co-op category is an extraction game. Lethal Company, R.E.P.O., Content Warning, PEAK — enter the hostile place, take the valuable thing, leave before something eats you.

Dead Letter Office is not an extraction game.

> **You are not raiding the facility. You are employed by it.**

The job *is* the game, and the job *is* the horror. This gives the product an antagonist no competitor has:

> **The enemy is the bureaucracy. The anomaly is only weather.**

Management never appears. It issues policy. The belt does not stop because a manual says it does not stop, and nobody who wrote the manual still works here.

**Elevator pitch:** *Papers, Please* × *Viscera Cleanup Detail*, played by four idiots on a Discord call.

---

## 2. Design Keystone

Physical slapstick is fast and loud. Document scrutiny is slow and quiet. Left alone, one of them eats the other — either players stop reading manifests entirely, or the chaos becomes an interruption to the "real" game.

The resolution is a single rule that everything else hangs from:

> **The scrutiny causes the chaos. The belt never stops.**

Every second spent reading a manifest is a second of parcels accumulating behind you. Careful work generates physical debt. The two halves are not adjacent — they are causally linked, and neither is optional.

Any proposed mechanic that breaks this rule is out of scope by default.

---

## 3. Pillars

### 3.1 Physical Co-Op Chaos
Players manipulate the same world. Packages are heavy, awkward, fragile, occasionally dangerous. Cooperation is physical, not menu-based.

**Critical distinction:** awkward ≠ unresponsive. The player controller and the grab joint must be *tight*. The **parcel** is the problem, not the input. Get this backwards and the game reads as broken rather than funny.

### 3.2 Host-Authoritative Multiplayer
The host owns gameplay-critical state. Clients send input and interaction requests. The goal is not synchronized physics — it is that all four players see the same meaningful gameplay state.

### 3.3 Mundane First, Uncanny Second
The facility begins as a believable post office and stops obeying architecture gradually. The contrast is the identity. Not "haunted post office."

### 3.4 The Clip Is The Product
This category markets itself through 30-second failure videos. A session that does not generate a story worth retelling does not spread the game. This is a **design constraint**, not a marketing task — it touches readable silhouettes, punchy audio stingers, blame attribution, and the end-of-shift screen.

### 3.5 Blame Is The Comedy Engine
The funniest outcome is always *a friend did this to you*. The `INCINERATE` stamp is a loaded gun pointed at a teammate's work. Systems should make fault legible and attributable.

### 3.6 Spectacular Failure, Cheap Failure
Enormous consequence in the moment. Low cost across runs. Punishment that stings for an hour kills the rerun.

---

## 4. Locked Decisions

| Decision | Resolution | Rationale |
| :-- | :-- | :-- |
| Player count | **4** (prototype validates at 2) | Category standard. Two-player chaos co-op consistently underperforms — the social loop needs a third wheel and someone to blame. |
| Run structure | **Roguelite employment stint** | See §5. |
| Antagonist | **The bureaucracy** | The differentiating call. Anomalies are environmental, not adversarial. |
| Voice | **Load-bearing, not ambient** | Enforced by asymmetric information (§8), not by falloff alone. |
| Facility persistence | **Per-stint, sampled from a mutation table** | Preserves escalation inside a run; survives replay. |
| Dead-letter investigation loop | **Deferred to Tier 4, not cut** | Openable packages (kept) is its expensive dependency. Remains cheap to add later. |

---

## 5. Run Structure

The roguelite unit is **the employment stint**, not the shift.

```text
HIRED
  ↓
Shift 1  ──► quota ▲  wrongness ▲
  ↓
Shift 2  ──► quota ▲▲  wrongness ▲▲
  ↓
Shift N  ──► quota ▲▲▲  wrongness ▲▲▲
  ↓
TERMINATED
  ↓
Re-hired — new employee, new facility seed, wrongness reset
```

Quota ratchets each shift. Wrongness climbs each shift. Both reset on termination. The stint is what the crew is trying to extend, and the ratchet is what makes them greedy enough to fail.

**What persists across stints:** cosmetics, unlocked tools, player knowledge, and the small unlock ladder. **What does not:** facility layout, wrongness, quota, mail volume.

---

## 6. Core Loop

```text
Receive → Inspect → Scan → Stamp → Move → Sort → Survive → Repeat
```

Second-order loops layered on top:

- **Shift loop** — clock in, hit quota before the whistle, clock out
- **Stint loop** — survive escalating shifts until termination
- **Mystery loop** — the facility becomes wrong; players build theories across stints

---

## 7. The End-of-Shift Report

**Highest value-per-hour feature in the product.** Not a score screen — a blame ledger, presented in the voice of an indifferent institution.

```text
SHIFT 6 — PERFORMANCE SUMMARY

  Routed correctly ................... 41
  Misrouted .......................... 12   ▸ D. HALVERSON (9)
  Incinerated in error ................ 3   ▸ J. CORRUPT (3)
  Parcels dropped .................... 22   ▸ D. HALVERSON (14)
  Structural damage ............. $1,204   ▸ UNATTRIBUTED
  Personnel lost ...................... 1   ▸ M. OKAFOR

  QUOTA:  MET
  Employee of the Shift:  J. CORRUPT
```

This screen is the screenshot. Lethal Company's quota screen gets posted constantly and it does not even name anyone. Ours does, and it is passive-aggressive about it.

Design requirements: every consequential action carries an actor attribution; unattributable events are labelled `UNATTRIBUTED` rather than hidden; the report is readable at streaming resolution in under four seconds.

---

## 8. Asymmetric Information

Proximity voice is only load-bearing if the game **forces talking**. Falloff alone produces ambience.

Split the information across physical posts that cannot be worked from one spot:

| Post | Holds | Needs from others |
| :-- | :-- | :-- |
| **Intake dock** | Incoming volume, parcel physical state | Where it's going |
| **Scan desk** | Manifest data, contents declaration | What the parcel physically is |
| **Routing chart** (far wall) | Destination → chute mapping, policy updates | The destination code |
| **Chute floor** | Physical routing, jam clearing | Which chute, and whether it's stamped |

No player can complete a parcel alone. This is also what makes four players non-redundant — with everyone in one room doing one job, you get a blob, not a crew.

**Facility scale implication:** the main sorting floor grows from 25×20m to approximately **30×24m plus adjacent rooms**, with posts deliberately distributed.

---

## 9. Openable Packages

A parcel is currently a data carrier with a physical body. Making it **openable** unlocks, for near-zero mechanical cost:

- Contraband and declaration mismatches
- Contents that escape (physical, mobile, unhelpful)
- Contents that are alive
- Fragile contents whose breakage is discovered later, on the report
- "We are not supposed to look inside"
- The entire dead-letter investigation loop, later, if wanted

Opening is a **policy violation** unless authorized. That tension — the manifest says one thing, the box weighs another, and checking is against the rules — is the game's thesis in miniature.

---

## 10. The PA System

An unseen management, delivered entirely through building announcements. Cheapest possible narrative vehicle, enormous tone payload, and it escalates for free as wrongness climbs.

```text
Wrongness 0    "Reminder: the break room refrigerator will be emptied Friday."
Wrongness 2    "Sorting staff are reminded not to open parcels marked HOLD."
Wrongness 5    "Room 14 is not accessible to sorting staff at this time."
Wrongness 8    "Would J. CORRUPT report to the dead letter desk."
Wrongness 10   [a list of employee names is read out. Yours is on it.]
```

Requirements: all PA lines are data-authored (§14, E13), gated by wrongness threshold, and can reference player names. Voice-acted late; text-to-speech or typed placeholder early.

---

## 11. The Wrongness System

Wrongness is a float that climbs per shift within a stint. Environmental mutations are **sampled from a table**, gated by threshold — not scripted into a fixed sequence.

```text
Threshold   Mutation pool
─────────────────────────────────────────────
  1–2       Signage changes. Room numbers gain suffixes.
  3–4       A door exists that did not. A corridor runs long.
  5–6       A room repeats. A window shows the wrong location.
  7–8       A loading dock opens into an interior space.
  9–10      The building is measurably larger than its footprint.
```

Every stint escalates. No two escalate identically. This is both the cheaper implementation *and* the one that survives a second playthrough — a scripted sequence is spent after one run.

Mutations must be **specifically postal**. The weirdness comes from familiar postal architecture behaving incorrectly, not from generic liminal-space aesthetics.

---

## 12. Environment Architecture

Unchanged from the roadmap, with Layer 3 access now gated by wrongness rather than by shift number.

```text
LAYER 1 — THE POST OFFICE      Lobby, counter, PO boxes, staff door
        ↓
LAYER 2 — THE SORTING FACILITY  Primary play space. ~30×24m + adjacent posts
        ↓
LAYER 3 — THE BACK ROOMS        Wrongness-gated. Expansion surface.
```

Build the main facility with expansion in mind. Do not make the product dependent on Layer 3.

---

## 13. Epic Map

Dependency tiers, not a calendar. Epics within a tier can run in parallel.

### Tier 0 — Spine
*Everything blocks on these.*

- **E0 — Netcode Spine.** Authority model, transport, replication primitives, spawner, session lifecycle. Host-authoritative from the first line of code.
- **E1 — Embodiment.** Controller, hands, grab, carry, throw, stumble, two-player cooperative carry. Tight input, hostile world.

### Tier 1 — The Job
*Requires E0, E1.*

- **E2 — The Parcel.** Data + body. Manifest, address, weight, fragility, contents, openable, tamper state, stable network identity.
- **E3 — The Work.** Receive → inspect → scan → stamp → route. Tools, desks, forms, routing chart, the four posts.
- **E4 — The Facility.** Conveyors, chutes, tubes, doors, Layers 1–3 greybox, signage as authored content.

### Tier 2 — The Pressure
*Requires Tier 1.*

- **E5 — The Ratchet.** Quota, shift timer, escalation curve, termination, the stint.
- **E6 — Chaos.** Hazards, jams, spills, fire, ticking ordnance, the heavy anvil, live contents.

### Tier 3 — The Social Layer
*Requires Tier 1. E8 requires E5.*

- **E7 — Presence.** Proximity voice, handheld radios, PA system, deliberate separation.
- **E8 — The Blame Report.** Attribution plumbing, stats, the end-of-shift screen. Small epic, outsized return.
- **E12 — Session Ops.** Lobby, one-click Steam invite, drop-in/drop-out, graceful host loss.

### Tier 4 — The Identity
*Requires Tier 2.*

- **E9 — Wrongness.** Mutation table, threshold gating, per-stint sampling, Layer 3 access.
- **E10 — Dead Letters.** *(Deferred.)* Open → investigate → classify → live with it. The second loop.
- **E11 — Between Shifts.** Break room, lockers, cosmetics, the unlock ladder, what persists.

### Tier 5 — Longevity
*Runs alongside from Tier 2 onward.*

- **E13 — Authoring Pipeline.** Parcels, anomalies, room mutations, PA lines, signage — all as data, no code required. This epic determines whether the game is alive twelve months after launch. Start it early and badly rather than late and well.

---

## 14. Work Order

```text
        ┌─────────┐     ┌─────────┐
        │   E0    │     │   E1    │      Tier 0 — Spine
        │ Netcode │     │  Body   │
        └────┬────┘     └────┬────┘
             └───────┬───────┘
        ┌────────────┼────────────┐
   ┌────▼───┐   ┌────▼───┐   ┌────▼────┐  Tier 1 — The Job
   │   E2   │   │   E3   │   │   E4    │
   │ Parcel │   │  Work  │   │Facility │
   └────┬───┘   └────┬───┘   └────┬────┘
        └────────────┼────────────┘
             ┌───────┴───────┐
        ┌────▼───┐     ┌─────▼──┐          Tier 2 — Pressure
        │   E5   │     │   E6   │
        │ Ratchet│     │ Chaos  │
        └────┬───┘     └────┬───┘
             │              │
   ┌─────────┼──────────────┼─────────┐
   │    ┌────▼───┐  ┌───────▼┐  ┌─────▼──┐ Tier 3 — Social
   │    │   E8   │  │   E7   │  │  E12   │
   │    │ Blame  │  │Presence│  │Session │
   │    └────────┘  └────────┘  └────────┘
   │
   │    ┌────────┐  ┌────────┐  ┌────────┐ Tier 4 — Identity
   └───►│   E9   │  │  E10   │  │  E11   │
        │Wrongness│ │ Dead   │  │Between │
        └────────┘  │Letters │  │ Shifts │
                    └────────┘  └────────┘

        ┌──────────────────────────────┐
        │  E13 — Authoring Pipeline    │   Tier 5 — runs from Tier 2 on
        └──────────────────────────────┘
```

**Honest read on ordering:** E0–E6 produce a competent chaos co-op indistinguishable from its competitors. **E8 makes it clippable. E9 and the PA make it *this game*.** If anything gets starved late, starve E11 — cosmetics are visible, cheap, and ship fine post-launch.

---

## 15. MVP Line

Not the subject of this document, but recorded so the vision does not drift into it.

**In:** E0, E1, E2 (physical only), E3 (scan/stamp/route), E4 (Layer 2 greybox), E5 (single shift), E6 (one hazard), E12 (lobby + invite).

**Deferred:** E7 voice, E8 report, E9 wrongness, E10 dead letters, E11 between-shifts, E13 pipeline, package opening, Layers 1 and 3.

**Validation question the MVP exists to answer:**

> Does manipulating a shared physical object still feel believable when three other people are doing it too, over real internet?

If the answer is no, nothing above this line matters.

---

## 16. Scope Guardrails

Do not build: dedicated servers, persistent accounts, custom matchmaking, anti-cheat, rollback netcode, deterministic multiplayer physics, complex inventory, production-grade voice moderation, >4 player support, procedurally generated liminal space, branching narrative.

---

## 17. Open Questions

1. **Termination consequence.** What does being fired actually cost? Currently: the stint. Is that enough sting, or does the crew need something visible to lose?
2. **Shift length.** Five minutes is a prototype number. The real target is probably 8–12 minutes per shift with 3–6 shifts per stint. Needs playtest data.
3. **Does inspection survive four players?** With four posts, the scrutiny work is distributed. Risk that no single player feels the Papers-Please pressure — it may dilute into four easy jobs.
4. **Horror ceiling.** How far does wrongness go before it competes with Phasmophobia rather than differentiating from it? Recommend capping at "architecturally impossible" and never crossing into "actively hostile entity."
5. **Price point.** Category norm is $9.99–$14.99. Affects content volume expectations at launch.

---

## 18. Final Technical Principle

> **Do not build a physics game and then network it. Build a game whose gameplay rules are network-authoritative from the beginning.**

Likewise: do not build a normal post office and bolt the liminal concept on later. Build a believable postal facility whose architecture already leaves room for something stranger beyond the staff doors.

The player should first think: *"This is a weird postal job."*

Then: *"Why is there a room back here?"*

Then: *"This building is much bigger than it should be."*
