---
area: general
type: decision
status: proposed
date: 2026-06-05
participants: [balazs, Adele]
supersedes: [009]
---

# 024 — dydo 2.0: Pivot to a Policy + Context Layer over Native Orchestration

dydo stops shipping its own *worker-tier* runtime (dispatching code-writers/reviewers into terminals, plus their inbox/messaging/queues/worktree machinery) and instead becomes the **policy and context layer** that Claude Code's native primitives — subagents, skills, and dynamic workflows — plug into. It **keeps** the conveniences that have no native equivalent: launching a named top-level orchestrator/co-thinker in a terminal, light top-level-to-top-level messaging, and a (now Notion-syncing) watchdog. **Claude owns the engine (spawning, scheduling, isolation, fan-out); dydo owns the rules of the road (universal guardrails) and the map (structured, validated project context).** This supersedes [Decision 009](./009-claude-native-features-evaluation.md), whose "skip all native features" verdict predated dynamic workflows and governable subagents.

## Context

Decision 009 (2026-03-25) evaluated native `.claude/` features and skipped all of them, on the reasoning that dydo's hand-rolled orchestration and file-level RBAC were strictly more capable. Its "Revisit When" clause: reopen when Claude adds features that *complement* (not duplicate) the guard/orchestration layer.

Since then the landscape changed materially:

- **Dynamic workflows** (`.claude/workflows/*.js`, v2.1.154) provide deterministic, scripted fan-out of dozens-to-hundreds of subagents — the exact orchestration dydo emulated with terminals + filesystem messaging, done better.
- **Subagents are now governable.** A `PreToolUse` hook fires for a subagent's tool calls and the payload carries `agent_type` (the agent name) and `agent_id` (per-instance), even though `session_id` is shared with the parent. Agent-types are skill-preloadable and tool-restrictable.
- **Native auto-memory** is enabled by default and was being suppressed by dydo's write-gating.

The old model — orchestrators dispatching sub-orchestrators dispatching code-writers across many terminals — was scaffolding to make pre-workflow Claude behave. The native runtime now does the scaffolding.

## Decision

Adopt a **clean 2.0 rebuild** (not strangler-fig) that **trims more than it adds**. Port only the guard core and the documentation system; regenerate everything else as native artifacts that dydo compiles.

### 1. Two-tier identity

| Tier | Who | Form | Onboards |
|---|---|---|---|
| **Tier 1 — human-facing** | co-thinker / orchestrator / manager | named persistent identity (Adele…), main thread, one per terminal tab | `dydo agent claim` (kept) |
| **Tier 2 — Claude-managed** | code-writer, reviewer, test-writer… | subagent; identity = `agent_type` (role) + `agent_id` (instance) | **declarative** — spawned as a typed, skill-preloaded agent. No claim. |

Constraint accepted: subagents cannot spawn subagents. Nested orchestration = **nested workflows** or **another Tier-1 terminal**, never a sub-orchestrator subagent.

### 2. Drop per-role RBAC

Per-role writable/read-only path matrices are removed. The unique capability ("write, but only here") is not worth its cost once:
- parallel collisions are handled by **worktree isolation**,
- read-only roles are handled by **native `tools:` allowlists** on the agent-type,
- sensitive paths are handled by **universal off-limits + nudges**,
- context-loading is handled by **skills**.

The guard's retained job shrinks to **universal off-limits (hard) + nudges (soft)**, reading `agent_type` only to flavor a few role-specific nudges. This is the crown jewel and is role-agnostic.

Coarse tool/access scope uses **native agent permission profiles** (`tools` / `disallowedTools` / `permissionMode`), not a dydo-rolled mechanism. Sub-agents are **not named**: identified by `agent_type` (function) + `agent_id` (instance) + recorded parent lineage. Naming buys nothing — humans never address them, and audit keys on `agent_id`.

### 3. Roles split into native artifacts

Each role becomes: (a) a `.claude/agents/<role>.md` definition (personality + `tools` profile + preloaded role skill), and (b) a skill carrying the old mode-file working guidance + must-read context. dydo **generates** both. The guard no longer needs a permission entry per role.

### 4. Workspaces keyed on task-slice, not identity

Deterministic WIP location is preserved via `scratch/<slice>/` paths assigned by the orchestrator/workflow and passed in the worker prompt. Decouples scratch space from the (small) named-identity pool so swarms scale.

### 5. Un-suppress native memory

Native memory (`~/.claude/projects/*/memory/`) lives outside the repo and was denied by write-gating. The guard whitelists it. Coexistence model: **CLAUDE.md = human-authored rules; native memory = Claude's working notes; dydo docs = curated structured knowledge.** (Addresses the stale-state risk flagged in 009 by keeping docs canonical.)

### 6. dydo gains a compiler role

`dydo sync` emits `.claude/agents/`, `.claude/skills/`, and `.claude/workflows/` from role + doc definitions. dydo authors the native artifacts; Claude runs them.

### 7. Notion + Obsidian as views (new)

