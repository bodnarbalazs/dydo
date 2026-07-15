---
area: project
type: context
name: simplification-campaign-handback
status: open
created: 2026-07-15
created-by: Adele (Fable)
---

# DR-041 Simplification Campaign — handback notes (Rail A complete)

Rail A executed 2026-07-15 by Fable in-conversation, per [DR-041](../decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md) and the [campaign plan](./simplification-campaign-plan.md). One slice per commit, green ratchet (build 0/0 + full suite 0 failed) after every slice; the final carve also passes `gap_check` 134/134.

## The slice commits (review in order)

| Commit | Slice | Suite after |
|---|---|---|
| `c6a46f23` | Pre-campaign baseline (DR-036 reform + 0295 + chunking) | 4847/0 |
| `ca136f9a` | 1a whoami | 4836/0 |
| `27bc7cb8` | 1b wait + guard pending-state/H20 strand | 4719/0 |
| `6f61e9e0` | 1c messaging (message/inbox/read + services + verdict routing) | 4599/0 |
| `f6435c35` | 1d dispatch (+ preflight, selector, options) | 4479/0 |
| `d6644c29` | 1e workspace | 4432/0 |
| `7394bb06` | 2a guard identity-hollow (+ TaskDone/Review gates stripped) | 4292/0 |
| `221b609a` | 1f agent CLI + clean + hand | 4105/0 |
| `28493845` | 2b watchdog→stub + terminal-launcher cluster | 3508/0 |
| (pending) | 3 AgentRegistry carve | 3009/0, gap_check 134/134 |

## Judgment calls to eyeball (most important first)

1. **Provenance is gone entirely.** With no sessions, agent/vendor/model provenance stamping could only ever be null, so the call sites were deleted (TaskCreate, IssueCreate, ReviewCommand — reviewer now records as `Unknown`) and `ArtifactProvenance` with them. If per-artifact model provenance matters in the new world, it needs a new source (e.g. env var from the platform), as a fresh feature.
2. **Guard git-safety semantics changed shape.** Stash/merge rules were per-agent-workspace-marker based (needs identity); reworked to CWD-based: `git stash` blocked outside a worktree, `git merge` blocked inside one. Same intent, simpler trigger — confirm it matches your expectations.
3. **Off-limits onboarding files now block reads for everyone.** The staged bootstrap read-bypass is gone; readability is purely the `files-off-limits.md` list. That list still contains agent-era patterns (`state.md`, `workflow.md`, `.session`, dead `.guard-lift.json`) — it is POLICY CONTENT, i.e. yours to prune on Rail B.
4. **`dydo init --agents` / roster scaffolding DEFERRED** — the one substantial DR-041 item left in code. Removing it needs an `AgentNames`-fallback redesign (empty pool currently breaks the worktree merge scan) and intersects your Rail B template decisions (init generates workflow.md). Recommend it as the first follow-up slice.
5. **Model-cap auto-restore is inert.** `ModelCapService.RestoreExpired` works and is tested, but the watchdog tick that drove it is gone. Decide where cap-restore lives (guard-trigger? the future Notion daemon?) — doc prose about it left as-is pending that.
6. **Stop hook is a no-op.** `dydo guard --stop` returns success so existing hook wiring doesn't break; the agent-state needs-human writer died. The `needs-human:` task-frontmatter flag (PM content) is preserved.
7. **Warn-severity nudge marker moved** from per-agent workspace to a single global marker under the workspace root.
8. **Worktree merge resolves by `.merge-source` marker scan** (first workspace holding one) instead of claimed identity.
9. **Clean follow-up deletes:** `ConfigService.ValidateAgentClaim` + `AgentClaimValidator` (orphaned but green), `GetWorktreeId` (source-orphaned accessor), dead `.guard-lift.json` off-limits default.

## Stale prose (Rail B — yours)

- `about-dynadocs.md`/`.template` + `README.md` onboarding sections still describe `dydo agent claim` (~lines 162–175 pre-carve numbering).
- `Templates/agent-workflow.template.md`, `index.template.md`, `coding-standards.template.md`, mode templates — the whole claim-ceremony narrative.
- `dydo/understand/guard-system.md` + `architecture.md` guard sections still describe staged onboarding.
- Model-cap doc prose says "the watchdog restores the original bindings".
- `dydo/reference/guardrails.md` was updated mechanically; worth a read-through for tone.

## Still to do before shipping 2.1.0

- [ ] init/roster removal slice (item 4).
- [ ] Notion-sync daemon rebuild in the watchdog stub (+ OSS Obsidian↔Notion research task) — DR-041 resolved-3.
- [ ] Rail B prose pass (above).
- [ ] **Re-arm the guard** (`notguard` → `guard`, yours) and re-verify hooks fire.
- [ ] Update DR-041 frontmatter `status: proposed` → accepted, and the campaign plan status.
- [ ] Your release ritual (version bump is yours; tree is committed slice-by-slice).
