---
area: project
type: context
name: simplification-campaign-plan
status: open
created: 2026-07-14
created-by: Adele
---

# dydo 2.1.0 Simplification Campaign — the deletion marathon plan

Executes [DR-041](../decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md). End state: dydo = **compiler + knowledge + PM + nudges**, plus a repurposed Notion-sync watchdog. Everything else — the orchestration/claim/wake/dispatch machinery — is deleted.

**Guard is DISARMED** for the campaign (`dydo guard` → `dydo notguard`). Velocity is up; **git review is the safety net**. Re-arm before shipping 2.1.0.

## The two rails (run in parallel; they cannot collide)

- **Rail A — CODE deletion** (.cs). Agent/codex-driven. Touches the orchestration machinery.
- **Rail B — PROMPT review** (skills / agents / mode + workflow templates, `.md`). **Human-driven (balazs).** Touches ZERO `.cs`, so it never collides with Rail A — this is the free parallelism balazs identified. Revise the prompt content to match the new run-model (ephemeral workers, no claim ceremony, task-boundary coordination) while the code gets gutted underneath.

## Rail A method — map, then cut

**You cannot parallelize blind deletion** — the machinery is a tangled graph (`AgentRegistry.cs` is referenced almost everywhere). So:

### Phase 0 — JUNCTION MAP (read-only, MUST be first)
One focused analysis pass produces:
- the dependency graph of the cut candidates → the **ordered deletion sequence** (leaf refs before their roots, or a subsystem + all its refs as one atomic slice);
- the **genuinely-disjoint clusters** that can run in parallel worktrees;
- the exact **KEEP-fence** file list (below), so deletion agents can't over-cut.
Deliverable: the real slice list that Phases 1..N execute. (This replaces guessing.)

### Phases 1..N — EXECUTE in worktree-isolated slices
- Each slice runs in its OWN git worktree (this is the fix for today's shared-tree collision — Brian/Charlie blocked each other because plain dispatch shares one tree).
- Each slice = "delete subsystem X + every now-dead reference + its tests → build green → tests green." Merge back, next slice.
- Serialize slices that touch a shared hot file (`AgentRegistry.cs`); parallelize truly-disjoint clusters.
- **Ratchet:** green build + green suite after every merged slice. A red build is a stop-and-fix, not a "continue."

## KEEP-fence (OFF-LIMITS to deletion — the value that survives)

- **Compiler:** `SyncCommand` + role/skill/agent/template generation (`Services/TemplateGenerator.cs`, role definitions) — targets `.claude/`, `.codex/`, `.agents/`.
- **Knowledge:** everything under `dydo/` (decisions, issues, docs, guides, reference).
- **PM:** task / backlog / changelog handlers + the records themselves; the new `task done` / archive lifecycle (DR-036, just landed).
- **Guard — nudges + off-limits only:** the regex-pattern-nudge engine and `files-off-limits` enforcement. (Identity/RBAC/must-reads inside the guard are CUT — see below.)
- **Watchdog — REWORK not delete:** repurpose `WatchdogService.cs` into the ~15s Notion-sync daemon (self-started on guard trigger; enables collaborator file-sync between commits).

## CUT-list (candidate clusters — exact boundaries come from Phase 0)

1. **Dispatch + terminal launch** — `DispatchCommand`, `DispatchService`, `DispatchPreflight`, `TerminalLauncher` + the 3 platform launchers, Codex launch options.
2. **Wait / wake** — `WaitCommand`, durable-marker machinery, the "must keep a wait active" guard rule, the whole #0279 app-server plan.
3. **Claim / roster / identity** — the claim/release/26-agent-pool logic in `AgentRegistry`, `AgentSessionManager`, `AgentLifecycleHandlers`, `WhoamiCommand`, agent state files.
4. **Guard identity layer** — must-reads, RBAC, claim-gate, session binding inside `GuardCommand` (KEEP nudges + off-limits).
5. **The 26-agent named roster** — identity-from-a-pool exists only because sessions were long-lived.

## REWORK / ADD (not deletion)

- **Watchdog → Notion-sync daemon** (~15s; CLI self-starts it on guard trigger; collaborator file-sync between commits).
- **Research:** scan OSS Obsidian↔Notion sync repos for patterns/tricks; fold the good ones in.
- **`dydo dispatch`** likely becomes (if kept at all) a thin `codex exec -C <worktree>` wrapper — decision in Phase 0.

## Open decisions to settle in / before Phase 0

- **Messaging / inbox fate** — cut entirely, or keep a minimal task-boundary form? (Raise-hand = worker exits with its question; no live inbox needed.)
- **How much of `AgentRegistry` is load-bearing for NON-agent things** (paths, config) vs pure claim/identity? Phase 0 must separate them before cutting.
- **Guard re-arm** — the campaign runs with `notguard`; define the point where the trimmed guard is re-armed and re-verified before 2.1.0 ships.

## Phase-0 map RESULT (2026-07-14) — finalized cut order

The map ran. Two structural findings:
- **`AgentRegistry` = 276 refs / 40 files, and it is load-bearing for KEEP code** (Guard nudges, ReviewCommand, TaskCreate/TaskDone, IssueCreate, Validation). It is a **carve, not a delete**, and it goes LAST.
- **Nothing is a clean leaf at the service layer.** `TerminalLauncher` is shared by dispatch + watchdog(auto-resume) + `WorktreeCommand`. The wait concept is enforced by the guard and re-armed by the watchdog. Even `whoami` — the smallest cut — cascades into **6 test files, incl. 3 shared infra files** (`IntegrationTestBase`, `CommandSmokeTests`, `CliEndToEndTests`).

**Consequence:** cut **leaves → branches → trunk**:
1. **Command leaves** (delete command + handler + dedicated tests + de-wire `Program.cs`/`HelpCommand`; fix shared-test-infra fallout): `Whoami`, `Inbox`, `Message`, `Read`, `Wait`, `Dispatch`, `Agent`, `Workspace`. Wait also removes the guard's "must keep a wait active" rule; ReviewCommand loses its 2 `DeliverInboxMessage` notify calls.
2. **Orphaned services** (now unreferenced by any command): `MessageService`, `InboxService`, `TerminalLauncher` + 3 platform launchers, `DispatchService`, `DispatchPreflight`, `AgentSessionManager`.
3. **Trunk:** hollow the Guard (strip must-reads/RBAC/claim-gate/session-binding; keep nudges + off-limits) · rework the Watchdog (→ Notion daemon) · **carve `AgentRegistry`** (keep the paths/config bits KEEP-code needs; delete claim/roster/identity).

**Every slice hits shared test infrastructure**, so slices are NOT independent worktrees — they serialize on the shared test project. Parallelism is limited to Rail B (prompts). This is a serial surgical grind, reviewed slice by slice — not a parallel free-for-all.

## Sequencing note (spend + safety)

- **Blocked on spend:** Rail A EXECUTION (deletion agents / codex workers) waits on the Anthropic spend limit being sorted. Phase-0 mapping is one focused pass; Rail B is balazs and needs nothing.
- **Not blocked:** balazs reviewing/revising the prompt files (Rail B) can start now and run through the whole campaign in parallel.
- Reform wave (DR-036) + Notion chunking are gated-green and tag-ready independently — tag whenever.