Canonical data stays as repo files (tasks, issues, decisions, progress). Obsidian = local docs view. Notion = team/PM dashboard with two-way sync. dydo owns the data and its shape; depend on neither for correctness, only for visualization.

## Fate Map

| Current capability | 2.0 disposition |
|---|---|
| **worker-tier** dispatch / inbox / queue / worktree-dispatch (junctions, merge dispatch) | **GONE** → native workflows + subagents (`isolation: 'worktree'`) |
| **top-level** dispatch (launch a named orchestrator/co-thinker in a terminal) | **KEPT** — no native equivalent; convenient |
| top-level ↔ top-level messaging / wait | **KEPT (light)** — coordination between the agents you talk to; worker messaging dies (workflows return structured output) |
| watchdog | **KEPT, slimmed** — top-level auto-close + orphan detection; **gains Notion sync** |
| orchestrator / sub-orchestrator roles | top-tier orchestrator runs workflows; nesting → nested workflows |
| per-role RBAC path matrix | **GONE** → off-limits + nudges + native tool allowlists |
| worker claim / per-worker named identity / workspace | **GONE** → `agent_type` + `agent_id`; scratch keyed on slice |
| mode files (co-thinker.md, code-writer.md…) | → **skills** |
| must-reads | → context preloaded into agent-types + light Tier-1 guard check |
| guard hook | **KEPT, slimmed** — universal off-limits + nudges, reads `agent_type` |
| nudges | **KEPT** — universal (crown jewel) |
| off-limits | **KEPT** — universal |
| inquisition | → **workflow** + `/inquisition` command |
| review flow | → **workflow** (find→verify) and/or skill |
| audit trail | **KEPT, re-keyed on `agent_id`**; fed via PostToolUse / SubagentStop hooks |
| docs model (understand/reference/guides, hubs, check/fix/index/graph) | **KEPT + grown** (project-context skill, optional MCP) |
| tasks / issues / backlog / decisions / changelog | **KEPT** as files; + Notion two-way sync |
| native memory | **UN-SUPPRESSED** |
| `dydo sync` (compiler) | **NEW** |
| Notion sync | **NEW** |

## Verification (spike, 2026-06-05 — RESOLVED, gate passed)

Tested empirically with a logging `PreToolUse` hook (`dydo/agents/Adele/spike/`). All unknowns resolved **positively**:

| Question | Result |
|---|---|
| Hooks live-reload mid-session? | **Yes** — editing `settings.local.json` takes effect immediately, no restart. |
| PreToolUse fires for Agent-tool subagent calls? | **Yes.** |
| PreToolUse fires for Workflow-spawned agent calls? | **Yes.** |
| Hook can hard-block (exit 2) inside both? | **Yes** — a marked Bash command was blocked in a subagent *and* in a workflow agent. Auto-approve (`acceptEdits`) mode does **not** bypass the hook; exit-2 is upstream of permission mode. |
| Payload identifies the actor? | **Yes** — `agent_id` (unique per instance; equals the agentId the Agent tool returns) + `agent_type`. `session_id` and `transcript_path` are **shared with the parent**. |
| `agent_type` carries the role? | **Yes, when a type is specified.** Agent-tool path surfaces the requested `subagent_type` (observed `general-purpose`); workflow path surfaces an explicit `agentType` (observed `Explore`). A bare `agent()` with no type defaults to the generic `agent_type: "workflow-subagent"`. |

**Design consequences (now load-bearing facts):**
- The guard must key identity on **`agent_id` / `agent_type`**, never `session_id` (subagents share the parent's). Absence of both fields is the Tier-1 (main-thread) signal.
- **`agent_type` = role** holds across both spawn paths → dydo generates `.claude/agents/<role>.md`; guard maps `agent_type → role` for nudges + audit.
- **Authoring constraint for `dydo sync`:** every worker spawn in a generated workflow MUST pass an explicit `agentType`, else the guard sees `workflow-subagent` and cannot resolve a role.
- A hook that errors hard (exit code other than 0/2 — e.g. a bad interpreter path; Python returns 2 on a missing script) **blocks every matched tool**. The guard must fail-open deliberately, and hook commands on Windows must use forward-slash paths (bash eats backslashes).

Residual (low-risk): a freshly-written *custom* agent type isn't picked up without a reload, so a custom `agent_type` was inferred from built-ins (which behave identically for this field), not directly observed. Confirm on next restart.

## Risks

- ~~Workflow workers may be ungoverned~~ **RETIRED** — verified the guard fires and hard-blocks for workflow-spawned agents (see Verification).
- **Loss of hard scope-confinement** from dropping RBAC. Mitigated by worktrees + read-only reviewer tools + review workflows. Revisit if scope-creep bugs become frequent.
- **Clean rebuild = flag-day risk.** Mitigated by porting the guard core and docs unchanged, and by gating on the verification spike.

## Revisit When

- Scope-creep without RBAC proves to be a frequent real failure (would reintroduce a thin, universal path-protection layer, not a per-role matrix).
