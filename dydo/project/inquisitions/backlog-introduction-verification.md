---
area: project
type: inquisition
---

# Inquisition: Backlog + Future-Features Slice Verification

Post-implementation QA of commit `3765e57` — the slice that introduced the `backlog/` and
`future-features/` doc categories as `dydo init` defaults (Decision 023). Mandate: confirm
the slice was implemented flawlessly so no new bugs surface later. Adversarial pass over a
slice that already cleared implementation review.

## 2026-05-21 — Charlie

### Scope

- **Entry point:** Feature investigation — the backlog/future-features slice (`3765e57`).
- **Files investigated:** `Services/FolderScaffolder.cs`, `Services/TemplateGenerator.cs`,
  `Services/RoleDefinitionService.cs`, `Services/HubGenerator.cs`, `Commands/WorktreeCommand.cs`,
  `Services/TerminalLauncher.cs`, `Services/WindowsTerminalLauncher.cs`,
  `Rules/{HubFiles,FolderMetaFiles,OrphanDocs,Frontmatter,UncustomizedDocs}Rule.cs`,
  `Templates/_backlog.template.md`, `Templates/_future-features.template.md`,
  `Templates/_project.template.md`, `Templates/about-dynadocs.template.md`, four
  `Templates/mode-*.template.md`, four `dydo/_system/roles/*.role.json`,
  `dydo/_system/types.json`.
- **Docs cross-checked:** Decision 023, `dydo/reference/about-dynadocs.md`,
  `dydo/guides/how-to-use-docs.md`, `dydo/project/_project.md`.
