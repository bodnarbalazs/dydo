---
area: project
type: context
name: swarm-wave1-worklist
status: open
created: 2026-07-11
created-by: Adele
---

# Swarm Wave-1 Worklist (from Wave-0 triage of 77 open issues)

Companion to [mother-of-all-swarms-plan](./mother-of-all-swarms-plan.md). Produced by a read-only
Claude triage pass (staleness verified by grepping cited symbols at HEAD). Each batch's issues touch
**disjoint primary files** → parallel codex dispatch is collision-safe within a batch; batches run
sequentially. High-severity first. Dispatch doctrine: codex implements + tests + gates, Claude
reviews, Adele sequences landings (explicit paths, verify HEAD).

## Buckets
- **LIVE-FIX: 46** (8 batches below)
- **CODEX-INFRA: 8** — DEFERRED behind 0277-round2 + 0282; serialize internally
- **STALE/MOOT: 7** → bulk-resolve (0165 0209 0220 0221 0235 0248 0250)
- **DUPLICATE: 2** → 0241→0217, 0244→0223
- **SPRINT/CAMPAIGN: 13** → route (below)
- **EXTERNAL: 1** → 0180 (upstream Claude Code, won't-fix)

## Ordered disjoint batches (Wave 1)
- **Batch 1 (H+):** 0155 GuardCommand bash-chain guard-bypass · 0217 gap_check SOURCE_DIRS blind spot · 0110 code-writer release-with-0-commits destroys work · 0178 self-review task-name suffix defeat · 0205 anchor-link BrokenLinks false-positive · 0228 IsWatchdogProcess 350ms scan · 0258 half-resolved shadow promoted to canonical
- **Batch 2:** 0192 NOTICE no operator escape hatch · 0200 claim-auto livelock · 0252 no live sync-model regen · 0259 create-with-body orphan-on-retry · 0261 destructive-endpoint wire-shape test · 0260 notion-sync.md DR-035 drift · 0190 ResolveSessionFallback multi-human leak
- **Batch 3:** 0142 dispatch-in-background silent no-launch · 0229 wait host-liveness recycled-PID · 0257 reset --parent-page archives real board · 0246 claim onboarding dead-end docs · 0245 nupkg push glob · 0238 model status subcommand · 0219 _assets test tighten
- **Batch 4:** 0177 open-ended bash poll-loop crash · 0255 dispatch state-file write-race crash · 0164 hub exclusion unify · 0119 flaky FileReadRetry · 0136 flaky PathUtilsDiscovery · 0206 CheckDocValidator regression test · 0210 _backlog.template dangling ref
- **Batch 5:** 0212 Tier-2 file-nudge never fires · 0130 stale-working reclaim surfaces archive path · 0223 whoami/status stale RBAC display (absorbs 0244) · 0120 flaky WatchdogService · 0137 flaky WorktreeCommand · 0202 inquisitor worktree marker-vs-cwd · 0262 dispatch/troubleshooting doc drift
- **Batch 6:** 0213 guard daily-validation phantom nested tree · 0181 saturate-vs-claim cap race · 0242 dispatch --auto-close default · 0264 5 test-coverage gaps · 0275 inbox read-marker cosmetic
- **Batch 7:** 0251 non-dydo worktree uncleanable · 0135 flaky WatchdogService anchor-pid · 0156 (AFTER 0155) 0149 workaround doc revise · 0144 auto-resume reuse-window · 0243 msg metachar --body-file hint
- **Batch 8:** 0158 CreateListeningWaitMarker dead Since-branch

**Hotspot files serialized across batches (never concurrent):** GuardCommand.cs, AgentRegistry.cs,
DispatchService/DispatchCommand, WatchdogService.cs, SyncRunner.cs, NotionCommand.cs, WaitCommand.cs.
The batch ordering already keeps each hotspot to one issue per batch.

## Codex-infra cluster — DEFERRED + serialized (dispatch only AFTER 0277-round2 + 0282 land)
Internal parallelism once unblocked: {0263+0267 serial (Watchdog/TerminalLauncher)} ∥
{0272+0276 serial (SyncCommand.cs)} ∥ 0265 (ProcessUtils.Ancestry) ∥ 0280 (ReadCommand empty-target).
- 0282 codex python-spawn sandbox (ALSO a swarm readiness gate) · 0272 codex read-only→sandbox_mode ·
  0276 emitter skips inquisitor.toml · 0263 dead-in-effect post-codex surfaces · 0267 watchdog anchor
  hardcodes FindClaudeAncestor · 0265 node vendor-token regex unanchored · 0280 dydo read empty-target throw.

## Sprint/campaign routing
- **Identity/claim campaign** (heavy AgentRegistry/AgentSessionManager overlap — do NOT batch):
  0198, 0199, 0201, 0211, 0268, 0150 (auto-resume-never-fires; 0144 is the paired polish).
- **M0 spine sprint:** 0278 (FutureFeature titles + idea-color).
- **M1 / DR-036:** 0247 (task approve --all destructive → approval reform).
- **H1 validator sprint:** 0249 (+ 0205 from Batch 1 — residue closes once 0205 lands).
- **Systemic (Wave 2, balazs-flagged):** 0266 run-sprint/orchestrator skill fix (not C# — skill+docs).
- **Scheduled decisions:** 0236 (spine body-sync, Brian), 0112 (worktree-marker GC command),
  0279 (codex msg delivery via app-server — de-scoped from the swarm gate; workaround = self-release).

## Verify-cheap-then-close mop-up (single slice, not 3 dispatches)
0165, 0221 secondaries, 0235 converter-idempotency property test.
