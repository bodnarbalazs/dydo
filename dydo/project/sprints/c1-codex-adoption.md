---
title: C1 — Codex Adoption
campaign:
end:
gate-result: plan-review PASS (2026-07-09, fresh-eyes reviewer, 2 rounds)
seq: 9
start: 2026-07-09
status: audit
area: project
type: context
---

> **Plan-review verdict: PASS** (2026-07-09, DR-039 §2 gate, two rounds: FAIL 3 seam blockers →
> PASS with all fixes disk-verified incl. resolve-at-source achievability check). Status `active`:
> implementation launched same day under Grace (planner-orchestrates per DR-039 §1); Claude
> workers per the bootstrap exception; landings sequenced through the chief-of-staff. This sprint
> gates the v2.0.7 release.

# C1 — Codex Adoption

Make dispatched Codex sessions first-class citizens under the dydo guard. Origin: the 2026-07-09
live codex dispatch smoke (Iris) — the simple path works (claim via hook, role, msg), but every
stateful guard protocol fails on a codex host (issue 0254), the launcher emits a maximally
restrictive bare launch (issue 0253), and dispatch validation gaps (0239/0240/0237) let bad
dispatches fail downstream instead of fast. **C1 gates the v2.0.7 release** (balazs, 2026-07-09,
elevated via Adele); M0 implementation follows C1 by sprint-level ordering.