- **Live verification:** built `dydo` from worktree HEAD (the installed CLI is v1.4.7,
  predates the slice — a stale binary will not show the slice's behavior); ran a fresh
  `dydo init claude`, `dydo check`, `dydo fix`, `dydo workspace init`, and re-`init` in a
  temp project.
- **Scouts dispatched:** 1 reviewer (Emma — worktree/junction audit), 1 test-writer
  (Grace — role-guard negative coverage + scaffold coverage).

### Verdict

**The slice is sound. No code defects found.** All six scope areas pass:

| Scope | Result | Evidence |
|---|---|---|
| 1. Scaffold correctness | PASS | Fresh `dydo init` creates `project/backlog/` and `project/future-features/` with meta + auto-generated `_index.md`; `dydo check` → 0 errors. |
| 2. Role permissions | PASS | Exactly co-thinker/code-writer/orchestrator/judge granted `dydo/project/backlog/**`; other roles denied at both layers; `future-features/**` covered by docs-writer's `dydo/project/**`. Matches Decision §7. |
| 3. Worktree junctions | PASS | Independent reviewer audit: the two new paths are processed by path-agnostic loops identically to the existing four; no corruption risk for nested worktrees or merge. |
| 4. Rules / validators | PASS | `dydo check` clean on a fresh scaffold; `types.json` already carried `context`/`folder-meta`/`hub` — no enum change needed; no rule false-flags or false-passes the new folders. |
| 5. Mode-file wording | PASS | All four mode-template one-liners match Decision §8 verbatim; regeneration (`RegenerateAgentFiles` → `GenerateModeFile`) reads the templates, so re-claim picks them up. |
| 6. Regressions | PASS | `dydo fix` idempotent (0 fixes on a fresh scaffold); re-`init` on an initialised project refuses cleanly; hub generation enumerates the new folders correctly. |

The five findings below are all **low-severity test-coverage and documentation polish** — none
block the slice. The mandate was "flawless"; these are the imperfections a passing review
missed.

### Findings

#### 1. `about-dynadocs.template.md` project-tree diagram omits `backlog/`

- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Templates/about-dynadocs.template.md:228-234` draws the `project/` tree —
  it lists `tasks/`, `decisions/`, `changelog/`, `issues/`, `inquisitions/`, `pitfalls/`,
  `future-features/` but **not `backlog/`**. The slice added `backlog/` to
  `_project.template.md`'s Contents list and to `dydo/project/_index.md`, but missed this
  second tree diagram. Every fresh `dydo init` regenerates `reference/about-dynadocs.md`
  (`FolderScaffolder.DocFiles` → `GenerateAboutDynadocsMd`) from this template, so the
  shipped reference doc misrepresents the tree the same `init` just scaffolded.
  `dydo/reference/about-dynadocs.md:226-234` in this repo carries the identical gap.
  Fix target is the template; the repo doc regenerates from it.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Templates/about-dynadocs.template.md` (lines 215-242), `dydo/reference/about-dynadocs.md` (lines 218-242), `dydo/project/decisions/023-backlog-doc-category.md` (§1).
- **Independent verification:** Read the `project/` tree block directly — lines 226-234 list `tasks/`, `decisions/`, `changelog/`, `issues/`, `inquisitions/`, `pitfalls/`, `future-features/`; `backlog/` is absent. The repo's generated `about-dynadocs.md` carries the identical omission at the same lines. Decision 023 §1 mandates `backlog/` as a sibling default of `future-features/`, so the diagram is internally inconsistent with the slice it ships in.
- **Alternative explanations considered:** A deliberate exclusion? No — `future-features/`, its decision-mate, is listed; omitting only `backlog/` is an oversight. Not a non-exhaustive guide either: this is a full tree diagram enumerating every other `project/` subfolder.
- **Issue:** #0209

#### 2. `_backlog.template.md` ships a dangling "Decision 023" reference to every project

- **Category:** doc-discrepancy
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Templates/_backlog.template.md:56` — "See Decision 023 —
  `dydo/project/decisions/023-backlog-doc-category.md` — for the full rationale and
  alternatives considered." This template is copied into every project by `dydo init`.
  A newly-scaffolded project has no decision 023, so its `project/backlog/_backlog.md`
  points readers at a doc that does not exist there. It is written as a code span, not a
  markdown link, so `BrokenLinksRule` does not flag it — which is why a `dydo check`-based
  review misses it. `_future-features.template.md` correctly carries no such reference.
  In the dydo repo itself the reference resolves; the defect is purely downstream.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Templates/_backlog.template.md` (line 56), `Templates/_future-features.template.md` (whole file), `dydo/project/decisions/023-backlog-doc-category.md`.
- **Independent verification:** Line 56 is an inline code span ("See Decision 023 — `dydo/project/decisions/023-backlog-doc-category.md` — ..."), not a markdown link, so `BrokenLinksRule` cannot flag it. `dydo init` copies the template verbatim; a freshly scaffolded project has no `decisions/023`, so the pointer dangles. Confirmed `_future-features.template.md` carries no analogous reference — the defect is unique to the backlog meta template.
- **Alternative explanations considered:** An intended in-repo cross-reference? The path resolves inside the dydo repo, but this is a *shipped template* whose audience is every downstream project — it must not reference repo-private docs. Defect confirmed.
- **Issue:** #0210

#### 3. Test-coverage gap — `FolderScaffolderTests` never asserts the two new folders scaffold

- **Category:** missing-test
- **Severity:** low
- **Type:** tested
- **Evidence:** `FolderScaffolderTests.Scaffold_CreatesExpectedFolderStructure` asserts
  `project/{tasks,decisions,changelog,pitfalls}` and stops; `Scaffold_CreatesHubIndexFiles`
  checks only the four main-folder hubs. Neither was extended for `project/backlog` or
  `project/future-features`. A regression dropping either entry from
  `FolderScaffolder.Folders`/`DocFiles` would not be caught here. (`project/issues` is
  likewise unasserted — a pre-existing pattern, not new.) The slice's
  `TemplateGeneratorTests` additions only prove the meta generators return non-empty
  content, not that they are wired into the scaffold. Test-writer Grace confirmed the
  scaffold behaves correctly and closed the gap with a real `Scaffold()`-into-temp-dir
  test (run: PASS):

  ```csharp
  [Fact]
  public void Scaffold_CreatesBacklogAndFutureFeaturesFolders()
  {
      _scaffolder.Scaffold(_testDir);
      var backlog = Path.Combine(_testDir, "project", "backlog");
      var futureFeatures = Path.Combine(_testDir, "project", "future-features");
      Assert.True(Directory.Exists(backlog));
      Assert.True(Directory.Exists(futureFeatures));
      Assert.True(File.Exists(Path.Combine(backlog, "_backlog.md")));
      Assert.True(File.Exists(Path.Combine(futureFeatures, "_future-features.md")));
      Assert.True(File.Exists(Path.Combine(backlog, "_index.md")));
      Assert.True(File.Exists(Path.Combine(futureFeatures, "_index.md")));
  }
  ```
- **Judge ruling:** CONFIRMED
- **Files examined:** `DynaDocs.Tests/Services/FolderScaffolderTests.cs` (full file), `git diff HEAD` of the same.
- **Independent verification:** Read `Scaffold_CreatesExpectedFolderStructure` (lines 24-37) and `Scaffold_CreatesHubIndexFiles` (lines 78-86) — neither asserts `project/backlog` or `project/future-features` (nor `project/issues`). The gap in commit `3765e57` is real. Grace's closing test `Scaffold_CreatesBacklogAndFutureFeaturesFolders` (lines 52-75) is present in the working tree but **uncommitted** (`git diff HEAD` confirms) — it matches the report's inline code with added comments.
- **Alternative explanations considered:** Coverage elsewhere? The slice's `TemplateGeneratorTests` additions only prove the meta generators return non-empty content, not that they are wired into `Scaffold()`. No other test exercises the scaffold's folder set. Genuine gap — closed, not yet landed.
- **Filed:** backlog item `land-backlog-slice-test-coverage.md` (shared with Finding 4). Tracks *landing the already-written tests*, not a reopened gap. Not an issue: nothing is broken, and per Decision 023 §8 a judge files confirmed-but-non-blocking findings to `backlog/`.

#### 4. Test-coverage gap — no negative role-permission test for `backlog/`

- **Category:** missing-test
- **Severity:** low
- **Type:** tested
- **Evidence:** The slice's `RoleBehaviorTests`/`RoleDefinitionServiceTests`/
  `ConfigurablePathsTests` additions assert only the *positive* side — that
  code-writer/co-thinker/orchestrator/judge **can** write `dydo/project/backlog/**`.
  No test asserts that the non-granted roles **cannot**. The one existing negative test
  (`IsPathAllowed_NonPermittedRoles_CannotWriteIssues`) targets `issues/`, not `backlog/`.
  Test-writer Grace confirmed the guard genuinely denies the non-granted roles (at both
  the role-definition layer and `AgentRegistry.IsPathAllowed`) and closed the gap (run:
  4/4 InlineData PASS):

  ```csharp
  [Theory]
  [InlineData("reviewer")]
  [InlineData("inquisitor")]
  [InlineData("planner")]
  [InlineData("test-writer")]
  public void IsPathAllowed_NonGrantedRoles_CannotWriteBacklog(string role)
  {
      var perms = BuildPerms(["src/**"], ["tests/**"]);
      var (writable, readOnly) = perms[role];
      Assert.DoesNotContain("dydo/project/backlog/**", writable);

      SetupConfig(["Adele"], new() { ["testuser"] = ["Adele"] },
          sourcePaths: ["src/**"], testPaths: ["tests/**"]);
      CreateSessionFile("Adele", $"test-backlog-{role}", role: role, task: "t1",
          writablePaths: writable.Select(p => p.Replace("{self}", "Adele")).ToList(),
          readOnlyPaths: readOnly.Select(p => p.Replace("{self}", "Adele")).ToList());

      var registry = new AgentRegistry(_testDir);
      var result = registry.IsPathAllowed($"test-backlog-{role}",
          "dydo/project/backlog/idea.md", "edit", out var error);
      Assert.False(result);
      Assert.NotEmpty(error);
  }
  ```

  Note: both new tests (this and #3) were written by the sub-dispatched test-writer in
  this inquisition's worktree and are **uncommitted** — they live only on branch
  `worktree/backlog-introduction-verification`. If that worktree is torn down without
  merging, the tests are lost; the code is preserved here so it can be re-landed.
  Full suite at the time: 4267 passed / 0 failed; gap_check 140/140.
- **Judge ruling:** CONFIRMED
- **Files examined:** `DynaDocs.Tests/Services/RoleBehaviorTests.cs` (lines 527-558 plus positive backlog assertions at 153/178/236/264/321/364/455), `git diff HEAD` of the same, `dydo/_system/roles/{code-writer,co-thinker,orchestrator,judge}.role.json`.
- **Independent verification:** Grepped all of `DynaDocs.Tests` for "backlog" — before Grace's addition every assertion was positive (the four granted roles *can* write `dydo/project/backlog/**`); the only negative path test, `IsPathAllowed_NonPermittedRoles_CannotWriteIssues`, targets `issues/`. Confirmed the four `.role.json` files grant `dydo/project/backlog/**` to exactly code-writer/co-thinker/orchestrator/judge. Grace's `IsPathAllowed_NonGrantedRoles_CannotWriteBacklog` (lines 530-558, uncommitted) covers reviewer/inquisitor/planner/test-writer.
- **Alternative explanations considered:** Is docs-writer a missing negative case? No — docs-writer holds a broad `dydo/project/**` grant that legitimately includes `backlog/` (Decision 023 §7); excluding it from the negative theory is correct, not an omission.
- **Filed:** backlog item `land-backlog-slice-test-coverage.md` (shared with Finding 3).

#### 5. Junction tests assert generated script text only — never execute the script

- **Category:** missing-test
- **Severity:** low
- **Type:** obvious
- **Evidence:** `TerminalLauncherTests` asserts the two new junction subpaths appear in the
  generated bash/PowerShell setup scripts (5 assertion sites), but no test creates a
  worktree, runs the junction script, and verifies an on-disk junction that resolves to
  main-root state. This gap applies equally to the pre-existing four junction subpaths
  (`agents`, `roles`, `issues`, `inquisitions`), so the slice introduces **no coverage
  regression** — the new paths reach exactly the same coverage level as `issues`/
  `inquisitions`. Recorded as a standing improvement opportunity, not a slice defect.
- **Judge ruling:** CONFIRMED
- **Files examined:** `DynaDocs.Tests/Services/TerminalLauncherTests.cs` (junction tests at lines 2316-2360; `JunctionSubpaths` assertions near 1843-1891).
- **Independent verification:** Read the four `...CreatesSharedStateJunctions` tests (bash/PowerShell × with/without main root). Each calls `WorktreeSetupScript`/`GetWindowsArguments` and runs `Assert.Contains` against the returned script *string*. No test creates a worktree, executes the script, or stats an on-disk junction. The gap is real — and equally pre-existing for the four older subpaths, so the slice adds no regression.
- **Alternative explanations considered:** Does it warrant an issue? No — not broken, not a regression, not a slice defect (the inquisitor's own framing). Identified, scoped, low-priority work → `backlog/`, not `issues/`.
- **Filed:** backlog item `junction-setup-script-e2e-test.md`.

### Hypotheses Not Reproduced

- **Suspected `_project.md` / `_project.template.md` divergence** — initial read of the
  diff suggested the slice added `issues/` to the repo's `_project.md` Contents but not to
  the template. Direct comparison of both files: they are byte-identical. The slice
  actually *fixed* a pre-existing drift in `_project.md`. Not an issue.
- **Junction change corrupting shared worktree state** — the brief's highest-risk concern.
  Reviewer audit (Emma) found the two new entries flow through three path-agnostic loops
  (`GenerateBashJunctionScript`, `GeneratePsJunctionScript`, `TeardownWorktree`) with no
  per-path branch; the hyphen in `future-features` is shell-safe; teardown removes
  junctions before `git worktree remove` (issue #104 hazard guarded). No new risk.
- **Rule false-flag / false-pass on the new folders** — `dydo check` on a fresh scaffold
  returns 0 errors. `FrontmatterRule` validates only `area`/`type` for `type: context`
  items (backlog's `status`/`created`/`origin` fields are unvalidated) — but Decision §4
  explicitly chose convention-only validation, so this is by design, not a gap.

### Notes (not findings)

- **`#0197` is unrelated.** The brief asked whether issue #0197's `gap_check` flag on
  `WorktreeCommand.cs` relates to the junction change. It does not — #0197 concerns
  `DYDO_AGENT` env-var scrubbing on watchdog/launcher `ProcessStartInfo`. The flag is
  incidental file-level overlap.
- **This worktree's `backlog/` and `future-features/` are plain directories, not
  junctions** (the other four shared subpaths are junctions). Cause: this worktree was
  created by the installed v1.4.7 CLI, which predates the slice. Transitional artifact,
  not a slice defect — worktrees created after the binary upgrade will junction them.
  Consequence worth noting: an agent filing a backlog item from a pre-upgrade worktree
  writes to worktree-local state, not the main tree.
- **`how-to-use-docs.md`** does not enumerate the new categories — but it omits `issues/`
  and `inquisitions/` too, so it is a non-exhaustive curated guide, not a tree reference.
  Out of scope for a finding.

### Confidence: high

Scaffold, role permissions, rules, mode wording, and regressions were verified live
against a binary built from the slice (the installed CLI is stale and must not be used to
test this). The junction change — the brief's highest-risk item — was independently
audited at source level by a reviewer; it was not exercised end-to-end on disk (see
Finding 5). Both confirmed test-coverage gaps were reproduced and closed by a test-writer
with the full suite green. The slice is additive and mechanically simple; nothing examined
suggests a latent code defect.

---

## Judge Review — 2026-05-21 — Dexter

All 5 findings reviewed independently against code, docs, templates, role definitions,
and the uncommitted test diff. **All 5 ruled CONFIRMED** — 0 false positives, 0
inconclusive. Each finding is genuine and correctly characterised; no over-claim found.

| # | Finding | Ruling | Filed as |
|---|---|---|---|
| 1 | `about-dynadocs.template.md` tree omits `backlog/` | CONFIRMED | issue #0209 |
| 2 | `_backlog.template.md` dangling Decision 023 reference | CONFIRMED | issue #0210 |
| 3 | `FolderScaffolderTests` misses the two new folders | CONFIRMED | backlog `land-backlog-slice-test-coverage.md` |
| 4 | No negative role-permission test for `backlog/` | CONFIRMED | backlog `land-backlog-slice-test-coverage.md` |
| 5 | Junction tests assert script text, never execute | CONFIRMED | backlog `junction-setup-script-e2e-test.md` |

**Issue vs. backlog routing.** Findings 1-2 are *broken* shipped output (a wrong tree
diagram, a dangling reference reaching every downstream project) → `issues/`. Findings
3-5 are not broken — 3-4 are test code already written and only awaiting a commit, 5 is a
pre-existing non-regression gap → `backlog/`, per Decision 023 §8 ("a judge files
confirmed-but-non-blocking findings to `backlog/` instead of `issues/`"). Findings 3 and 4
share one backlog item: the resolving action is a single commit of both test files.

**Worktree note.** Grace's two tests *and* both backlog items live only on
`worktree/backlog-introduction-verification`. They are durable on disk only if this
worktree is merged, not discarded — merging is recommended.

**Hypotheses Not Reproduced — spot-check.** Confirmed clear: `_project.md` vs
`_project.template.md` are byte-identical (`git diff --no-index`, empty output); the
junction reviewer audit and the `FrontmatterRule` by-design call both hold up. No
re-opened concerns.
