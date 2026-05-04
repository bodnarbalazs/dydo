---
area: project
type: inquisition
---

# Inquisition: dydo check / dydo fix drift

Audit of `dydo check` rule drift on the dydo project itself and on the cross-project probe at `Desktop\LC`. The brief (`dydo/agents/Adele/brief-dydo-check-drift.md`) supplies the four buckets reproduced from the user's local run. This report classifies each finding as **hard-code-gap**, **soft-prompting-gap**, or **legitimate**, evidences each one against rule code + templates + CLI, and proposes a fix path. Investigation only — no code changes.

## 2026-05-04 — Charlie

### Scope

- **Entry point:** Area investigation — `dydo check` output drift across two projects.
- **Files investigated (rules):**
  - `Rules/FrontmatterRule.cs`
  - `Rules/SummaryRule.cs`
  - `Rules/OrphanDocsRule.cs`
  - `Rules/RelativeLinksRule.cs`
  - `Rules/BrokenLinksRule.cs`
  - `Rules/NamingRule.cs`
  - `Rules/RuleBase.cs`
- **Files investigated (other):**
  - `Models/Frontmatter.cs` (allowed-types list)
  - `Services/DocScanner.cs` (scan recursion)
  - `Services/MarkdownParser.cs` (`ExtractSummaryParagraph` semantics)
  - `Services/HubGenerator.cs` + `Commands/FixHubHandler.cs` (auto-index regeneration)
  - `Commands/IssueCreateHandler.cs` (`BuildBodySection`)
  - `Templates/mode-inquisitor.template.md` (filing prompt)
  - `Templates/template-additions-readme.md` (fragment semantics)