**Planner:** Grace (co-think with balazs 2026-07-09 → this plan; planned under the co-thinker
role with the planner skill — the planner ROLE lands in P1, same note as Brian's M0). **Gate:**
plan-review by a fresh-eyes reviewer per DR 039 §2 — routed through Adele. **No implementation
before the green light.**

**BOOTSTRAP EXCEPTION (from Adele's brief):** C1 is implemented by CLAUDE workers — codex cannot
operate under guard until C1 lands; that is the point.

## v2.0.7 acceptance ↔ slices (balazs's verbatim criteria)

| Criterion | Delivered by |
|---|---|
| good path usage | c1-4 (preflight fail-fast) + c1-5 (role validation) |
| auto-approved permissions (classifier posture, NOT yolo) | c1-3 (configured posture) |
| working flow | c1-1 (read verb) + c1-2 (durable wait) |
| can release | c1-1 (read-ack unwedges inbox clear) + c1-2 (release clears wait marker) |
| calls dydo commands correctly | c1-1/c1-2 onboarding docs + c1-7 (e2e) + c1-8 (live smoke) |

## Co-think outcomes (balazs at-terminal, 2026-07-09)

1. **Posture (0253):** `--sandbox workspace-write --ask-for-approval on-request`, config-surfaced
   with per-dispatch override. `untrusted` (the literal classifier policy) was rejected: it
   auto-approves only known-safe reads, so every dydo CLI call (a state-mutating external binary)
   would escalate to a human click — today's pain again. The sandbox is the enforcement boundary;
   the dydo guard hook remains project-boundary defense-in-depth. The dangerous-bypass flag is
   never emitted and not configurable.
2. **Release mechanics (0254):** one host-agnostic read verb that PRINTS content and registers
   the read (display-equals-ack, no blind acking), covering inbox items and must-reads. The
   release ceremony stays identical on every host — no codex-lenient path. Release also cleans up
   the agent's durable wait marker.
3. **MCP worker-lane marker: deferred** to the gated MCP-delegation experiment
   (`backlog/codex-mcp-delegation-experiment.md`). C1 covers the dispatched-session path only —
   for a dispatched Tier-1 codex host, the no-`agent_id` → Tier-1 routing is already correct
   (smoke: claim-via-hook worked). C1 keeps a hook-trust preflight check (c1-4).
4. **Missing Windows sandbox prerequisite:** fail-fast with the actionable setup instruction +
   documented setup — never silently degrade to a weaker sandbox (c1-4 checks, c1-3 documents,
   c1-8 verifies live).

## Slices (rows in `sprint-tasks/`, each born `ready`)

| Row | What | Kind | Isolation |
|---|---|---|---|
| c1-1-read-verb | host-agnostic `dydo read` — print + register (0254 lead) | code | worktree-safe |
| c1-2-durable-wait | marker-based wait registration (0254) + env-path nearest-host gate (0256 fold, HIGH — v2.0.7 holds for it) | code | worktree-safe, after c1-1 |
| c1-3-codex-posture | configured approval+sandbox launch posture (0253) | code | worktree-safe |
| c1-4-dispatch-preflight | fail-fast vendor/executable/sandbox/hook-trust checks (0239 generalized) | code | worktree-safe |
| c1-5-role-validation | dispatch `--role` validation + caller-role fix (0240+0237) | code | worktree-safe, after c1-2 |
| c1-6-model-provenance | exact-model capture + display map + whoami host/model | code | worktree-safe, after c1-2 & c1-3 |
| c1-7-codex-e2e-coverage | 0233's open asks + C1-path regression tests | test | worktree-safe, after code slices |
| c1-8-live-smoke | codex dispatch smoke re-run incl. resume + release | human-gated | n/a |

## Dependency order

```
c1-1 ∥ c1-3 ∥ c1-4               (disjoint by file)
c1-2   (after c1-1 — both touch Commands/GuardCommand.cs, Services/AgentRegistry.cs, and the wait/read doc surfaces)
c1-5   (after c1-2 — Services/AgentRegistry.cs chain: the 0237 fix threads dispatcher identity
        through AgentSelector/IAgentRegistry/AgentRegistry; plan-review resequence 2026-07-09)
c1-6   (after c1-2 AND c1-3 — GuardCommand.cs chain via c1-2; Services/ConfigFactory.cs chain via c1-3)
        c1-5 ∥ c1-6 (file-disjoint)
c1-7   (after all code slices — exercises landed seams; new test files by default, see row)
c1-8   (last; human at the terminal; pairs naturally with the v2.0.7 release candidate)
```

**Sprint-level ordering (balazs via Adele, 2026-07-09): C1 implements FIRST and gates v2.0.7; M0
follows.** Any change to this sequence goes through Adele.

## Full file footprint (cross-sprint disjointness)

Code (owner slice in parentheses; a file appears once unless chained above):
- `Commands/ReadCommand.cs` — NEW (c1-1)
- `Services/ReadTrackingService.cs` — NEW, extracted from `GuardCommand.TrackReadCompletion` (c1-1)
- `Commands/GuardCommand.cs` — c1-1 (extraction call sites) → c1-2 (`MissingGeneralWait`) →
  c1-6 (`InferModel`/`ParseInput`); serialized by the chain above
- `Services/InboxService.cs`, `Services/MustReadTracker.cs` (c1-1)
- `Commands/HelpCommand.cs`, `Program.cs`, `Services/CompletionProvider.cs` (c1-1 — new verb registration)
- `Commands/WaitCommand.cs`, `Models/WaitMarker.cs`, `Commands/AgentCommand.cs` (release :47,
  wait-check :732-738) (c1-2)
- `Services/AgentRegistry.cs` — c1-1 (read marks) → c1-2 (durable marker CRUD) → c1-5
  (`CanTakeRole` dispatcher threading, 988-992); serialized
- `Services/TerminalLauncher.cs`, `Services/WindowsTerminalLauncher.cs`,
  `Services/LinuxTerminalLauncher.cs`, `Services/MacTerminalLauncher.cs` (amended in — planner
  ruling 2026-07-09, review finding: codex lines exist on those platforms too),
  `Models/DispatchConfig.cs` (c1-3)
- `Services/ConfigFactory.cs` — c1-3 (posture defaults) → c1-6 (display-map defaults); serialized
- `Services/DispatchService.cs` + NEW `Services/DispatchPreflight.cs` (c1-4)
- `Commands/DispatchCommand.cs`, `Services/RoleDefinitionService.cs`,
  `Services/RoleConstraintEvaluator.cs`, `Services/AgentSelector.cs`,
  `Services/IAgentRegistry.cs` (c1-5)
- `Models/HookInput.cs`, `Models/ModelsConfig.cs`, `Services/ArtifactProvenance.cs`
  (display names resolved at the source — zero consumer edits), `Commands/AgentListHandler.cs`,
  `Commands/WhoamiCommand.cs` (c1-6). NOT touched: `MessageService.cs`, `IssueCreateHandler.cs`,
  `ReviewCommand.cs`, `TaskCreateHandler.cs` — the latter two are M1-S2a's.
- `DynaDocs.Tests/**` — each code slice owns its neighboring test files (named per row); c1-7
  defaults to NEW files (`DynaDocs.Tests/Integration/Codex*` family) and may add to an existing
  home only where no C1 slice edited it (verify-before-touch — see the row).

Docs (6-surface rule, `dydo/guides/adding-a-command.md`):
- `dydo/reference/dydo-commands.md` + `Templates/dydo-commands.template.md`,
  `dydo/reference/about-dynadocs.md` + `Templates/about-dynadocs.template.md`,
  `Commands/HelpCommand.cs`, `DynaDocs.Tests/Commands/CommandSmokeTests.cs` — c1-1 (new `read`
  verb); c1-2 re-touches the wait sections of the two dydo-commands surfaces (serialized after
  c1-1). **Ripple watch:** if `CommandDocConsistencyTests` drags in further README-family
  surfaces, report to Adele before landing — don't absorb silently.
- `dydo/reference/configuration.md` (+ its template if clone-synced — worker greps) — c1-3
  (posture keys) then c1-6 (display-map key); serialized by the c1-3 → c1-6 chain.
- `dydo/_system/templates/agent-workflow.template.md` + codex onboarding prose (claim is a manual
  step; reads via `dydo read`; wait registration) — c1-1 adds read guidance, c1-2 adds wait
  guidance (serialized). NOTE: these templates have uncommitted working-tree edits from another
  stream — workers rebase on current state, and flag to Adele if the edits look mid-flight.
- Issue Resolution sections + `resolved/` moves on land: 0254 (c1-1+c1-2 jointly — resolve when
  both landed), 0256 (c1-2, the fold), 0253 (c1-3), 0239 (c1-4), 0240+0237 (c1-5), 0233 (c1-7),
  `backlog/exact-model-provenance-display.md` → resolved/absorbed (c1-6). 0256 doc corrections
  (also c1-2's): issue 0250's Resolution text ("env path already ownership-checked" is false)
  and `backlog/codex-mcp-delegation-experiment.md` post-0250 claim (:122-125).

Cross-sprint seams (declared per the pipeline posture):
- `Services/CompletionProvider.cs` — c1-1 here, m0-4 later: **resolved by the C1-first sprint
  ordering**, exactly the seam Brian's M0 record already declares.
- **The six command-doc surfaces are shared with m0-4** (a code slice adding its own new verb):
  `Commands/HelpCommand.cs`, `DynaDocs.Tests/Commands/CommandSmokeTests.cs`,
  `dydo/reference/dydo-commands.md` + `Templates/dydo-commands.template.md`,
  `dydo/reference/about-dynadocs.md` + `Templates/about-dynadocs.template.md` — c1-1 touches all
  six (c1-2 re-touches the dydo-commands pair). Ordering-safe under the same C1-first sprint
  ordering; declared here per the pipeline posture (plan-review finding, 2026-07-09).
- M1-S2a's files (`Commands/Task*Handler.cs`, `ReviewCommand.cs`, `WorkspaceCommand.cs`,
  `WorktreeCommand.cs`) — untouched by every C1 slice (c1-6 deliberately resolves provenance at
  the source so `ReviewCommand.cs`/`TaskCreateHandler.cs` need zero edits). Fully disjoint.
- M0 docs-only slices (m0-2/3/5) — no shared files with C1.

## Gates (every code slice, exact commands)

- `python DynaDocs.Tests/coverage/run_tests.py` — green, `CommandDocConsistencyTests` included
- `DynaDocs.Tests/coverage/gap_check.py --force-run` (Commands/ and Services/ touched everywhere)
- `dydo check`

## Prior-art & evidence (DR 039 §4 obligation)

- **Codex CLI posture flags** verified 2026-07-09 against the official CLI reference
  (developers.openai.com/codex/cli/reference): `--ask-for-approval {untrusted|on-request|never}`
  (`on-failure` is DEPRECATED — do not emit), `--sandbox {read-only|workspace-write|danger-full-access}`,
  `-c key=value` repeatable config overrides, `--dangerously-bypass-approvals-and-sandbox`
  (alias `--yolo`) exists and is banned here. Re-verify at implementation time (0231 precedent).
- **Read registration** already exists in-repo: `GuardCommand.TrackReadCompletion`
  (GuardCommand.cs:646-666) marks must-reads and inbox messages from observed Read calls — c1-1
  extracts and reuses it; no parallel implementation.
- **Model display map:** verified none exists (grep over ModelsConfig/ArtifactProvenance/
  ConfigFactory, 2026-07-09); provenance stores raw ids verbatim. New map's home is the DR 028
  tier config per `backlog/exact-model-provenance-display.md`.
- **Fail-fast validation pattern:** DR 037 §6 / issue 0239's expected shape (name the missing
  prerequisite + the fix); existing seam `DispatchService.CanResolveLaunchExecutable` (87-100).

## Rollback story

Every slice is plain git-revertable code on a branch. New config sections are additive with
absent-section = current-behavior defaults (DR 028 precedent), so no config migration and no
rollback hazard. No live-board or state-file mutations; c1-8 mutates nothing in the repo.

## Out of scope

- MCP-codex worker-lane marker, mcp-server registration probes, approvals-through-MCP, stop-hook
  parity — all stay on `backlog/codex-mcp-delegation-experiment.md` (balazs, co-think §3).
- Issue 0265 (low — node-ancestor vendor classification substring-matches anywhere in the
  cmdline): adjacent to the 0256 fold but explicitly DEFERRED (planner call on Adele's routing,
  2026-07-09) — post-C1 hardening.
- Planner role, reviewer subskills, `requires-prior` vocabulary changes (P1 / DR 039 R1–R4) —
  c1-5 fixes constraint *evaluation*, not the constraint *set*.
- M0/M1 files (see seams above); Notion sync surfaces.
- Codex-side automatic message push — codex hosts poll via inbox; real-time delivery parity is
  future work if the smoke shows it matters.

## Watch-outs

- **Guard self-modification hazard:** c1-1/c1-2/c1-6 edit `GuardCommand.cs` — the very hook the
  workers run under. Workers build/test in their worktree only; never install a locally built
  binary over the live `dydo` on PATH mid-sprint.
- **Codex flag drift:** the CLI reference marks `on-failure` deprecated — emit only the verified
  values; c1-3's tests assert the exact emitted command line.
- **Display-equals-ack:** `dydo read` must never register a read without emitting the content
  (weakens the must-read guarantee); reviewer checks this property explicitly.
- **No model names in plan text** (DR 039): c1-6 seeds display names from
  `backlog/exact-model-provenance-display.md` + the ids already in the tier config — neither this
  record nor the rows enumerate them.
- **Windows sandbox interaction:** sandbox path-virtualization can break path-keyed hook trust
  (Noah's probe). c1-4's trust check must key on what codex actually resolves; c1-8 verifies the
  posture+trust combination live before release.
