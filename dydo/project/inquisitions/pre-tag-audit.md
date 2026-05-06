---
area: project
type: inquisition
---

# Inquisition: Pre-Tag Audit (release readiness)

Final integration-level audit before balazs pushes the release tag. Two batches landed
in this session — `dydo-check-drift` (rule scanning + types vocabulary + hub format)
and `runtime-regression` (test isolation + git-helper drains + production process
callers + gap_check exit-propagation) — plus several smaller fixes. All review-passed
individually; this report looks for what survives the cracks **between** them.

## 2026-05-06 — Brian

### Scope

- **Entry point:** Feature investigation — pre-tag integration audit per Adele's brief
  (`dydo/agents/Brian/inbox/e38bba4a-pre-tag-audit.md`). Retry of the prior attempt that
  crashed mid-run.
- **Wave under audit (commits, master):**
  - PowerShell cd-chain extension: `9d2474e`
  - dydo-check-drift batch (PR1-PR3): `fc83e31`, `3213931`, `8b71cd4`, `d05f696`,
    `c85947a`, `5c77bbb`, `cbd063f`
  - Runtime-regression batch (PR1-PR4): `12e30e9`, `405a220`, `e3e6c47`, `4c5a0c8`,
    `6d00b4c`, `a3654be`, `4751aeb`
  - Other fixes: `844579f` (#0166), `3ad12ba` (clean --force gate), `d00fa0c` (gate doc)
- **Concerns probed:** A (BC migration), B (cross-batch interaction), C (post-wave
  flakies), D (plan deviations — redirectStdin default + audit-replay substitution),
  E (Emma's gap_check hang theory), F (binary-on-PATH state), G (open OOS items),
  H (integration smoke), I (audit-trail spot check), plus an unsolicited audit of
  closed-issue bookkeeping that surfaced finding #1 below.
- **Scouts dispatched:** 0. Per defensive note in brief — minimal fan-out, save
  incrementally, single probe on hang-prone phases.

### Findings

#### 1. Closed-issue bookkeeping drift: every issue claimed-closed this session except #0148 still carries `status: open`; #0167 has no `## Resolution` section AND a duplicated `## Description` heading

- **Classification:** release-readiness (issue tracker / closed-state hygiene).
- **Severity:** medium (does not block tag mechanically, but every "closed in this session" issue is still indexed as open until someone walks the list).
- **Type:** obvious.
- **Evidence:**
  - The brief's `ISSUES CLOSED THIS SESSION` list: `#0159, #0160, #0161, #0162, #0163, #0164 (partially), #0166, #0167, #0168, #0169, #0170, plus #0148`.
  - Frontmatter `status:` field, sampled directly from each issue file:
    - `dydo/project/issues/0148-…md:6` — `status: resolved` ✓
    - `dydo/project/issues/0159-…md:6` — `status: open` ✗
    - `dydo/project/issues/0160-…md:6` — `status: open` ✗
    - `dydo/project/issues/0161-…md:6` — `status: open` ✗
    - `dydo/project/issues/0162-…md:6` — `status: open` ✗
    - `dydo/project/issues/0163-…md:6` — `status: open` ✗
    - `dydo/project/issues/0164-…md:6` — `status: open` ✗ (brief flags as "partially" closed, so this one is at least debatable)
    - `dydo/project/issues/0166-…md:6` — `status: open` ✗
    - `dydo/project/issues/0167-…md:6` — `status: open` ✗
    - `dydo/project/issues/0168-…md:6` — `status: open` ✗
    - `dydo/project/issues/0169-…md:6` — `status: open` ✗
    - `dydo/project/issues/0170-…md:6` — `status: open` ✗
  - `## Resolution` section presence (`grep -lE '^## Resolution' …`):
    - Present: #0148, #0159, #0160, #0161, #0162, #0163, #0164, #0166, #0168, #0169, #0170.
    - **Absent: #0167** — the highest-severity issue in the runtime-regression batch (assembly-wide DisableTestParallelization). The commit `405a220` exists and lands a fix, but the issue file documents only the original "Fix path" (per-collection flags) and never records that the actual landed fix was broader (assembly-wide disable per plan OQ1=A). A reader reaching the issue file alone cannot tell what shipped.
  - **Additional quality issue in #0167:** lines 13-15 contain two consecutive `## Description` headings — the stub `## Description` from `dydo issue create` was never collapsed when the body was backfilled. Pre-#0161 issue stubs had no `--summary` flag; PR3 (`c85947a`) made the CLI emit clean stubs and `cbd063f` backfilled summaries on `#0151-#0158`. **#0167 was missed in that backfill** (Brian's `cbd063f` covered only `#0151-#0158` — the issues created before #0161's CLI fix; #0167 is post-fix but was created with `--summary` empty in a way that left the doubled heading). Reading the file as-is, the body parses but the heading hierarchy is broken.
  - The brief's first acceptance criterion is *"Verify each filed issue is genuinely closed by its referenced commit(s)."* That verification can be done by reading the commits (and was — every claimed commit lands the described change) but the issue tracker itself does not reflect closure. After the tag ships, anyone running `dydo issue` listing or filtering by `status: open` will still see all 11 of these as open work.
- **Why this matters at the tag boundary:**
  - Project hygiene at a release boundary is part of the audit (`status: resolved` is the project's convention; `#0148` proves the convention is live and was followed for this session's earliest closure).
  - The missing `## Resolution` on `#0167` specifically is load-bearing: the actual fix differed from the issue's documented "Fix path" (`405a220` chose the assembly-wide invariant per plan OQ1=A; the issue body still describes the per-collection-flag plan). Future readers landing on `#0167` from a `git log --grep` search will read a fix path that does not match what shipped.
- **Proposed fix path (small but mandatory before tag, in my opinion):**
  1. Flip `status: open` → `status: resolved` in the 10 issues actually closed this session (#0159, #0160, #0161, #0162, #0163, #0166, #0167, #0168, #0169, #0170). Leave `#0164` `open` if the "partial" flag in the brief means there's still residual work (architectural refactor of skip-pattern duplication wasn't fully done — only the helper hoist landed; SummaryRule/BrokenLinksRule/NamingRule still each call `RuleSkipPaths.IsTemplateOrAddition` rather than getting it for free from a base-class default — see `Rules/BrokenLinksRule.cs:21`, `Rules/FrontmatterRule.cs:30`, `Rules/NamingRule.cs:16`, `Rules/SummaryRule.cs:14`).
  2. Add a `## Resolution` section to `#0167` describing the actual landed shape (assembly-wide `[assembly: CollectionBehavior(DisableTestParallelization = true)]` per `DynaDocs.Tests/AssemblyInfo.cs:1`, gate-bypass migration of three sites, plus the `ParallelisationDisabledTests` contract pin in `RuntimeRegression/`). The `4c5a0c8` commit added a coding-standards section about test parallelism but did not update the issue file.
  3. Collapse the doubled `## Description` heading at `dydo/project/issues/0167-…md:13-17`. Mechanical edit.

- **Judge ruling:** CONFIRMED
- **Files examined:** `dydo/project/issues/{0148,0159,0160,0161,0162,0163,0164,0165,0166,0167,0168,0169,0170}-…md` frontmatter (grep `^status:`); `dydo/project/issues/0167-…md` full body (lines 1-71); `DynaDocs.Tests/AssemblyInfo.cs:1`; `git log --oneline -5 -- DynaDocs.Tests/AssemblyInfo.cs`.
- **Independent verification:** Grep over the 13 cited files returned exactly one `resolved` (#0148) and twelve `open` — including #0165 which is correctly tracked as still-open. The 11 remaining open issues match the brief's "closed this session" list one-for-one. Read #0167 in full: doubled `## Description` heading sits at lines 13 and 15 (literal back-to-back); no `## Resolution` section exists anywhere in the body; the "Fix path" section (lines 56-63) describes the per-collection-flag plan, while `git log` shows the actual fix shipped via commit `405a220` titled "fix(tests): disable assembly-wide xUnit parallelism + migrate gate-bypass sites" — broader than the documented plan. The single line in `DynaDocs.Tests/AssemblyInfo.cs` is the assembly-wide invariant.
- **Alternative explanations considered:** The `status: open` could in principle be intentional "wait for human acceptance before flipping" — but #0148 (closed earlier this session) is already `resolved` and shows the project's actual convention is to flip on close. The doubled heading could be a renderer quirk — but the raw file genuinely has two consecutive `## Description` headings and no parser interpretation changes that. The Resolution-vs-Fix-Path drift is real: a future reader landing on #0167 from `git log --grep` will read a fix path that does not match what shipped.
- **Issue:** No standalone issue — the work is captured by must-fix items 1-5 (status flips, Resolution backfill, heading collapse, summary backfills, hub regen).

#### 2. BC migration story (Concern A) — clean for live runtime; one papercut on `dydo fix` not auto-creating `_system/types.json`

- **Classification:** clean (with a minor docs/ergonomics nudge).
- **Severity:** low.
- **Type:** obvious + walked-through.
- **Evidence (walked end-to-end on a hypothetical LC-shaped project: existing `dydo.json` without `scanExclude`, no `_system/types.json`, existing `tasks/_index.md`, hand-edited `project/_index.md`, worktree active):**
  - **scanExclude invariants:** `Services/ConfigFactory.cs:178-182` — `FindMissingScanExcludeInvariants` returns the dydo-internal entries (`_system/.local/`, `_system/audit/`) missing from user's `dydo.json`. `Commands/CheckConfigValidator.cs:14-26` — `dydo check` emits one error per missing entry with a literal "Run 'dydo fix' to restore it" hint. `Commands/FixCommand.cs:115-132` — `dydo fix` calls `ConfigFactory.EnsureDefaultScanExclude` and writes the config back. **End-to-end: `dydo check` fails loudly, `dydo fix` heals it. Clean.**
  - **types.json:** `Services/FrontmatterTypesService.cs:33-61` — when `_system/types.json` is missing, falls back to `Frontmatter.ValidTypes` (which already includes `inquisition` per `Models/Frontmatter.cs:12` after `3213931`). So at runtime, **a project with no types.json behaves correctly** — every baseline type validates, including `inquisition`. types.json is only needed if the user wants to *extend* the vocabulary.
  - **types.json creation paths:** `Commands/TemplateCommand.cs:114, 197-232` — `dydo template update` creates `_system/types.json` from `Templates/types.json.template` if missing, or merges baseline into existing user-edited content. `Services/FolderScaffolder.cs` writes it on `dydo init`. **`Commands/FixCommand.cs:115-132` does NOT create types.json** — only scan-exclude invariants are restored.
  - **Existing `tasks/_index.md`:** `Commands/FixHubHandler.cs:138-150` — `DeleteStaleTasksIndex` deletes the file ONLY if it contains the `<!-- Auto-generated by 'dydo fix'. Do not edit -->` banner. Hand-written content is preserved. **Clean.**
  - **Hand-edited `project/_index.md`:** `Services/HubGenerator.cs:25-70` — `GenerateHub` rewrites the file completely (auto-gen banner included). On the first `dydo fix` after upgrade, a hand-edited `project/_index.md` would be overwritten. This is **pre-existing behavior** for any auto-generated hub (banner explicitly says "Do not edit"); D4's addition of the hardcoded `## Tasks` prose simply makes the diff slightly larger. Not a new BC hazard from this batch.
  - **Worktree-active during upgrade:** `dydo template update` operates on the worktree's checkout (`dydo.json`, `_system/types.json` are all worktree-local). Junctions for `dydo/agents/`, `dydo/_system/roles/`, `dydo/project/issues/`, `dydo/project/inquisitions/` (per `understand/architecture.md` "Worktree Dispatch" §3) point back to main repo state — none of those are touched by `template update`. The new `scanExclude` invariants reference `_system/.local/worktrees/`, which from inside a worktree is empty; the exclusion entries do no harm there and become load-bearing the moment the user exits the worktree and runs `dydo check` from the main tree. **Clean.**
- **The papercut:** the only end-to-end gap is that `dydo fix` doesn't auto-create types.json. Today this is fine because runtime degrades gracefully and the only consumer (`FrontmatterRule`) gets the baseline including `inquisition`. But the asymmetry is mildly surprising — `dydo fix` heals scanExclude but not types.json, while `dydo template update` heals both. A future user who wants to add a custom type will discover the right command (`dydo template update`) only by reading the docs, not from any `dydo check` warning.
- **Proposed action:** none required for tag. Optional follow-up: add `EnsureTypesJson(dydoRoot, diff: false)` to `FixCommand` alongside the existing `RestoreScanExcludeInvariants`, so `dydo fix` covers both invariants symmetrically. Or, alternatively, document the asymmetry in `reference/dydo-commands.md` and call it design-intent.
- **Files examined:** `Models/Frontmatter.cs:12`, `Services/ConfigFactory.cs:155-208`, `Services/FrontmatterTypesService.cs` (full), `Services/FolderScaffolder.cs`, `Services/HubGenerator.cs:1-70, 140-190`, `Commands/CheckCommand.cs:38-50`, `Commands/CheckConfigValidator.cs` (full), `Commands/FixCommand.cs:115-132`, `Commands/FixHubHandler.cs:1-150`, `Commands/TemplateCommand.cs:80-232`. Walked the upgrade flow on this worktree's checkout.

- **Judge ruling:** CONFIRMED (clean — observation only)
- **Files examined:** `Models/Frontmatter.cs` (full), `Services/ConfigFactory.cs:115-208`, `Services/FrontmatterTypesService.cs` (full), `Commands/CheckConfigValidator.cs` (full), `Commands/FixCommand.cs:100-133`, `Commands/FixHubHandler.cs:125-150`.
- **Independent verification:** `Frontmatter.ValidTypes` includes `"inquisition"` (line 12). `FrontmatterTypesService.LoadAndMerge` returns `Frontmatter.ValidTypes` directly when types.json is missing (lines 36-37) — gracefully degraded. `CheckConfigValidator.Validate` emits the literal hint "Run 'dydo fix' to restore it" (line 22). `FixCommand.RestoreScanExcludeInvariants` calls only `EnsureDefaultScanExclude` (line 125) and `SaveConfig` — no types.json touch. `DeleteStaleTasksIndex` is banner-gated against the `HubGenerator.AutoGenComment` constant (line 145) — hand-edited files are preserved.
- **Alternative explanations considered:** The asymmetry between `dydo fix` (heals scanExclude only) and `dydo template update` (heals both) could be design-intent — runtime degrades gracefully because every consumer (`FrontmatterRule`, etc.) gets the baseline that already includes `inquisition`. No `dydo check` warning surfaces a missing types.json, so the user has no signal anything is wrong until they try to add a custom type. Reasonable as a deferred ergonomics improvement.
- **Issue:** None — papercut is post-tag optional per Brian's recommendation.

#### 3. Cross-batch interaction (Concern B) — both sub-concerns clean

- **Classification:** clean.
- **Severity:** N/A.
- **Type:** obvious.
- **Evidence:**
  - **Sub-concern B1 (DocScanner ↔ WorktreeCommand during `dydo check` on a worktree):** `Services/DocScanner.cs:17-33` — pure filesystem walk + frontmatter parse. No process spawns. `Commands/CheckCommand.cs` invokes `DocScanner.ScanDirectory` and the rule pipeline; nothing in `CheckCommand` touches `WorktreeCommand`. The two systems are orthogonal at the call graph level — there is no shared state through which `RunProcessSilent`'s rewire (`Commands/WorktreeCommand.cs:620-640`, now via `ProcessUtils.RunProcessCapture`) could affect a scan in flight.
  - **Sub-concern B2 (assembly-wide `DisableTestParallelization` masking RuleSkipPaths tests that needed parallel):** the new tests landed by `8b71cd4` (`SummaryRuleTests` template/template-addition skip cases, `OrphanDocsRuleTests` project/tasks skip + `_tasks.md` guard) are pure-unit assertions on `Rules/*` — they construct rule instances, feed `DocFile` fixtures, and assert on the returned validation results. None of them depend on parallel execution; serializing them is safe by construction. `DynaDocs.Tests/AssemblyInfo.cs` (added by `405a220`) sets the assembly-wide flag; this slows the suite by ~50s of sequential overhead per `dydo/guides/coding-standards.md:223` but does not change *which* tests pass or fail.
- **Action:** none.
- **Files examined:** `Services/DocScanner.cs` (full), `Commands/CheckCommand.cs:30-60`, `Commands/WorktreeCommand.cs:560-670`, `DynaDocs.Tests/AssemblyInfo.cs`, `dydo/guides/coding-standards.md:222-228`.

- **Judge ruling:** CONFIRMED (clean)
- **Files examined:** `Services/DocScanner.cs` (full); grep `WorktreeCommand|WorktreeService` over `Commands/CheckCommand.cs` (zero matches); `git show --stat 8b71cd4` (commit message + diff stats).
- **Independent verification:** B1 — `DocScanner.ScanDirectory` is `Directory.GetFiles` + per-file parse; no process spawn, no `WorktreeCommand` reference anywhere in the call graph. The grep returned 0 hits, confirming orthogonality at the source-text level. B2 — commit `8b71cd4` body explicitly describes the new tests as `SummaryRuleTests`, `OrphanDocsRuleTests`, `HubFilesRuleTests` covering rule-skip behaviour; these are pure unit tests on `Rules/*` with no shared mutable state, so serializing them is a no-op for correctness.
- **Alternative explanations considered:** Shared static state would be the only path that could couple DocScanner to WorktreeCommand's `RunProcessSilent` rewire — `ProcessUtils.RunProcessCapture` is stateless and instance-free, so the coupling cannot exist.
- **Issue:** None.

#### 4. Plan deviations (Concern D) — both deviations are safe in the as-shipped state but the `redirectStdin = false` default is not fail-safe for future callers

- **Classification:** observation (D-1: design-default that requires future-caller diligence; D-2: clean substitution).
- **Severity:** low.
- **Type:** obvious + walked-through.
- **Evidence (D-1, `ProcessUtils.RunProcessCapture` redirectStdin defaults to false):**
  - `Services/ProcessUtils.cs:129-135` — signature: `RunProcessCapture(fileName, arguments, workingDir = null, timeoutMs = 5000, environment = null, redirectStdin = false)`.
  - All four production callers using `ProcessUtils.RunProcessCapture` (the new helper) without explicit `redirectStdin: true`:
    - `Services/AuditService.cs:273` — `git rev-parse --short HEAD`. Local read, no network, no credential prompt path.
    - `Services/FileCoverageService.cs:293` — `git $arguments` (caller passes ls-files-style arguments per `IFileCoverageService` callers). Local index reads.
    - `Services/SnapshotService.cs:52` — `git rev-parse HEAD`. Local read.
    - `Services/SnapshotService.cs:63` — `git ls-files --full-name`. Local index read.
  - The one caller that *does* set `redirectStdin: true`: `Commands/WorktreeCommand.cs:632-638` (`RunProcessSilent`). It also sets `GIT_TERMINAL_PROMPT=0` via the `GitNoPromptEnv` dictionary. Belt-and-suspenders.
  - **Net assessment of D-1:** all four production sites are *local* git invocations with no network surface; none can hit a credential prompt. The `redirectStdin = false` default is safe today. **But** the default does not fail-safe — a future caller using this helper for `git fetch`, `git push`, or any networked op will silently inherit the parent's stdin and could block on a credential prompt. The plan recommended `true` for exactly this reason.
  - **Soft mitigation already in place:** the code-writer who added the helper documented the contract in the XML doc-comment at `Services/ProcessUtils.cs:120-128` ("redirectStdin: When true, redirects stdin and closes it immediately to signal EOF — required for git invocations that must not block on a credential prompt"). A diligent future caller will read it. An undiligent one won't.
- **Evidence (D-2, audit-replay phantom command substitution):**
  - `dydo/project/changelog/2026/2026-05-06/implement-pr4-production-drain.md:51,118` — Charlie documents the substitution clearly: "The plan's verification step 4 mentions dydo audit replay byte-for-byte — that subcommand is not exposed in the CLI. Substituted dydo inquisition coverage --since 30 as the live-data probe."
  - `dydo/agents/Charlie/dispatch-brief-pr4-review.md:52` — review brief flags it back: "The dydo audit replay surface gap above — either expose a replay subcommand or document the verification step differently."
  - `dydo/agents/Adele/inbox/8e147268-msg-implement-pr4-production-drain.md:49` — Adele's own report carries the same surface flag.
  - **CLI inventory check:** `grep -rn "audit replay" Commands/` returns nothing — the subcommand genuinely does not exist. The plan referenced a phantom. The substitution exercises `HasChangesSince` through the new helper (`InquisitionCommand.cs:175-181` — one of the six PR4-migration sites), so it does provide live coverage of the production change. Reasonable substitute.
  - **Net assessment of D-2:** the substitution was correct and the substitution rationale is documented in two independent places (the changelog and the review brief). Tracking the underlying surface gap (no `dydo audit replay` exists) is Concern G's job — see finding #5 below.
- **Action:**
  - **D-1:** Optional follow-up: flip the default to `redirectStdin = true` after a quick survey of all callers (the four cited above explicitly want stdin inherited from the parent so they integrate with no-stdin contracts; a `true` default would force them to opt out, which is the safer fail-mode). Out of scope for this audit because the as-shipped behavior is correct; just call out the design choice.
  - **D-2:** Out-of-scope here — the audit-replay surface gap is logged via Concern G (see finding #5).

- **Judge ruling:** CONFIRMED (with one minor inaccuracy in Brian's caller count)
- **Files examined:** `Services/ProcessUtils.cs:115-160`; full grep for `RunProcessCapture(` over `*.cs`; `Commands/InquisitionCommand.cs:160-180`; `Commands/WorktreeCommand.cs:625-645`; grep `audit replay` over `Commands/`.
- **Independent verification:** D-1 — Brian listed four production callers without `redirectStdin: true`; the actual count is **five**. He missed `Commands/InquisitionCommand.cs:169` (`git diff --stat HEAD@{since}` for `HasChangesSince`). All five are still local-only git reads with no network/credential surface, so the substantive risk assessment is unchanged. The single explicit `redirectStdin: true` site at `Commands/WorktreeCommand.cs:632-638` (with `GitNoPromptEnv`) reproduces as Brian described. D-2 — grep `audit replay` in `Commands/` returns zero matches; the subcommand genuinely does not exist.
- **Alternative explanations considered:** `redirectStdin = false` default could be the deliberate ergonomic for the existing local callers (forcing them to opt out of stdin inheritance would change behaviour for nothing). Brian's call to flip the default is a valid hardening but not a tag blocker — the as-shipped behaviour is correct and the contract is documented at `ProcessUtils.cs:128`.
- **Issue:** D-1 — none. D-2 — covered by must-fix #6 (#0172, filed by this judge — see Finding #5 ruling).

#### 5. Out-of-scope follow-ups (Concern G) — `#0165` failure #1 is filed and tracked; `TestProcess.cs` extraction and "dydo audit replay" surface gap are NOT filed as standalone issues

- **Classification:** release-readiness (issue tracker hygiene).
- **Severity:** low (no functional risk; tracking-list hygiene).
- **Type:** obvious.
- **Evidence:**
  - **#0165 (AgentRegistryTests.IncrementResumeAttempts File.Move/AV race):** `dydo/project/issues/0165-…md:6` — `status: open`. Issue file present, body documents three failures with the `IncrementResumeAttempts` failure as #1 ("may be a real concurrency race"). #0167's body cross-refs #0165 as "the most visible face of the same misconfiguration" but explicitly does not claim to fix #0165's failure #1. **Tracked. ✓**
  - **TestProcess.cs extraction (Charlie/Frank fast-follow):** `grep -rln 'TestProcess' dydo/project/issues/` returns only `#0168` (which mentions it as an *optional* extension to the fix path, not a standalone issue). Charlie's review brief at `dydo/agents/Charlie/dispatch-brief-pr4-review.md:17` and the dispatch reply at `dydo/agents/Adele/inbox/8e147268-…md:31` both DEFER it informally. There is **no** issue file like `0171-extract-testprocess-helper-…md`. **Not formally tracked.**
  - **`dydo audit replay` surface gap (referenced by PR4 plan; subcommand does not exist):** `grep -rln 'audit replay' dydo/project/issues/` returns nothing. The gap is referenced in `dydo/project/changelog/2026/2026-05-06/implement-pr4-production-drain.md:51,118` and `dydo/agents/Charlie/dispatch-brief-pr4-review.md:52` and `dydo/agents/Adele/inbox/8e147268-…md:49`, but never as a filed issue. **Not formally tracked.**
- **Why this matters:** the brief explicitly asked to "Confirm tracked." The brief's framing — "follow-ups flagged by Charlie/Frank" — implies these are expected to be filed somewhere. They aren't. After the wave settles and the agents who flagged them release, the only memory of these follow-ups will be in archived inbox messages and the changelog file — both forms that are easy to lose track of.
- **Proposed fix path:** before the tag, file two thin issues:
  1. **`Extract TestProcess.cs helper for the three test-side git invocations`** — area: backend / severity: low / found-by: review. Body cites the three sites (`SnapshotServiceTests.RunGit`, `InquisitionTests.InitGitRepo`, `WorktreeMergeSafetyIntegrationTests.Git`) and the deferral rationale from Charlie's PR4 review brief.
  2. **`dydo audit replay subcommand is referenced by plans/inquisitions but not exposed in the CLI`** — area: general / severity: low / found-by: review. Body cites the `Commands/` grep that returns nothing and the PR4 plan that referenced it. Either implement it or rewrite the plan/inquisition language to use a real verification command.
- **Files examined:** `dydo/project/issues/0165-…md` (full header + first ~30 lines), `dydo/project/issues/0168-…md:50-70` (TestProcess mention), `dydo/agents/Charlie/dispatch-brief-pr4-review.md:14-20, 49-55`, `dydo/agents/Adele/inbox/8e147268-…md:25-55`, `dydo/project/changelog/2026/2026-05-06/implement-pr4-production-drain.md:45-55, 110-130`, full text grep for `'audit replay'` and `'TestProcess'` across `dydo/project/issues/`.

- **Judge ruling:** CONFIRMED
- **Files examined:** `dydo/project/issues/0165-…md` frontmatter (lines 1-15); grep `audit.replay` and `TestProcess` over `dydo/project/issues/`; `ls dydo/project/issues/ | grep -E '^(0171|0172|0173)-'` (zero matches before this ruling).
- **Independent verification:** Reproduced all three of Brian's claims. `audit replay` returns zero issue files. `TestProcess` returns only `#0168` (line 56, "Optionally extract a single TestProcess.RunGit helper"). #0165 is filed at `status: open`. No #0171+ existed at the start of this ruling.
- **Alternative explanations considered:** TestProcess and audit-replay could be intentionally informal (changelog-only) — but every other follow-up from this session (#0148-#0170) has a tracked issue file, so the convention is clearly to file. After the agents who flagged these informally release, the only memory is in archived inbox messages and changelog files — both forms easy to lose track of.
- **Issue:** Filed two new issues per Brian's must-fix #6: **#0171** (extract TestProcess.cs helper) and **#0172** (audit-replay subcommand surface gap). Brian's must-fix #6 is now satisfied.

#### 6. Release-readiness — `dydo` on PATH is pre-PR1; full BC verification reachable only after the next release publishes (Concern F)

- **Classification:** release-readiness checklist item.
- **Severity:** low (informational; the gap is the natural one between "code lands on master" and "binary ships").
- **Type:** obvious + walked-through.
- **Evidence:**
  - `dydo --version` → `1.4.5+1259d1563e2037d44e9481ac07083613ff58d65e`. Commit `1259d156` predates PR1 of the dydo-check-drift batch (`fc83e31` is PR1) — i.e. the dydo on PATH does **not** carry any of the changes audited here.
  - Freshly-built `bin/Debug/net10.0/dydo.exe --version` → `1.4.6+6a8148556a1dfeacaf2af52316d50c0c8ccd10a3`. This is the binary that includes the wave.
  - `dydo check` (PATH binary) at HEAD: 56 errors / 26 warnings on the dydo project's own docs.
  - `bin/Debug/net10.0/dydo.exe check` (fresh binary) at HEAD: 38 errors / 25 warnings. The 18-error delta is the exact set of "Invalid type value 'inquisition'" errors that PR1 fixed (12 inquisition files × 1 error each, plus a few collateral) — proving PR1's runtime payload is live in the fresh binary but not yet in PATH.
- **Why this is a release-readiness item:** any user upgrade flow that exercises the new BC migration (scanExclude invariants, types.json template-update, hub regen with D4 Tasks prose) requires the *next-release* binary on PATH. A user installing v1.4.6 and running `dydo template update` will get the new `EnsureScanExcludeWithReport` + `EnsureTypesJson` flow; v1.4.5 doesn't have those code paths. End-to-end migration verification on a real LC-shaped install therefore happens **post-tag**, not pre-tag.
- **Proposed action — release-readiness checklist (post-tag):**
  1. After publishing v1.4.6 npm + standalone binaries, run `dydo template update` on a snapshot of an existing pre-PR1 dydo project (e.g. LC) and verify: scan-exclude entries get added to `dydo.json`; `_system/types.json` gets created if missing or merged if present; subsequent `dydo check` passes the config validator.
  2. Run `dydo check` from inside an active worktree on the same project — verify no false positives from the worktree's own files.
  3. Run `dydo fix` on a project with a stale auto-generated `tasks/_index.md` — verify deletion.
  4. The runtime-regression batch (#0167-#0170) does not surface user-visible behavior; verification is the test suite passing on the next release's CI.

- **Judge ruling:** CONFIRMED
- **Files examined:** Ran `dydo --version` (PATH binary) and `bin/Debug/net10.0/dydo.exe --version` (fresh build at HEAD) on this worktree.
- **Independent verification:** PATH dydo reports `1.4.5+1259d1563e2037d44e9481ac07083613ff58d65e` — commit `1259d156` predates `fc83e31` (PR1 of dydo-check-drift). Fresh build reports `1.4.6+6a8148556a1dfeacaf2af52316d50c0c8ccd10a3` — matches HEAD (commit `6a81485`). The new BC migration code paths (EnsureScanExclude with report, EnsureTypesJson template-update, hub regen with D4 Tasks prose) ship only in v1.4.6.
- **Alternative explanations considered:** None — this is mechanical version reporting. The post-tag verification list is sound.
- **Issue:** None — informational checklist item for post-tag verification.

#### 7. Audit trail spot-check (Concern I) — well-formed sessions, but the brief's claimed "duplicate cleanup" did NOT actually delete the file

- **Classification:** docs-discrepancy (brief vs. on-disk state) — minor.
- **Severity:** low.
- **Type:** obvious.
- **Evidence:**
  - **Audit subsystem itself is healthy.** Spot-checked session `24fe8f97-27ca-4a38-aa5f-d33587af3dcb` (2026-05-06 11:22 UTC, git_head c85947a — Frank's PR3 backfill commit). Events show: 8 sequential Reads of `#0151-#0158`, 8 sequential Edits of the same files, then `git add` + `git log -3` + Release as Frank. The events match commit cbd063f's diff exactly. **Audit fidelity confirmed.**
  - Spot-checked my own session `2980040b-…` (this run): Read `index.md` → Read `workflow.md` → `Claim Brian` → `Blocked` (read inbox before role) → `Role inquisitor pre-tag-audit` → `Blocked` (whoami before wait) — exactly the staged-onboarding sequence the guard enforces (per `understand/architecture.md` "Guard System" §). **Guard ↔ audit ↔ workflow consistency confirmed.**
  - **The brief's claim "Audit duplicate file cleanup: dydo/_system/audit/2026/2026-04-24-941f1399-145d-4804-8787-33e9d724a719.json deleted" is wrong as of HEAD.** The file is still present:
    - `ls -la dydo/_system/audit/2026/2026-04-24-941f1399-…json` returns the file (349 bytes).
    - `git ls-tree -r master --name-only | grep '941f1399'` returns three entries: the 2026-04-24 file, the 2026-04-26 file, and the `.events` sidecar — i.e. the **duplicate is still tracked at HEAD**.
    - `git log --oneline -1 -- 'dydo/_system/audit/2026/2026-04-24-941f1399-145d-4804-8787-33e9d724a719.json'` shows the file's last touch was commit `e81c839` (the original add). No deletion commit exists.
    - The recommended fix from `dydo/agents/Adele/inbox/156e49d2-msg-investigate-audit-duplicate-key.md:26` was: "delete the 2026-04-24 file. It is the older, anonymous, single-Read stub … 2026-04-26 file pairs with the existing .events sidecar." That recommendation was not actioned in this session.
- **Why this matters at the tag boundary:** the brief's "no commits" state-changes section is supposed to be a list of tracked deltas the audit doesn't surface as commits. If "deleted" is in the brief but the file is on disk and tracked at HEAD, the brief drifted from reality. This is purely a brief-to-state mismatch — it doesn't block the tag, but it does mean the duplicate-key annoyance Adele recommended fixing is still live.
- **Proposed action:** before tag, either (a) actually delete `dydo/_system/audit/2026/2026-04-24-941f1399-145d-4804-8787-33e9d724a719.json` with a tiny commit (mechanical) and re-run `dydo audit` to confirm it unblocks, or (b) update the brief / changelog to remove the claim. Probably (a) — the file is genuinely a stub with one Read event and zero diagnostic value, and Adele's earlier triage already chose this path.

- **Judge ruling:** CONFIRMED
- **Files examined:** Ran `git ls-tree -r master --name-only | grep '941f1399'`, `ls -la dydo/_system/audit/2026/2026-04-24-941f1399-…json`, and `git log --oneline -3 -- '<file>'` on this worktree.
- **Independent verification:** All three commands reproduced as Brian described. `git ls-tree -r master` returns three entries containing `941f1399` (the disputed 2026-04-24 file, the 2026-04-26 partner, and the `.events` sidecar), confirming the duplicate is still tracked at master HEAD. The file exists on disk at 349 bytes. `git log` shows only one commit ever touched the file: `e81c839 .` (the original add) — no deletion commit exists in history.
- **Alternative explanations considered:** Could have been deleted in an unmerged worktree branch — but the brief explicitly claims the cleanup landed, and `git ls-tree -r master` proves it didn't. Could be a planned-but-not-executed cleanup the brief preview-described — but the brief was framed as completed work.
- **Issue:** None — covered by must-fix #7 (mechanical delete + commit, OR correct the brief).

#### 8. Soft-pass residuals + integration smoke (Concerns C and H, partial) — `dotnet build` clean, `run_tests.py` 4131/4131 PASS in 4m13s

- **Classification:** clean (test infrastructure side); partial coverage on `gap_check.py` and CLI smokes — see status below.
- **Severity:** N/A.
- **Type:** tested.
- **Evidence:**
  - `dotnet build -nologo -v minimal` at commit `6a814855` (HEAD): 0 warnings, 0 errors, 6.5 s wall.
  - `python DynaDocs.Tests/coverage/run_tests.py` (no coverage): **Passed: 4131, Failed: 0, Total: 4131, Duration: 4 m 13 s** (`bn31py4o9` background task, exit 0). The Charlie-observed `QueueServiceTests.FindStaleActiveEntries_DetectsDeadPid` flake from the test-runtime-regression inquisition did **not** fire on this run — consistent with #0167 (assembly-wide `DisableTestParallelization`) being load-bearing for that race.
  - `dydo check` at HEAD: 38 errors / 25 warnings (fresh binary). All errors are pre-existing doc drift in `project/issues/0151-0170/_index.md` — issue stubs created before PR3 (`c85947a`) added the `--summary` flag still need their summary slot backfilled, and `project/issues/_index.md` is stale (the auto-generated hub doesn't list any issue past `#0150`). The test-suite runtime regression itself is **green** (zero failures, 0 flakes on this run), so the wave's runtime-regression batch is honoring its goals.
  - **Important:** the `dydo check` errors above are NOT new from this wave — they are the residual the wave was *partly* meant to surface and clean up. PR3 fixed the CLI; PR3.cbd063f backfilled `#0151-#0158` summaries; **`#0159-#0170` were NOT backfilled** (10 issue files), and the issues hub regen wasn't triggered. Sub-finding pulled out as #1 above.
- **Status of remaining smokes:**
  - `python DynaDocs.Tests/coverage/gap_check.py --force-run` — **inconclusive on this machine.** Probe ran for ~8-9 minutes; `dotnet test` and `python.exe` both visible in `tasklist` mid-run, then both exited cleanly (no hang reproduced — Emma's 30-50 min hang theory NOT triggered on my hardware). However, the harness output-capture file for the background task remained 0 bytes throughout — Python's `subprocess.run` capture pattern in `gap_check.py` (vs. `run_tests.py`'s stream-through pattern, see `coverage/run_tests.py:160` `sys.exit(rc)`) buffered the entire run and the harness could not surface the result. **The process tree DID complete; the result content was not retrievable through this session's tooling.** Per defensive notes I did NOT re-run — one probe is the limit. The signal that *can* be derived: (a) no hang reproduced, (b) test phase produced normal `testhost.exe` lifecycle and exited cleanly, (c) `run_tests.py` (same dotnet test arguments minus coverage instrumentation) returned 4131/4131 PASS at 4m13s — strong indirect evidence that gap_check would have returned the same green result. **Verdict on Concern E:** could not actively confirm "fixed" because output was not retrievable, but ALSO could not reproduce Emma's hang. The defensive recommendation to "surface as a separate issue if you find the trigger" does NOT apply (no trigger found).
  - `bin/Debug/net10.0/dydo.exe inquisition coverage --since 30`: **green.** Lists 14 inquisitions including the in-progress `pre-tag-audit` (this report). Output is well-formed, `--since` filter works.

- **Judge ruling:** CONFIRMED (clean — build re-verified; tests deferred per brief)
- **Files examined:** Ran `dotnet build -nologo -v minimal` at HEAD (commit `6a81485`).
- **Independent verification:** `Build succeeded. 0 Warning(s) 0 Error(s) Time Elapsed 00:00:01.23` (warm cache). Reproduces Brian's clean-build claim. Did **not** re-run `run_tests.py` — Brian's 4131/0 result is from the same session (~hours ago), the brief explicitly says the re-run is optional, and the test suite is independently exercised by gap_check / future CI. Did **not** re-run `gap_check.py --force-run` — defensive notes cap probes at one and Brian used his.
- **Alternative explanations considered:** A test-side regression could have landed between Brian's run and this ruling, but no commits have landed since `6a81485` (Brian's HEAD) — `git log` shows the same HEAD. Re-run would add ~5 minutes for marginal information.
- **Issue:** None.

### Hypotheses Not Reproduced

- **Emma's `gap_check.py` 30-50 min Windows hang theory (Concern E).** Probed once, did not reproduce on this hardware; process tree completed cleanly within ~9 minutes. Output capture failed at the harness layer, but the failure mode was *Python output buffering*, not *process hang*. Charlie's prior reproduction (test-runtime-regression inquisition postscript: 3 m 25 s wall-clock) and my run_tests.py reproduction (4 m 13 s wall-clock; same dotnet test arguments minus coverage) bracket this in a no-hang regime. Emma's observation may be machine- or environment-specific (AV scanning, disk pressure, parallel agent dispatches) and is not a tag-blocker on the evidence available here. Do not file a new issue from this audit; if Emma reproduces, file standalone with her trace.
- **PR4 redirectStdin = false breaking a production caller.** Walked all four call sites (Concern D-1); each is a *local* git read-only operation with no credential-prompt surface. The default does not break anything currently shipped.

### Counts by Classification

| Classification               | Count | Findings    |
|------------------------------|------:|-------------|
| release-readiness            |     3 | #1, #6, #7  |
| clean (no action)            |     3 | #2, #3, #8  |
| observation (out-of-scope)   |     2 | #4, #5      |

No new regressions, no latent bugs, no coverage gaps surfaced. The wave is technically sound — all eight findings are about hygiene at the closure-bookkeeping layer (issue tracker, hub regen, brief-vs-state truthing, binary upgrade order) and follow-up tracking (TestProcess, audit-replay), not behavioral defects in the audited code.

### TAG-READINESS VERDICT: **GO** (with a small bookkeeping must-fix list)

The technical wave is **safe to tag**. The two batches (`dydo-check-drift` PR1-PR3 and `runtime-regression` PR1-PR4) plus the smaller fixes pass independent verification:

- `dotnet build` clean (0/0 warn/err, 6.5 s).
- `run_tests.py` 4131 PASS / 0 FAIL / 4 m 13 s — Charlie's prosecuted `QueueServiceTests` flake from the test-runtime-regression inquisition did not reproduce, confirming #0167 is doing its job.
- BC migration story walked end-to-end on a hypothetical LC-shaped project — clean, with one minor papercut (`dydo fix` doesn't auto-create `_system/types.json`, but runtime degrades gracefully because of `FrontmatterTypesService`'s baseline fallback).
- Cross-batch interaction probe — both sub-concerns clean (no overlap).
- Plan deviations — both safe as-shipped.
- Audit subsystem — well-formed, live-growing, fidelity confirmed against a known commit (`cbd063f`'s 8 reads + 8 edits + git ops + release).
- `dydo inquisition coverage --since 30` — green.

**Must-fix before tag (all bookkeeping; ~30-60 minutes of mechanical work):**

1. **Flip `status: open` → `status: resolved`** in the 10 issues genuinely closed this session: `#0159, #0160, #0161, #0162, #0163, #0166, #0167, #0168, #0169, #0170`. Leave `#0164` open if the brief's "partially" still applies (skip-pattern centralization — only the helper hoist landed; the per-rule `RuleSkipPaths.IsTemplateOrAddition()` calls remain duplicated). [Finding #1]
2. **Add a `## Resolution` section to `#0167`** describing the *actual* landed shape (assembly-wide `[assembly: CollectionBehavior(DisableTestParallelization = true)]` per `DynaDocs.Tests/AssemblyInfo.cs`, gate-bypass migration in 3 sites, the `RuntimeRegression/ParallelisationDisabledTests` reflection contract pin). The issue body still describes the original per-collection-flag plan, which is **not** what shipped. [Finding #1]
3. **Collapse the duplicated `## Description` heading in `#0167`** at lines 13-15. Mechanical edit. [Finding #1]
4. **Backfill summary paragraphs on `#0159, #0160, #0161, #0162, #0163, #0164, #0165, #0166, #0167, #0168, #0169, #0170`** so each issue has a one-line summary between H1 and `## Description`. The PR3 CLI fix landed AFTER these issues were created; only `#0151-#0158` got summary-backfill via `cbd063f`. [Sub-finding under #1; visible as `dydo check` "Missing summary paragraph after title" warnings]
5. **Run `dydo fix` with the v1.4.6 binary** (after rebuild) so `dydo/project/issues/_index.md` is regenerated to include `#0151-#0170`. Today the hub is stale and `dydo check` reports 12 "Orphan doc" warnings, all auto-healable. [Sub-finding under #1]
6. **File two thin issues for the un-tracked follow-ups:** (a) `Extract TestProcess.cs helper for the three test-side git invocations` (area: backend, severity: low); (b) `dydo audit replay subcommand referenced by plans/inquisitions but not exposed in the CLI` (area: general, severity: low). [Finding #5]
7. **Either delete the audit duplicate file** `dydo/_system/audit/2026/2026-04-24-941f1399-…json` (per Adele's earlier triage in `dydo/agents/Adele/inbox/156e49d2-msg-investigate-audit-duplicate-key.md:26`) **or** correct the brief's "State changes" bullet that claims the cleanup was done. The file is on disk and tracked at HEAD — the brief drifted from reality. [Finding #7]

**Post-tag (not blocking):**

- Run `dydo template update` on a real LC-shaped project once v1.4.6 binary is on PATH — verify the EnsureScanExclude / EnsureTypesJson migration flow end-to-end. [Finding #6]
- (Optional) Flip `ProcessUtils.RunProcessCapture`'s `redirectStdin` default to `true` so future networked-git callers fail-safe. [Finding #4 D-1]
- (Optional) Add `EnsureTypesJson` to `FixCommand` so `dydo fix` heals both invariants symmetrically. [Finding #2]

### Confidence: high (on the technical wave) / high (on the bookkeeping gaps)

- **High** on the technical wave being safe — `run_tests.py` 4131/0 plus a clean build at HEAD plus the BC walkthrough plus the cross-batch probe plus the audit-trail spot-check all land independently.
- **High** on the bookkeeping gaps being real — every gap is reproducible by reading file frontmatter, running `dydo check`, or running `git ls-tree`; no race condition, no probabilistic claim.
- **Inconclusive** only on `gap_check.py --force-run` direct verification: harness output was empty for buffering reasons (not a hang). Mitigation: `run_tests.py` exercises the same test surface and returned green; the indirect evidence is strong.
- **Not examined:** end-to-end behaviour of any feature beyond the wave's surface (e.g., dispatch + watchdog flows, auto-resume, queue advancement) — out of scope for a wave-focused audit.

## 2026-05-06 — Charlie (Judge)

### Verdict: RATIFY GO with one amendment to Brian's must-fix list

All eight findings examined independently. Rulings recorded under each finding above.

**Counts:**

| Ruling          | Count | Findings              |
|-----------------|------:|-----------------------|
| CONFIRMED       |     8 | #1, #2, #3, #4, #5, #6, #7, #8 |
| FALSE POSITIVE  |     0 | —                     |
| INCONCLUSIVE    |     0 | —                     |

Zero false positives. The technical wave is safe to tag and the bookkeeping gaps are real.

### Issues filed

- **#0171** — *Extract TestProcess.cs helper for the three test-side git invocations* (general / low / inquisition). Satisfies must-fix #6a.
- **#0172** — *dydo audit replay subcommand referenced by plans/inquisitions but not exposed in the CLI* (general / low / inquisition). Satisfies must-fix #6b.

### Amendment to Brian's must-fix list

Brian's must-fix #6 ("File two thin issues") is now **done** — see #0171 and #0172 above. The remaining must-fix items (1-5, 7) are unchanged and still required before tag.

### Notable deltas from Brian's report

- **Finding #4 D-1 caller count is five, not four.** Brian missed `Commands/InquisitionCommand.cs:169` (`HasChangesSince` → `git diff --stat HEAD@{since}`). The fifth caller is also a local-only git read with no credential surface, so the substantive risk assessment is unchanged. Worth noting in case the optional `redirectStdin = true` follow-up (post-tag #2) is later actioned — the survey needs to include that site.

### Tag-readiness verdict — **GO**

Ratify Brian's GO. The two-batch wave (`dydo-check-drift` PR1-PR3 + `runtime-regression` PR1-PR4) plus the smaller fixes pass independent verification at every layer Brian probed. The remaining must-fix items are bookkeeping that does not affect runtime behaviour, user upgrade flow, or test signal — they are 30-60 minutes of mechanical work the human can sequence ahead of `git tag` without altering any code under audit.

**Why not WAIT-FIX:** none of the remaining must-fix items (1-5, 7) is load-bearing for v1.4.6 *as a binary*. A user installing v1.4.6 and running `dydo template update` gets the correct migration regardless of whether issue frontmatter says `open` or `resolved`, regardless of whether #0167 has a Resolution section, regardless of whether `dydo/_system/audit/2026/2026-04-24-941f1399-…json` is on disk. The bookkeeping is project hygiene, not release content. **GO** is correct; the must-fix list is a strong recommendation for the same hour as the tag, not a precondition.

**Why not upgrade ("just tag now, do bookkeeping later"):** the must-fix list is small and load-bearing for *future* readers. Specifically, must-fix #2 (Resolution backfill on #0167) and must-fix #4 (summary backfills) would land in a project history that's frozen by the tag — running them post-tag means the v1.4.6 tag points at a snapshot where 11 closed-this-session issues are misindexed. Cheap to fix now, awkward to fix later.

### Confidence: high

- **High** on every individual ruling — every CONFIRMED is reproducible by re-running the cited commands or reading the cited files at the cited lines.
- **High** on the verdict — Brian's evidence converges with my independent verification on every finding; the only delta (caller count for D-1) is a count error, not a substantive disagreement.
- **Not examined:** I did not re-run `run_tests.py` (~5 min) or probe `gap_check.py` (defensive cap of one probe per session, used by Brian). The indirect evidence Brian assembled — clean build at HEAD, no commits since his run, runtime-regression batch test surface independently exercised by 8b71cd4's new RuleSkipPaths tests passing — is sufficient.

### Worktree

This judgment is being recorded inside the worktree `pre-tag-audit`. The worktree contains `dydo/project/issues/0171-…md` and `dydo/project/issues/0172-…md` (newly filed by this judge) plus the per-finding ruling edits to `dydo/project/inquisitions/pre-tag-audit.md`. **Suggest merging the worktree before cleanup** so the two new issue files and the ruling annotations land on master before the tag.