- **Live reproductions:**
  - `dydo check` in this project: 15 errors, 20 warnings (brief reported 9 warnings — 11 new task-orphans accreted between brief and now; doesn't change classification).
  - `dydo check` at `Desktop\LC`: 38 errors, 6 warnings.
- **Scouts dispatched:** 0. All evidence is grep-able from rule source + a single CLI reproduction; parallel scouts would not add signal here. Judge dispatched at the end.

### Findings

#### 1. `Frontmatter.ValidTypes` allowed-types list missing `inquisition`

- **Bucket:** A (brief).
- **Classification:** hard-code-gap.
- **Severity:** medium.
- **Type:** obvious.
- **Evidence:**
  - `Models/Frontmatter.cs:12` — `ValidTypes = ["hub", "concept", "guide", "reference", "decision", "pitfall", "changelog", "context", "folder-meta", "issue"]`. No `inquisition`.
  - `Rules/FrontmatterRule.cs:73-76` — emits the error verbatim against `ValidTypes`.
  - `dydo/understand/architecture.md` has a first-class **Inquisition Coverage** section; `dydo/_system/roles/` defines an `inquisitor.role.json`; `dydo/project/inquisitions/` is a real, populated tree; the inquisitor mode template tells agents to write reports there with no contradicting frontmatter guidance.
  - All 11 inquisition files use `area: project, type: inquisition` (`head -5 dydo/project/inquisitions/*.md`).
  - Prior context: `dydo/project/tasks/cleanup-docs-check-backlog.md:23` records Brian acknowledging this exact gap on 2026-05-01 and deferring it to "a parallel dydo-tool fix batch" that hasn't shipped.
- **Proposed fix path:** Append `"inquisition"` to `Frontmatter.ValidTypes` in `Models/Frontmatter.cs:12`. Trivial — single-line edit. No template or doc change needed (existing inquisitions already use the type).
- **Open questions:** Should `Frontmatter.ValidTypes` ever stop being a hardcoded array and instead be sourced from a project-overridable JSON, similar to `dydo/_system/roles/`? Out of scope for this fix — flagging for the planner.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Models/Frontmatter.cs:11-12`, `Rules/FrontmatterRule.cs:69-76`, all 12 files under `dydo/project/inquisitions/` (verified via `grep -l '^type:\s*inquisition'`).
- **Independent verification:** Re-ran `dydo check` from inside the worktree — output contains 12 "Invalid type value 'inquisition'" errors, exactly one per inquisition file (including the brand-new `dydo-check-drift.md`). Confirmed `inquisitor.role.json` exists as a first-class role and the inquisitor template instructs agents to write reports under this directory with no contradicting frontmatter guidance, so there is no escape hatch where this type would be intentionally rejected.
- **Alternative explanations considered:** Could `inquisition` be deliberately omitted (e.g., reports treated as ad-hoc, non-canonical docs)? No — the architecture has a first-class **Inquisition Coverage** concept and `cleanup-docs-check-backlog.md:22-23` records Brian explicitly deferring *this same gap* to "a parallel dydo-tool fix batch" on 2026-05-01. Known-and-deferred miss, not an intentional exclusion.

#### 2. `SummaryRule` does not skip `_system/template-additions/`

- **Bucket:** B (brief).
- **Classification:** hard-code-gap.
- **Severity:** medium.
- **Type:** obvious.
- **Evidence:**
  - `Rules/SummaryRule.cs:10-22` — full body of the rule. No path filter. If `doc.Title` is empty, returns "Missing title (# heading)" as an **error**.
  - Template additions are *fragments* spliced into other docs via `{{include:name}}` (`Templates/template-additions-readme.md:1-12`). Adding an H1 would corrupt the host doc's heading hierarchy.
  - Sibling rules `FrontmatterRule:22-26`, `BrokenLinksRule:24-26`, and `NamingRule:17-19` all skip `_system/templates/` and `_system/template-additions/` with the **identical 4-line block**. `SummaryRule` is the outlier.
  - LC reproduces this on 5 files (one extra: `extra-test-guidance.md`). Same root cause.
  - Prior context: `dydo/project/tasks/cleanup-docs-check-backlog.md:22` flags exactly this — "`SummaryRule.cs` is missing the `_system/template-additions/` exclusion the other rules already have."
- **Proposed fix path:** Add the same `if (normalized.StartsWith("_system/templates/", …) || normalized.StartsWith("_system/template-additions/", …)) yield break;` block at the top of `SummaryRule.Validate`. See finding #6 for a deeper refactor that would prevent this class of bug.
- **Open questions:** None — direct port of an existing pattern.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Rules/SummaryRule.cs` (full, 23 lines), `Rules/FrontmatterRule.cs:21-26`, `Rules/BrokenLinksRule.cs:21-26`, `Rules/NamingRule.cs:14-19`, `Templates/template-additions-readme.md`, live `dydo check` output.
- **Independent verification:** Read the full body of `SummaryRule.cs` — only 23 lines, zero path filter, errors on missing `# title` unconditionally. Diffed against the three sibling rules: each carries an identical 4-line `if (normalized.StartsWith("_system/templates/", …) || normalized.StartsWith("_system/template-additions/", …)) yield break;` block. Live `dydo check` reproduces 4 errors on `_system/template-additions/extra-*.md` files. LC reproduces 5 (one extra: `extra-test-guidance.md`).
- **Alternative explanations considered:** Could template-addition files be expected to have H1s? No — `template-additions-readme.md` explicitly describes them as "fragments" injected via `{{include:name}}`; an H1 would corrupt the host doc's heading hierarchy. The three sibling rules' identical exclusion block is the smoking gun for "expected pattern, missed by SummaryRule."

#### 3. Issues created by `dydo issue create` cannot satisfy `SummaryRule`

- **Bucket:** C (brief). Brief framed it as "mixed hard/soft"; verdict below leans hard.
- **Classification:** hard-code-gap (primary) + soft-prompting-gap (secondary).
- **Severity:** medium.
- **Type:** obvious.
- **Evidence:**
  - `Services/MarkdownParser.cs:52-80` — `ExtractSummaryParagraph` looks for non-empty text **between** the H1 and the first H2/H1/`---`. If the next non-blank line is `## Description`, the summary is null.
  - `Commands/IssueCreateHandler.cs:159-188` — `BuildBodySection` always emits `## Description` immediately under the H1 with the body **inside** Description. There is **no** code path that puts content above `## Description`. Therefore every issue produced by `dydo issue create` triggers the `Missing summary paragraph after title` warning.
  - 8 currently-flagged issues (#0151-#0158) confirm the pattern. Spot-check `dydo/project/issues/0151-…md:11-15`: `# <title>` → blank → `## Description` → content. No summary line.
  - Older issues (#0028-#0148) **do** have a summary above `## Description` because Brian backfilled them by hand in commit 756bedb (`dydo/project/tasks/cleanup-docs-check-backlog.md:18`). That backfill is unsustainable as a permanent strategy.
  - Inquisitor template (`Templates/mode-inquisitor.template.md:280`) instructs filing as: `dydo issue create --title '...' --area <a> --severity <s> --found-by inquisition`. No `--summary` (the flag doesn't exist) and no `--body` either, so most inquisition-filed issues land with the `(Describe the issue)` placeholder Description and no summary at all. This is exactly what produces the dead-on-arrival summary warning, every time.
- **Proposed fix path:** Two coordinated edits in one PR.
  1. **Hard:** Extend `IssueCreateHandler` with an optional `--summary "<one-line>"` flag (or, preferably, a positional first paragraph derived from the body). Render output as `# {title}\n\n{summary}\n\n## Description\n\n{body}`. When summary is omitted at create-time, pre-fill `(One-line summary)` placeholder so the generated file is at least structurally compliant; agents fix the placeholder afterward.
  2. **Soft:** Update `Templates/mode-inquisitor.template.md` (and the equivalent passages in code-writer/reviewer mode templates that file issues) to teach the new `--summary` flag and a 1-sentence summary discipline. After regeneration, `dydo template update` will propagate to active agents.
- **Open questions:**
  - Alternative path: relax `SummaryRule` to accept "first paragraph after `## Description` or `## Reproduction`" as the summary for `type: issue` files. Lower-effort, but loses the hub preview value (`HubGenerator:182-198` consumes `SummaryParagraph` to render `_index.md` link descriptions; many issue stubs would render with no preview). Surface to the planner.
  - Should there also be a backfill pass that promotes the first sentence of `## Description` into a summary line for issues #0151-#0158? Probably yes; pair with the CLI fix.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/IssueCreateHandler.cs:159-188` (full `BuildBodySection`), `Commands/IssueCreateHandler.cs:88-102` (file template), `Services/MarkdownParser.cs:52-80` (`ExtractSummaryParagraph`), `Templates/mode-inquisitor.template.md:270-285`, `dydo/project/issues/0151-…md:1-15`, live `dydo check` output.
- **Independent verification:** Traced every code path of `BuildBodySection`: (a) `bodyContent == null` → `## Description\n\n(Describe the issue)\n\n…`; (b) structural-heading body → `## Description\n\n{bodyContent}`; (c) plain body → `## Description\n\n{bodyContent}\n\n…`. **All three paths emit `## Description` immediately after the H1, with zero content above it.** The file template at `:88-102` then wraps as `# {title}\n\n{bodySection}`. `ExtractSummaryParagraph` walks past `# title`, skips blank lines, then breaks at the first `#`-prefixed line — so a `## Description` next-non-blank line yields null summary by construction. Live: 8 issues (#0151-#0158, all `found-by: inquisition`) trigger "Missing summary paragraph" — matches exactly. Spot-checked #0151: `# title` → blank → `## Description` → content. Inquisitor template line 280 documents `dydo issue create --title '...' --area <a> --severity <s> --found-by inquisition`; no `--summary` (flag doesn't exist), no `--body`, so the placeholder path is the default for inquisition-filed issues.
- **Alternative explanations considered:** Could `SummaryRule` be intentionally lax for `type: issue`? No — the rule is type-agnostic, the warning is real, and `HubGenerator:182-198` consumes `SummaryParagraph` to render `_index.md` previews (missing summaries degrade hub usefulness). Brian's hand-backfill of #0028-#0148 (per `cleanup-docs-check-backlog.md:18`) confirms summaries are expected for issues — backfill, not silence, was the chosen workaround.

#### 4. `OrphanDocsRule` does not skip transient task files

- **Bucket:** D (brief).
- **Classification:** hard-code-gap (with a partial workaround via `dydo fix`).
- **Severity:** low.
- **Type:** obvious.
- **Evidence:**
  - `Rules/OrphanDocsRule.cs:13` — `MainDocFolders = ["guides", "project", "reference", "understand"]`. Task files live under `project/tasks/`, so the rule scopes them in.
  - `Rules/OrphanDocsRule.cs:38-43` — BFS-reachability from `project/_index.md`. A task only counts as reachable when `tasks/_index.md` lists it.
  - `tasks/_index.md` is auto-regenerated by `dydo fix` (`Commands/FixHubHandler.RegenerateHubs` → `Services/HubGenerator.GenerateHub`). The `<!-- Auto-generated by 'dydo fix'. Do not edit -->` banner makes this explicit.
  - **Therefore:** the orphan warnings are the visible footprint of `dydo fix` not having run since the last task was created. Right now, 12 of 35 task files are orphans, including `dydo-check-drift.md` (created when I set my role at 2026-05-04T17:44Z).
  - LC reproduces this on 2 task files plus 1 orphan resolved-issue file (a different cause — see finding #7).
  - `FrontmatterRule:36-50` already gives task files special treatment because their frontmatter schema differs (name/status/created/assigned). The orphan rule is the only one that still treats task files as first-class permanent docs.
- **Proposed fix path:** Two acceptable shapes; prefer (a).
  - **(a) Skip task files in the orphan rule.** Add `if (normalized.StartsWith("project/tasks/", StringComparison.OrdinalIgnoreCase) && !doc.FileName.StartsWith("_")) return [];` at the top of `OrphanDocsRule.Validate`. Mirrors `FrontmatterRule:36-50` exactly. Side-effect: task files no longer need to appear in `tasks/_index.md` at all — `dydo fix` could either keep listing them (status quo) or stop listing them (cleaner; the index file becomes meaningful only for the description).
  - **(b) Trigger hub regen on task-file create/touch.** Higher coupling, more moving parts; not recommended.
- **Open questions:** Same nudge as finding #1 — should the four-element `MainDocFolders` array be configurable? Same answer: out of scope, flag for planner.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Rules/OrphanDocsRule.cs:13` (`MainDocFolders`), `:18-46` (Validate), `:106-140` (BFS), `Commands/FixHubHandler.cs:14-64` (RegenerateHubs), `Services/HubGenerator.cs:60-118` (GenerateAllHubs), `Rules/FrontmatterRule.cs:36-50` (existing task-special-case precedent), live `dydo check`.
- **Independent verification:** Read the rule body: `project` is in `MainDocFolders` and the BFS starts from `project/_index.md`, so task files are scoped in. Read `FixHubHandler.RegenerateHubs` — it walks `project/tasks/` and rewrites `_index.md` via `HubGenerator.GenerateHub`, which lists every non-meta file in the folder. Live check produces 13 task-orphan warnings, including `dydo-check-drift-ruling.md` which I created myself when I set my role at 17:55 UTC — exactly the "every role-set adds noise" pattern Charlie described. `FrontmatterRule:36-50` already gives task files special treatment, so the proposed fix is a direct port of an existing pattern.
- **Alternative explanations considered:** Could the warnings be the user's intended signal that they need to run `dydo fix`? Defensible read, but the warnings have no path-to-zero without a separate command and accrete on every role-set. The task file `dydo-check-drift-ruling.md` will keep nagging until `dydo fix` runs, and then go quiet — that's UX noise, not signal. Severity correctly graded as low (rough edge, not a correctness gap). Fix path (a) preferred and consistent with `FrontmatterRule`'s existing precedent.

#### 5. NEW (LC-only): `DocScanner` recurses into `_system/.local/worktrees/`

- **Bucket:** E (cross-project, not in brief).
- **Classification:** hard-code-gap.
- **Severity:** **high** in any project that has live worktrees (LC currently produces ~15 spurious errors from one worktree alone; scales linearly).
- **Type:** obvious.
- **Evidence:**
  - `Services/DocScanner.cs:14-25` — `Directory.GetFiles(path, "*.md", SearchOption.AllDirectories)` with no exclusion list. Walks every subdirectory under the dydo root.
  - `dydo/_system/.local/worktrees/<id>/` is the live worktree storage (per `understand/architecture.md` "Worktree Dispatch" section, step 2). Each entry is a full project clone — `*.md` files everywhere including `src/.../README.md`, `tests/README.md`, etc., none of which are real "docs" of the host project.
  - LC's `dydo check` confirms — files like `_system/.local/worktrees/frontend-slice-05-scene-editor-structure/src/microservices/asset_processing/README.md` get flagged for naming (`asset_processing` not kebab-case), missing frontmatter, and broken links (because the worktree is on a different branch with different relative paths).
  - The dydo project itself doesn't expose this finding because the run is happening **inside** the worktree (`Checking …\worktrees\dydo-check-drift\dydo…`). From inside, `_system/.local/worktrees/` is empty. From the main project root, it isn't.
  - `Services/HubGenerator.IsExcludedPath:279-289` already excludes `_system/`, `agents/`, and dotfile dirs **for hub generation**. The same exclusion logic is missing from the scan stage feeding the rule pipeline.
  - Compare: `Commands/FixHubHandler.IsExcludedFolder:134-146` excludes `_system`, `agents`, `_assets`. Three independent exclusion lists, all slightly different, none applied to the scan.
- **Proposed fix path:** Add a single, central exclusion check at the scan boundary. Cleanest implementation:
  - In `Services/DocScanner.ScanDirectory`, after `GetFiles`, filter out paths whose normalized relative path starts with `_system/.local/`. Optional but recommended: also filter `_system/audit/` (only contains JSON today, but defensively).
  - Verify: `_system/templates/` and `_system/template-additions/` should **stay scanned** so per-rule logical skips can keep firing (they contain real source-of-truth template content that other parts of the system surface).
- **Open questions:**
  - Is there value in a `dydo.json` configurable `scan-exclude` list, so projects can add their own `node_modules`, `target`, etc.? Out of scope; flag for planner.
  - Should the scan also stop at `.git` and other VCS dirs as a sanity guard? Today this isn't a problem because `.git` is hidden and `Directory.GetFiles` doesn't follow it on Windows by default, but worth confirming on Unix.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Services/DocScanner.cs:14-25`, `Services/HubGenerator.cs:279-289` (`IsExcludedPath`), `Commands/FixHubHandler.cs:134-146` (`IsExcludedFolder`), live `dydo check` at `C:\Users\User\Desktop\LC`.
- **Independent verification:** Read `ScanDirectory` — `Directory.GetFiles(path, "*.md", SearchOption.AllDirectories)` with no exclusion or filter. Compared against the two hub-generator exclusion lists; both already exclude `_system/`. Reproduced live at LC: `_system/.local/worktrees/frontend-slice-05-scene-editor-structure/src/microservices/asset_processing/README.md` and two siblings flagged for naming, missing frontmatter, and broken links — exactly Charlie's prediction. From inside the dydo-check-drift worktree, `_system/.local/worktrees/` is empty, so the dydo project's local check doesn't surface this; the gap only appears when the run is at the main project root with live worktrees, which matches Charlie's mechanism.
- **Alternative explanations considered:** Could scanning into `_system/.local/` be intentional (e.g., to surface broken docs in stale worktrees)? No — those files belong to an unrelated branch with different source paths; broken-link errors there are noise about another commit's filesystem state, not the host project's. And both hub-generation paths already exclude `_system/` — the scan-side omission is a clear inconsistency, not a design choice. Severity high is accurate: ~12 spurious errors from one worktree alone, scaling linearly with worktree count.

#### 6. Skip-pattern logic is duplicated across rules and silently inconsistent

- **Bucket:** Meta-finding (synthesizes #2 and #5).
- **Classification:** hard-code-gap (architectural).
- **Severity:** low (no live drift caused by it once #2 and #5 are fixed; preventing the next instance is the value).
- **Type:** obvious.
- **Evidence:**
  - `FrontmatterRule:22-26`, `BrokenLinksRule:24-26`, `NamingRule:17-19` all carry the **identical** 4-line `_system/templates/` + `_system/template-additions/` skip block.
  - `SummaryRule` lacks it (finding #2).
  - `RelativeLinksRule` has no path skips at all.
  - No rule skips `_system/.local/` (finding #5).
  - `HubGenerator.IsExcludedPath` and `FixHubHandler.IsExcludedFolder` are two **separate** exclusion lists with overlapping but distinct semantics.
- **Proposed fix path:** Hoist a small `RuleScopeFilter` (or extend `RuleBase` with a `ShouldSkip(DocFile)` virtual + a project-wide default returning true for `_system/.local/`) so:
  - One source of truth for "files that no rule should look at" (`_system/.local/**`, hidden dirs).
  - Per-rule `protected virtual bool SkipForThisRule(DocFile)` for rule-local logic (e.g., template-additions for content rules).
- **Open questions:** Could be done at the orchestration layer (`Services/DocChecker` or whichever assembles rules) instead of inside `RuleBase`, depending on testability preferences. Hand to planner.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Rules/FrontmatterRule.cs:21-26`, `Rules/BrokenLinksRule.cs:21-26`, `Rules/NamingRule.cs:14-19`, `Rules/SummaryRule.cs` (full), `Rules/RelativeLinksRule.cs` (full), `Rules/OrphanDocsRule.cs` (full), `Services/HubGenerator.cs:279-289`, `Commands/FixHubHandler.cs:134-146`, `Services/DocScanner.cs:14-25`.
- **Independent verification:** Diffed all six rules' skip blocks: 3 share an identical 4-line `_system/templates/` + `_system/template-additions/` skip; SummaryRule lacks it (proximate cause of #2); RelativeLinksRule has no path skips; OrphanDocsRule uses a different scoping model (MainDocFolders allowlist). Diffed the two hub exclusion lists: HubGenerator (`_system/`, `agents/`, `/.`) versus FixHubHandler (`_system`, `agents`, `_assets`) — three sources of truth, all slightly different, none applied at the scan boundary (proximate cause of #5).
- **Alternative explanations considered:** Could the duplication be load-bearing — i.e., per-rule semantics that resist hoisting? Partially. RelativeLinksRule arguably should run on template-additions to catch wikilinks; OrphanDocsRule's scoping is fundamentally different from the others. But the `_system/.local/` scan-side exclusion is a clean single-place fix, and the per-rule template-additions block is a clear copy-paste candidate. Architectural shape (RuleScopeFilter / `RuleBase` virtual / orchestration-layer) correctly deferred to the planner — Charlie didn't overspecify, which is right for a meta-finding.

#### 7. NEW (LC-only): `RelativeLinksRule` flags intentional source-code references

- **Bucket:** F (cross-project).
- **Classification:** **legitimate** for dydo's stated convention; **soft-prompting-gap** for projects that document UI/source.
- **Severity:** low.
- **Type:** obvious.
- **Evidence:**
  - `Rules/RelativeLinksRule.cs:33-38` — every internal link without `.md` extension (and not an asset) errors as "Link missing .md extension". Asset list at line 7-8 is media only.
  - LC docs intentionally link to source (`reference/design-system/tokens.md` → `theme.css`; `reference/design-system/voice.md` → `MainHeader.tsx`; `_design-system.md` → a directory). For a project documenting a UI system, these references carry real value.
  - The dydo project itself adopted the rule strictly: prior task notes (`cleanup-docs-check-backlog.md:17`) record that `Templates/*.template.md` and `.cs` references were "dropped to inline code" to satisfy the rule. So the rule is enforced as designed.
- **Proposed fix path:** No code change. Two soft options for the planner to weigh:
  - Document the convention more loudly (in `guides/coding-standards.md` or `guides/how-to-use-docs.md`) so projects know to keep source references as inline `code` rather than links.
  - If projects want to allow source links, add an explicit allow-list extension (`AssetExtensions` analog for code: `.tsx`, `.css`, `.cs`, …) gated by a `dydo.json` setting. Out of scope unless the user signals demand.
- **Open questions:** Does LC consider these warnings or genuinely broken docs? Worth a follow-up to the LC project owner; not a code gap.
- **Judge ruling:** CONFIRMED (legitimate — no dydo code change)
- **Files examined:** `Rules/RelativeLinksRule.cs:7-46` (full), live `dydo check` at LC, `dydo/project/tasks/cleanup-docs-check-backlog.md:17`.
- **Independent verification:** Read the rule end-to-end. `AssetExtensions` covers media only (svg, png, jpg, jpeg, gif, webp, ico, pdf, zip, tar, gz). Source extensions (.tsx, .css, .cs, .ts, .js) are not exempt — every such link errors as "Link missing .md extension". Live LC: `theme.css`, `MainHeader.tsx` references flagged. Reverse-confirmed via `cleanup-docs-check-backlog.md:17`: the dydo project itself "dropped to inline code" `.cs` references rather than linking, treating the rule as the canonical convention.
- **Alternative explanations considered:** Could this be a code gap? No — the rule fires as designed for projects following dydo's "no source links" convention. The "soft-prompting-gap for UI/source projects" framing is correct: it's documentation/convention, not a rule bug. No-code-change call is right; an asset-extension allowlist for source files belongs in a future ask, not this batch.

#### 8. NEW (LC-only): genuinely broken links in committed docs

- **Bucket:** G (cross-project).
- **Classification:** legitimate.
- **Severity:** low (project hygiene).
- **Type:** obvious.
- **Evidence:** LC's `dydo check` flags real broken targets:
  - `project/changelog/2026/2026-04-24/auth-and-email-vertical-slice.md:14` → `../../agents/Brian/plan-auth-email.md` (agent workspace path, ephemeral).
  - `project/changelog/2026/.../token-economy-and-subscriptions-vertical-slice.md:14` → `../../agents/Charlie/plan-token-economy.md`.
  - `project/issues/_index.md:15` → resolved issue at non-resolved path.
  - `project/tasks/_index.md:14-18` → 4 task files that no longer exist.
  - `project/decisions/021-….md:233,237` → README/STORYBOOK files that don't exist on disk.
- **Proposed fix path:** No dydo code change. These are real link rot in LC's committed docs and belong in LC's own cleanup queue.
- **Open questions:** None.
- **Judge ruling:** CONFIRMED (legitimate — no dydo code change)
- **Files examined:** Live `dydo check` output at LC.
- **Independent verification:** Reproduced the exact broken-link list at LC: `agents/Brian/plan-auth-email.md` (ephemeral agent path, never committed), `agents/Charlie/plan-token-economy.md` (same), `decisions/021-…` → `design-system/README.md` and `STORYBOOK.md` (don't exist), `project/issues/_index.md` → resolved-issue at non-resolved path, `project/tasks/_index.md` → 4 missing task files. All real link rot in LC's committed docs.
- **Alternative explanations considered:** None — these are LC's docs pointing at things that don't exist on disk. Nothing for dydo to fix.

#### 9. Minor: `dydo check` agent-assignment block reports stale sessions inside docs-check output

- **Bucket:** H (cross-project, observation).
- **Classification:** legitimate (likely intentional), borderline noise.
- **Severity:** low.
- **Type:** obvious.
- **Evidence:** LC's run ends with: `Checking agent assignments...` → `Agent 'Adele' has stale session (claimed 150 hours ago).` Same for Dexter. The dydo project's run shows `No issues found.` for the same block.
- **Proposed fix path:** No drift; the feature is doing what it says. Flagging only because it adds noise to the docs-check report and may be worth surfacing to a separate `dydo agent` sub-command in a future cleanup. Out of scope here.
- **Judge ruling:** CONFIRMED (legitimate — intentional behavior)
- **Files examined:** Live `dydo check` at LC and on the dydo project itself.
- **Independent verification:** LC: "Agent 'Adele' has stale session (claimed 150 hours ago)" plus same for Dexter. Dydo project: `No issues found.` for the same block. The "Checking agent assignments..." sub-section is clearly demarcated, the message is informational, and the exit code is governed by docs-check errors above — this is a separate post-docs check, not a rule violation.
- **Alternative explanations considered:** Could this be drift in the docs-check pipeline? No — the section header is explicit, the output is informational, and behavior is consistent across runs. The "could move to a separate `dydo agent` sub-command" suggestion is cosmetic and correctly out-of-scope.

### Hypotheses Not Reproduced

- *None tested.* Every finding was provable directly from rule source + a single CLI run + the repository's own filesystem. No race conditions or timing-dependent claims here.

### Counts by Classification

- **hard-code-gap:** 5 (findings 1, 2, 3, 4, 5)
- **hard-code-gap (architectural / meta):** 1 (finding 6)
- **soft-prompting-gap (secondary, paired with #3):** 1
- **legitimate:** 3 (findings 7, 8, 9)

### Fix-Order Recommendation

Three independent PRs is the cleanest split. Order matters for testability — fix the scan first so subsequent rule changes can be verified without spurious worktree noise.

**PR 1 — Scan boundary.** *Highest value, blocks LC from running clean at all.*
- Fix #5: filter `_system/.local/` (and ideally `_system/audit/`) at `DocScanner.ScanDirectory`.
- Fix #6 (preventive scaffold only — actually unifying all skip patterns can be a follow-up): add a single project-wide scan-exclude list in `DocScanner` so future rules don't repeat the inconsistency.
- Verify: `dydo check` at `Desktop\LC` drops by ~15 errors (the worktree-scoped ones) and matches LC's real doc state.

**PR 2 — Rule corrections (independent, can ship together).**
- Fix #1: append `"inquisition"` to `Frontmatter.ValidTypes` (one line in `Models/Frontmatter.cs`).
- Fix #2: add the `_system/templates/` + `_system/template-additions/` skip block at the top of `SummaryRule.Validate` (4 lines, mirrors three sibling rules).
- Fix #4: add `project/tasks/**` skip in `OrphanDocsRule.Validate` (mirrors `FrontmatterRule:36-50`).
- Verify: `dydo check` on the dydo project drops to **0 errors, 0 warnings**.

**PR 3 — Issue summary discipline (CLI + template, paired).**
- Fix #3 hard side: extend `Commands/IssueCreateHandler` with `--summary` (or first-paragraph promotion) and render summary above `## Description`.
- Fix #3 soft side: update `Templates/mode-inquisitor.template.md` (and any other mode template with a `dydo issue create` example) to teach the new flag.
- Optional backfill commit: promote first sentence of `## Description` to a summary line for #0151-#0158 so the project starts clean.
- Verify: a fresh `dydo issue create` produces a file that passes `SummaryRule` without manual editing.

### Proposed Issues To File

Adele — please confirm before I dispatch the judge to file these. Format per brief.

1. Should I file: **`Frontmatter.ValidTypes` allowed-types list missing 'inquisition' — every inquisition report errors on `dydo check`** — area: project / severity: medium?
2. Should I file: **`SummaryRule` lacks the `_system/template-additions/` skip block its three sibling rules already have** — area: project / severity: medium?
3. Should I file: **`dydo issue create` output cannot satisfy `SummaryRule` — every issue stub triggers a 'Missing summary paragraph' warning** — area: project / severity: medium?
4. Should I file: **`OrphanDocsRule` flags transient task files until `dydo fix` runs — adds noise on every check after agent role-set** — area: project / severity: low?
5. Should I file: **`DocScanner` recurses into `_system/.local/worktrees/` — `dydo check` reports tens of spurious errors in projects with live worktrees** — area: project / severity: high?
6. Should I file: **Skip-pattern blocks duplicated across rules with no central source of truth — silent inconsistency vector (already produced #2 and #5)** — area: project / severity: low?

### Confidence: high

- **Thoroughly covered:** The four buckets in the brief (A/B/C/D) — each one's verdict is grounded in rule code, the relevant template/CLI handler, and a live reproduction. The cross-project probe at `Desktop\LC` surfaced two new hard gaps (#5 the worst) plus two legitimate-finding categories that don't move the needle on dydo itself.
- **Surface-level:** The architectural meta-finding (#6) is supported by direct file reads but I didn't propose a complete refactored design — that's a planner's job.
- **Not examined:** `dydo fix` end-to-end behavior on each rule's auto-fixable subset; `dydo check` performance characteristics on large doc trees; any rule beyond the seven I read (`Rules/HubFilesRule.cs`, `Rules/FolderMetaFilesRule.cs`, `Rules/OffLimitsRule.cs`, `Rules/UncustomizedDocsRule.cs` were not driving the observed drift, so I left them).

## 2026-05-04 — Dexter (Judge ruling)

### Verdict

All 9 findings ruled **CONFIRMED**. Zero false positives, zero inconclusive. Each finding's ruling block is inline above with files examined, independent verification, and alternative explanations considered.

| # | Bucket | Classification | Ruling |
|---|---|---|---|
| 1 | A | hard-code-gap | CONFIRMED |
| 2 | B | hard-code-gap | CONFIRMED |
| 3 | C | hard-code-gap (+ soft secondary) | CONFIRMED |
| 4 | D | hard-code-gap | CONFIRMED |
| 5 | E (LC) | hard-code-gap | CONFIRMED |
| 6 | meta | hard-code-gap (architectural) | CONFIRMED |
| 7 | F (LC) | legitimate (no code change) | CONFIRMED |
| 8 | G (LC) | legitimate (LC link rot) | CONFIRMED |
| 9 | H (LC) | legitimate (intentional) | CONFIRMED |

### Independent reproductions performed

- `dydo check` from inside `dydo-check-drift` worktree: **16 errors, 22 warnings**. Drift unchanged from brief (16/22 vs. 15/20) — net +1 error and +2 warnings come from this very inquisition file (1 inquisition + 2 task files: `dydo-check-drift.md` and `dydo-check-drift-ruling.md` orphaning themselves the moment they were created). Drift class composition matches Charlie's claims exactly.
- `dydo check` at `C:\Users\User\Desktop\LC`: **38 errors, 6 warnings**. Reproduces #5 (worktree scan), #2 (5 template-additions; LC has the extra `extra-test-guidance.md`), #7 (theme.css, MainHeader.tsx flagged), #8 (broken-link inventory), and #9 (stale-session block) verbatim.

### Rulings on the 6 proposed issues

All 6 proposals are well-formed and sized correctly. Endorsed for filing as listed:

1. **Endorse.** Frontmatter.ValidTypes / inquisition — area: project / severity: **medium** ✓
2. **Endorse.** SummaryRule template-additions skip — area: project / severity: **medium** ✓
3. **Endorse.** `dydo issue create` summary discipline — area: project / severity: **medium** ✓ (acceptable as medium given backfill is a working workaround; one could argue high since every new issue trips it, but medium fits the "annoying but unblockable" bar)
4. **Endorse.** OrphanDocsRule task-file skip — area: project / severity: **low** ✓
5. **Endorse.** DocScanner / `_system/.local/` exclusion — area: project / severity: **high** ✓ (correct grade — this one materially blocks LC from running clean)
6. **Endorse.** Skip-pattern duplication / central source of truth — area: project / severity: **low** ✓ (correct grade — preventive, not driving live drift once #2 and #5 are fixed)

Adele (or whoever picks this up) — proposals are ready to file as-listed. Per the brief I am **not** filing them; that's deferred to Adele's confirmation.

### Notes for the planner

- Findings #1, #4, #6 each surface the same latent question: should `Frontmatter.ValidTypes`, `MainDocFolders`, and the scan-exclusion list all be data-driven (project-overridable JSON, like roles)? Three independent nudges in the same direction — worth a single planner-level decision.
- Finding #3's soft side touches multiple mode templates (inquisitor, code-writer, reviewer) that document `dydo issue create` invocations. If the CLI gains `--summary`, the template sweep should be one coordinated pass, not piecemeal.
- Fix-order recommendation (PR1 → PR2/PR3) is sound: clearing the worktree-scan noise first makes verification of the rule fixes mechanical instead of manual.
