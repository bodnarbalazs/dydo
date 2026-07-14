---
area: project
type: decision
status: proposed
date: 2026-07-14
participants: [balazs, Adele]
---

# 041 — dydo Cedes Orchestration: The Vendor-Agnostic Authoring + Knowledge + PM Layer

**Status: PROPOSED — written to be attacked, not obeyed.** This record captures a strategic pivot decided 2026-07-13/14. It is the successor to [DR-024](./024-dydo-2-native-pivot.md) (the "go native" pivot that outsourced orchestration but *kept top-level dispatch*) and it finishes that pivot by removing the piece DR-024 left in.

## The decision, in one line

**dydo is the vendor-agnostic SOURCE and BRAIN, not the RUNTIME.** It authors skills/agents/roles/policy once and compiles them to each vendor's native format; it holds the documentation and project-management structure. It does **not** run, coordinate, wake, or babysit agents — the coding platforms (Claude Code, Codex) own that, and are actively perfecting it.

## Why now — the evidence (all from the 2026-07-13 session)

- **The switchboard problem.** dydo's own top-level dispatch model (long-lived codex agents in terminal tabs, claiming names from a 26-agent pool) turned the human into a switchboard operator: hand-nudging Brian, Charlie, Dexter all day because idle codex agents don't wake on message arrival (#0279). Meanwhile Claude **native subagents** (every reviewer/planner/simplifier) ran silently, flawlessly, needing no tab/claim/wake — and caught 5+ real bugs. The evidence pointed one way.
- **Every mechanism dydo built to fix this is machinery the platforms are removing the need for.** #0279 (wake idle codex), #0292 (durable-wait blindness), the whole codex app-server `turn/start` spike — all exist only because dydo chose long-lived sessions. Ephemeral run-to-completion workers make the entire class evaporate.
- **Three independent convergences on the same shape.** Anthropic **agent teams** (mailbox + shared task list, coordination at turn boundaries), **steipete's fleet** (`/handoff` docs + decision briefs, one thread per repo, policy-as-SKILL.md), and **Codex v2** (agents-with-mailboxes; mid-flight injection is WIP). Three teams building the actual tools independently arrived at task-boundary coordination. dydo should not out-engineer them at their own game.
- **Vendor-coupled enforcement is brittle.** The guard's codex hook trust depends on an opaque per-entry hash dydo cannot compute (#0296) — so dydo silently reported the guard "green" while codex had it switched off, and never actually matched codex's shell tool at all (#0295). Coordination/enforcement that dydo can't verify, dydo shouldn't own.
- **Vendor lock-in is a real risk the human wants to hedge.** Anthropic's usage limits (which literally halted an in-progress review this session) and communication pushed toward diversification. The one thing that makes vendor-switching cheap is **author-once → compile-to-many**. Research found this is a genuine market gap: only toy one-way migration scripts exist; "author-once bidirectional role compilation was not found as a named product."

## What dydo KEEPS (its actual, defensible value)

| Component | Why it survives |
|---|---|
| **The compiler** (`dydo sync`): role/skill/agent definitions → `.claude/`, `.codex/`, `.agents/` | The crown jewel. Author once, run on any vendor. Nobody else has this; it's the anti-lock-in layer. |
| **The knowledge structure**: decisions, issues, docs — versioned with the code | The goldmine. A decision record handed to an agent at the moment it designs (DR-036 → the reform) is something no PM app can do. Repo-native context, not a separate board. |
| **The PM records**: tasks/backlog/changelog as markdown+frontmatter | The "simple and dumb filesystem" contract both vendors can read. The queue lives here. |
| **The guard as POLICY CONTENT** (off-limits, dangerous-command rules) | Keep the *rules*; the enforcement *mechanism* is increasingly vendor-native (hooks/sandboxes). Own what to block, lean on the platform to block it. |
| **Chief-of-staff / co-thinker roles** | Compiled skills, not running processes. The human's interface to the whole thing. |
| **Notion-as-view** | Queryable DBs + real views over the PM records — a genuine capability Obsidian lacks. A read projection, not a coupling. |

## What dydo CUTS (cede to the platforms)

- Long-lived agent sessions and the fixed 26-agent named roster.
- Claim / release / identity-from-a-pool machinery (identity is assigned at spawn, not claimed).
- The wake machinery: `dydo wait`, durable markers, the entire #0279 codex app-server integration plan (#0292 dies with it).
- Tab-per-agent top-level dispatch of *workers*.
- dydo-owned orchestration loops. Coordination happens via native means (Claude subagents / agent teams / dynamic workflows; Codex exec / v2) at **task boundaries**.

**Boundary rule going forward:** *dydo authors and knows; the platform runs and coordinates.* If a proposed dydo feature is about **running or coordinating** agents, it belongs to the vendor now. If it is about **representing** an agent/skill/policy/decision/task in a vendor-neutral way, it belongs to dydo.

## How work runs under this model (near-term, interim)

Until the vendors' coordination is mature enough to lean on fully, the human runs work himself, hands-on, using native primitives — NOT a dydo orchestration engine:

- **Tier-1 (human + thinkers):** the human talks to a chief-of-staff session (CLI or GUI — same engine, sit wherever). Planning/review/inquisition run as **native subagents**.
- **Tier-2 (workers):** ephemeral, run-to-completion. `codex exec -C <worktree> …` (proven this session: unattended recipe works, raise-hand = exit→`codex exec resume`, worktree = the write boundary, attach-on-demand via the shared session store). One writer per worktree; parallel across slices, serial within. This is `run-sprint`'s existing shape, minus the tabs.
- **The contract** is the task record: brief + worktree + status + report, in markdown. Raise-hand = a worker exits with its question; the orchestrator (or human) answers by resuming the thread with full context intact.

Note this interim worker-running is explicitly a *stopgap that shrinks over time* as the platforms' own coordination (agent teams, Codex v2 mailboxes) matures — dydo should not invest in hardening it beyond "good enough to use."

## Consequences & known limitations

- **The human loses "watch every agent type in a tab"** and gains "not being a switchboard." Attach-on-demand (`codex resume <name>`, GUI thread list) replaces always-on tabs.
- **Model-diversity-as-adversary is retained** (codex writes, Claude reviews) — it caught real bugs this session — but expressed through the compiler routing tasks to the right vendor, not through a dydo-run fleet.
- **The bottleneck moves to review** (every scaling source agrees). Invest in reviewer capacity + disjoint slicing, not more writers. Budget ~15× chat tokens for multi-agent (Anthropic's own number).
- **dydo shrinks.** This is finishing DR-024's pivot, not abandoning dydo. The parts cut are the parts that hurt; the parts kept are the parts that worked.

## Resolved (balazs, 2026-07-14)

1. **Guard: keep policy, cut identity-tied enforcement.** NUDGES stay 100% (regex patterns on hooks + a helpful error message — this is the essence of "dynamic documentation"). Files-off-limits stays. Everything gated on an AGENT IDENTITY — must-reads, RBAC, claim gates — dies *with* the claim ceremony. That is a feature of the simplification, not a loss.
2. **Compiler targets what the human uses** (Claude + Codex). No chasing every emerging format.
3. **Files are source; Notion is projection.** BUT the **watchdog survives, repurposed** as a Notion-sync daemon: syncs every ~15s; the dydo CLI, on guard trigger (≈ every agent call), checks whether the watchdog is running and starts it if not. Bonus: this live sync also gives **collaborator file-sync between commits**. Research task: scan open-source Obsidian↔Notion sync repos for patterns/tricks and fold the good ones into our sync logic.
4. **The smallest dydo = compiler + knowledge + PM + nudges.** "dydo becomes dynamic documentation, mostly — like its name says (DynaDocs)." Most of the C# orchestration machinery gets deleted outright.
5. **Migration = a deletion campaign.** Find the junctions ripe for deletion and cut aggressively; git review is the net. Guard is DISARMED during the campaign (renamed `dydo guard` → `dydo notguard` so it won't fire). Plan: `backlog/simplification-campaign-plan.md`.

## Related
- [DR-024](./024-dydo-2-native-pivot.md) — the native pivot this completes (it kept top-level dispatch; this removes it).
- [DR-026](./026-tier1-managers-doctrine.md) — Tier-1 agents are managers, not implementers (this generalizes it: dydo itself is not the implementer/runtime).
- [DR-037](./037-cross-vendor-dispatch-same-vendor-default.md) — task-boundary coordination doctrine (validated by all three vendor convergences).
- Issues #0279 (codex wake), #0292 (wait blindness), #0295 (guard shell hole), #0296 (preflight false-green) — the machinery this pivot makes moot or reframes.
