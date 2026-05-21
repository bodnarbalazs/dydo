---
area: project
type: inquisition
---

# Inquisition: `dydo check` link-validator resolver divergence

Issue #0184 reports a 55-error wall of "Broken link" reports against valid cross-folder relative markdown links when running `dydo check` against a subfolder. The reporter hypothesised a divergence between the link resolvers used by `dydo check` and `dydo graph`, with folder-meta `_*.md` files specially affected. This inquisition: surveys the two resolvers, reproduces the failure mode, names the root cause, and maps the surface.

**Headline:** the reporter's resolver-divergence hypothesis is **refuted as a code-algorithm divergence**. The two resolvers produce identical results for relative paths. The real bug is that **`dydo check <subfolder>` uses that subfolder as both the scan scope and the resolver's `basePath`**, so `allDocs` excludes everything outside `<subfolder>` and any cross-folder link points at "nothing the validator can see." `dydo graph` is not subject to this because it *always* scans from the docs root. The folder-meta `_*.md` suspicion is incidental — those files concentrate cross-folder navigation links by purpose, not by code path.

---

## 2026-05-19 — Frank

### Scope

- **Entry point:** Area investigation — `dydo check` link-validator behaviour vs `dydo graph` resolver behaviour.
- **Files investigated (rules / resolvers):**
  - `Rules/BrokenLinksRule.cs`
  - `Rules/RelativeLinksRule.cs`
  - `Rules/OrphanDocsRule.cs`
  - `Services/LinkResolver.cs` (used by check rules)
  - `Services/ILinkResolver.cs`
  - `Services/DocLinkResolver.cs` (used by graph)
  - `Services/DocGraph.cs`
  - `Services/LinkExtractor.cs`
- **Files investigated (orchestration / scope):**
  - `Commands/CheckCommand.cs`
  - `Commands/CheckDocValidator.cs`
  - `Commands/GraphCommand.cs`
  - `Services/DocScanner.cs`
  - `Services/MarkdownParser.cs`
  - `Utils/PathUtils.cs` (`NormalizePath`, `NormalizeForKey`, `NormalizeForPattern`)
  - `Utils/PathUtils.Discovery.cs` (`ResolvePath`, `FindDocsFolder`)
  - `Models/DocFile.cs`, `Models/LinkInfo.cs`
- **Tests audited:**
  - `DynaDocs.Tests/Services/LinkResolverTests.cs`
  - `DynaDocs.Tests/Services/DocGraphTests.cs`
  - `DynaDocs.Tests/Services/PathUtilsTests.cs`
  - `DynaDocs.Tests/Rules/BrokenLinksRuleTests.cs`
  - `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs`
- **Live reproductions on this worktree:**
  - `dydo check` (whole dydo tree) → 1 error, 3 warnings, 0 broken-link false positives in 957 files.
  - `dydo check dydo/project/decisions` → 5 broken-link false positives.
  - `dydo check dydo/project` → 16 errors, 10 of them are broken-link false positives crossing out of `project/`.
  - `dydo check dydo/guides` → 29 broken-link false positives.
  - `dydo check dydo/reference` → 27 broken-link false positives (including `reference/roles/*.md` using `../../project/...`).
  - Hand-crafted 4-file repro tree at `dydo/agents/Frank/repro/` exercising every link shape.
  - PowerShell trace of `PathUtils.ResolvePath` on the failing input (`_decisions.md` → `../pitfalls/_index.md`) showing the resolver returns the correct absolute path.
- **Prior inquisitions consulted:** `dydo-check-drift.md` (Charlie, 2026-05-04) — same family of `dydo check`-rule bugs; #0163 (`DocScanner` recursing into worktrees) is the closest precedent for scanner-scope bugs.
- **Scouts dispatched:** 0. Evidence is grep-able from rule source + hands-on CLI reproductions; the divergence claim is testable directly. Judge dispatched at the end.

### Findings

#### 1. `dydo check <subfolder>` shrinks `allDocs` to the subfolder, breaking every cross-folder link

- **Category:** bug.
- **Severity:** high (this *is* issue #0184; it eats the signal of `dydo check` for any sub-tree invocation).
- **Type:** tested.
- **Evidence:**
  - `Commands/CheckCommand.cs:122-132` (`ResolvePath`): when the user passes a path, returns `Path.GetFullPath(path)` — the subfolder *becomes* the basePath. Falls back to `PathUtils.FindDocsFolder(Environment.CurrentDirectory)` (the docs root) only when no path is given.
  - `Commands/CheckDocValidator.cs:20-23`: `scanner.ScanDirectory(basePath)` builds `allDocs` strictly from that basePath. There is no second pass that walks the full docs tree to populate the link-target universe.
  - `Services/DocScanner.cs:20`: `Directory.GetFiles(path, "*.md", SearchOption.AllDirectories)` — scope is the basePath argument, full stop.
  - `Services/LinkResolver.cs:11-22` (`ResolveLink`): the membership test is `allDocs.FirstOrDefault(d => NormalizePath(d.FilePath).Equals(resolvedPath, …))`. If the resolved target file is not in `allDocs`, the link is declared broken — *even though `resolvedPath` is an absolute path on disk that `File.Exists` would happily confirm.* The rule never falls back to `File.Exists`.
  - `Commands/GraphCommand.cs:49`: `dydo graph` uses `PathUtils.FindDocsFolder(Environment.CurrentDirectory)` as basePath unconditionally. The target file argument is a *query* parameter, not a scope. That asymmetry is the source of the reporter's observation that "`dydo graph` resolves these links and `dydo check` does not."
- **Reproduction (live, this worktree):**
  ```
  $ dydo check dydo/project/decisions
  ERRORS:
    _decisions.md
      - Line 41: Broken link: ../pitfalls/_index.md
    009-crap-per-method-metric.md
      - Line 50: Broken link: ../../understand/architecture.md
    010-baton-passing-and-review-enforcement.md
      - Line 62: Broken link: ../../reference/guardrails.md
    021-unified-general-wait.md
      - Line 104: Broken link: ../issues/resolved/0133-…
    022-auto-resume-crashed-agents.md
      - Line 106: Broken link: ../issues/0130-…
  Found 5 errors, 0 warnings in 27 files.

  $ ls dydo/project/pitfalls/_index.md dydo/understand/architecture.md \
       dydo/reference/guardrails.md
  (all three exist)

  $ dydo check          # no path arg → full docs tree
  Found 1 errors, 3 warnings in 957 files.   # zero broken-link false positives
  ```
- **Algorithmic verification that the resolver itself is correct.** Ran the *exact* `PathUtils.ResolvePath` logic in PowerShell against the failing input:
  ```
  sourcePath = Path.Combine("…\dydo", "project/decisions/_decisions.md")
  sourceDir  = Path.GetDirectoryName(sourcePath)
             = …\dydo\project/decisions   (mixed separators preserved)
  combined   = Path.Combine(sourceDir, "../pitfalls/_index.md")
             = …\dydo\project/decisions\../pitfalls/_index.md
  resolved   = Path.GetFullPath(combined)
             = …\dydo\project\pitfalls\_index.md
  normalized = …/dydo/project/pitfalls/_index.md

  doc.FilePath of the target =
             = …/dydo/project/pitfalls/_index.md     (set by MarkdownParser.Parse:25)

  match = True
  ```
  The resolver lands on the correct target; the failure is purely the `allDocs` membership test rejecting it.
- **Why `dydo graph` works.** `dydo graph` always scans from the docs root (`GraphCommand.cs:49`) regardless of what file you ask it about. Its `allDocs` is therefore complete, so `DocGraph.Build` (`Services/DocGraph.cs:36-37`) finds the same `../../understand/architecture.md` target the check rule rejects.
- **Severity / blast radius matrix (live counts on this worktree):**

  | Scan target | Files scanned | Broken-link false positives |
  |---|---|---|
  | `dydo` (whole tree) | 957 | 0 |
  | `dydo/project` | 894 | 10 |
  | `dydo/project/decisions` | 27 | 5 |
  | `dydo/guides` | 12 | 29 |
  | `dydo/reference` | 20 | 27 |
  | `dydo/agents/Frank/repro` (hand-crafted) | 4 | 1 cross-folder + 3 anchor-only (see finding 2) |

  The count grows fast with folder depth: in `dydo/reference/roles/`, every link to `../../project/decisions/…` (the role docs heavily cite design decisions) becomes a false positive — that single subfolder family alone contributes 11 of the 27 false positives. LC's "55 errors on decisions" reflects a folder that cites guides/reference/understand heavily.
- **Surface map (every relative-link shape, agreement between `dydo check` and `dydo graph` based on the bug above):**

  | Shape | Example | `dydo check <subfolder>` | `dydo check` (root) | `dydo graph` |
  |---|---|---|---|---|
  | `./sibling.md` (same folder, in scope) | `[s](./sibling.md)` | ✅ resolves | ✅ | ✅ |
  | `./_index.md` (sibling folder-meta, in scope) | `[s](./_index.md)` | ✅ resolves | ✅ | ✅ |
  | `../parent.md` (target inside scope) | `sub/inner.md → ../index.md` | ✅ resolves | ✅ | ✅ |
  | `../sibling.md` (target outside scope) | `decisions/_decisions.md → ../pitfalls/_index.md` | ❌ false-positive | ✅ | ✅ |
  | `../../foo/bar.md` (two-up, cross-tree) | `decisions/009.md → ../../understand/architecture.md` | ❌ false-positive | ✅ | ✅ |
  | `./sibling.md#anchor` (in scope) | `[s](./sibling.md#header-a)` | ✅ resolves | ✅ | ✅ (graph ignores anchor part) |
  | `../sibling.md#anchor` (out of scope) | — | ❌ false-positive | ✅ | ✅ |
  | `#section` (anchor-only on same file) | `[me](#section)` | ❌ false-positive (see finding 2) | ❌ false-positive | ✅ (graph yields no edge) |
  | links from `_index.md` / `_foo.md` | as above | same as other shapes | same | same |
  | template files `_system/templates/**` | — | skipped by rule | skipped | skipped |
  | wikilinks `[[foo]]` | — | `RelativeLinksRule` error, `BrokenLinksRule` skips | — | — |

  **Folder-meta `_*.md` finding: REFUTED as a code special case.** No path in `BrokenLinksRule`, `LinkResolver`, `PathUtils.ResolvePath`, or `LinkExtractor` branches on `_*.md`. Folder-meta files appear over-represented in the failure set because their purpose is to link outward (a `_decisions.md` is a navigation index that points to siblings and parents). My repro tree replicates the failure with a regular `index.md` source too (`dydo/agents/Frank/repro/index.md` → `#repro-tree` etc.) confirming it's not filename-dependent.
- **Key resolver-divergence finding for the reporter's hypothesis.** There *are* two physically distinct resolver implementations in the codebase (see finding 3) — but their *output* is equivalent for the failing inputs. The disagreement between `dydo check` and `dydo graph` does not come from resolver code; it comes from the upstream scope decision (subfolder-as-basePath vs always-docs-root).
- **Judge ruling:** CONFIRMED
- **Files examined (Brian, 2026-05-19):** `Commands/CheckCommand.cs` (lines 1-133), `Commands/CheckDocValidator.cs` (lines 1-70), `Commands/GraphCommand.cs` (lines 1-187), `Services/DocScanner.cs` (lines 1-71), `Services/LinkResolver.cs` (lines 1-45), `Utils/PathUtils.cs` (`NormalizePath`, `NormalizeForKey`, `NormalizeForPattern`), `Utils/PathUtils.Discovery.cs` (`ResolvePath` lines 54-59, `FindDocsFolder` lines 61-93).
- **Independent verification:**
  - Re-ran the four live reproductions on this worktree. Counts match Frank's matrix exactly: `decisions` → 5 broken-link errors; `guides` → 29; `reference` → 27; whole tree → 0 broken-link errors (`Found 2 errors, 4 warnings in 959 files` — both errors are Wikilink violations in inquisition files, none are broken-link false positives; the 4 warnings are Orphan-doc warnings on the new inquisition/issue files).
  - Confirmed all 5 link targets in `decisions` exist on disk (`Test-Path` returns True for `dydo/project/pitfalls/_index.md`, `dydo/understand/architecture.md`, `dydo/reference/guardrails.md`, `dydo/project/issues/resolved/0133-…`, `dydo/project/issues/0130-…`).
  - Verified the asymmetry: `dydo graph dydo/project/decisions/009-crap-per-method-metric.md` returns `[degree 1] understand/architecture.md` — the same link the check rule rejects.
  - Hand-traced `CheckCommand.ResolvePath` (lines 122-132) → `CheckDocValidator.Validate` (line 20) → `DocScanner.ScanDirectory` (line 20, `SearchOption.AllDirectories` rooted at the user-supplied path) → `LinkResolver.ResolveLink` (lines 17-20, membership test against the narrowed `allDocs`). The path matches Frank's traced flow.
  - Verified the folder-meta refutation: `grep "BrokenLinksRule\|LinkResolver\|PathUtils.Discovery\|LinkExtractor"` for any `_` or `IsHubFile` branch — no special-case for `_*.md` in the link-validation chain (the `_index.md` / `IsHubFile` branches in `OrphanDocsRule` and `HubFilesRule` are about reachability, not link resolution).
- **Alternative explanations considered:**
  - Could be intentional ("subfolder scan is meant to be a quick local check, don't run it across folder boundaries")? No — `CheckCommand`'s pathArgument has no such constraint, and the help text says "Path to docs folder or file to check" with no indication that targets are scoped to the subfolder. No documented exception in `coding-standards.md` or in `understand/`.
  - Could be a fallback path for `File.Exists` that I missed? `BrokenLinksRule.cs:46` calls `_linkResolver.ResolveLink` for `.md` targets; the resolver itself never falls back to `File.Exists`. The non-`.md` branch (lines 34-44) is the only `File.Exists` path and it doesn't apply here.
- **Issue:** #0185 (severity: high)

#### 2. Anchor-only `[label](#section)` links always produce a "Broken link: " error (empty target)

- **Category:** bug.
- **Severity:** low–medium (depends on whether any non-template doc currently uses this shape; today none does in this repo).
- **Type:** tested.
- **Evidence:**
  - `Services/LinkExtractor.cs:33,69-74`: `SplitAnchor("#section")` returns `path = ""`, `anchor = "section"`. The resulting `LinkInfo.Target` is the empty string.
  - `Rules/BrokenLinksRule.cs:32-44`: the non-`.md` branch is taken whenever `!link.Target.EndsWith(".md")` — *which is true for an empty target*. The branch resolves the link as a non-markdown asset on disk:
    ```csharp
    var resolvedPath = PathUtils.ResolvePath(
        Path.Combine(basePath, doc.RelativePath),
        link.Target                       // ""
    );
    if (!File.Exists(resolvedPath))
        yield return CreateError(doc, $"Broken link: {link.Target}", link.LineNumber);
    ```
  - `Utils/PathUtils.Discovery.cs:54-59`: `ResolvePath(sourcePath, "")` returns `Path.GetFullPath(Path.Combine(sourceDir, ""))` = the **source doc's parent directory**. `File.Exists(directory_path)` is false. Error fires with `Broken link: ` (empty target, no anchor info — `BrokenLinksRule.cs:48` only appends `#anchor` in the .md branch).
  - The rule **never re-enters the anchor-validation path** for anchor-only links. Even if the anchor exists on the same page, the link is reported broken.
- **Reproduction (repro/sub/inner.md):**
  ```
  $ cat dydo/agents/Frank/repro/sub/inner.md
  …
  - [Anchor only](#inner)

  $ dydo check dydo/agents/Frank/repro
  ERRORS:
    sub/inner.md
      - Line 9: Broken link:
  ```
  Same shape on `index.md` and `_index.md` in the same repro tree (three "Broken link:" errors with empty targets in a 4-file tree).
- **Why this is masked in the production tree today.** Grep for `](#` outside templates: the only matches in `dydo/**/*.md` live under `dydo/_system/templates/mode-judge.template.md`, which `RuleSkipPaths.IsTemplateOrAddition` (`Utils/RuleSkipPaths.cs:19-26`) bypasses entirely via the early `yield break` in `BrokenLinksRule.cs:21-22`. So the latent bug never fires *on this repo*. Any new doc that uses `[label](#section)` outside `_system/templates/**` or `_system/template-additions/**` will trigger it.
- **Resolver behaviour for anchor-only links is internally inconsistent.** `LinkResolver.ResolveLink` (`Services/LinkResolver.cs`) would handle anchor-only links correctly *if* the rule actually called it — it would compute `resolvedPath` = source doc, find the source doc in `allDocs`, and validate the anchor against `targetDoc.Anchors`. The bug is the `EndsWith(".md")` short-circuit in `BrokenLinksRule.cs:32` that sends empty-target links down the wrong path.
- **Judge ruling:** CONFIRMED (with one peripheral inaccuracy noted)
- **Files examined (Brian, 2026-05-19):** `Services/LinkExtractor.cs` (lines 1-92, especially `SplitAnchor` at 69-74), `Rules/BrokenLinksRule.cs` (lines 1-53), `Utils/PathUtils.Discovery.cs` (`ResolvePath` lines 54-59), `Utils/RuleSkipPaths.cs` (lines 1-27), `Services/LinkResolver.cs` (lines 1-45).
- **Independent verification:**
  - Reproduced on `dydo check dydo/agents/Frank/repro`: three "Broken link: " errors with empty targets (`_index.md` line 6 → `#repro-hub`, `index.md` line 6 → `#repro-tree`, `sub/inner.md` line 9 → `#inner`). Output text matches the predicted format exactly (empty after the colon — the `.md` branch is the only path that appends the anchor info).
  - Verified the latency claim: `grep "\]\(#" dydo/**/*.md` returns exactly one match — `dydo/_system/templates/mode-judge.template.md`. `RuleSkipPaths.IsTemplateOrAddition` (lines 19-26) returns true for `_system/templates/` and `_system/template-additions/` prefixes, so `BrokenLinksRule.cs:21-22` yields-breaks before the buggy path. The bug is genuinely latent in production.
  - Hand-traced the empty-target flow: `LinkExtractor.SplitAnchor("#section")` returns `(path: "", anchor: "section")` (line 71-73, `target[..anchorIndex]` when `anchorIndex == 0` is the empty string). `BrokenLinksRule.cs:32` — `"".EndsWith(".md")` is false, so the non-`.md` branch is taken. `PathUtils.ResolvePath(Path.Combine(basePath, doc.RelativePath), "")` evaluates to `Path.GetFullPath(Path.Combine(sourceDir, ""))` = source dir (a directory path). `File.Exists(<directory>)` is false. Error fires with empty target.
- **Peripheral inaccuracy in Frank's evidence (does not change the ruling):** Frank wrote that `LinkResolver.ResolveLink` would handle anchor-only links correctly if called — that's not quite right. With `link.Target = ""`, `PathUtils.ResolvePath` returns the source *directory*, not the source *file*. The `allDocs.FirstOrDefault(d => d.FilePath.Equals(resolvedPath, …))` membership test on `LinkResolver.cs:17-20` would then also fail (no doc has a directory path as its `FilePath`). The underlying bug is real and reproduced; the suggested fix path needs to explicitly handle the anchor-only case (e.g., when `link.Target == "" && link.Anchor != null`, validate the anchor against `doc.Anchors` directly).
- **Alternative explanations considered:** Could the empty-target error be considered acceptable because the markdown is unusual? No — anchor-only intra-page links (`[me](#section)`) are valid markdown and standard usage; there's no documented exception. Could `Validate_AcceptsValidAnchor` in `BrokenLinksRuleTests` already cover this? No — that test uses a non-empty target (`./reference.md`), so it exercises the `.md` branch, not the empty-target path.
- **Issue:** #0186 (severity: low — latent in this repo today)

#### 3. Two parallel resolver implementations (`LinkResolver` vs `DocLinkResolver`)

- **Category:** antipattern.
- **Severity:** low (no current behaviour bug — but a fragility/maintenance liability and a hazard for the eventual fix).
- **Type:** obvious.
- **Evidence:**
  - `Services/LinkResolver.cs:6-28` — instance class implementing `ILinkResolver`, takes `List<DocFile>` + `basePath`, computes target via `PathUtils.ResolvePath(Path.Combine(basePath, sourceDoc.RelativePath), link.Target)` then membership-tests against `allDocs[i].FilePath`. Used by `Rules/BrokenLinksRule.cs:46` and `Rules/OrphanDocsRule.cs:132-138`.
  - `Services/DocLinkResolver.cs:6-46` — `internal static` class, takes only `sourceDoc`, `link`, `basePath`. Manually splits on `/`, walks `..` segments via a `List<string>`, returns a `basePath`-relative string. Used by `Services/DocGraph.cs:33` only.
  - Both arrive at equivalent paths for in-tree relative links (verified by hand-trace on `_decisions.md → ../pitfalls/_index.md` and by the live success of both `dydo graph` and `dydo check` (root) on the same docs tree).
  - Algorithmic differences worth flagging for the fix slice:
    1. **Anchor handling.** `DocLinkResolver` strips a trailing `#…` itself (`DocLinkResolver.cs:12-15`) even though `LinkExtractor` has already stripped it (`LinkExtractor.cs:33,69-74`). Dead code in production but a tripwire for any future caller that bypasses `LinkExtractor`.
    2. **Empty-target handling.** `DocLinkResolver` returns `null` for empty targets (anchor-only links). `LinkResolver` does not have an equivalent early-exit and lets the empty target flow into `Path.Combine` (which is the surface for finding 2, indirectly).
    3. **`.` and `..` walk.** `DocLinkResolver` does its own segment-walk against the *relative* path string; `LinkResolver` delegates to `Path.GetFullPath` (which collapses `..` and normalises separators). The two would diverge on a path that walks above the docs root with `..` (the OS resolver continues up the filesystem; the manual walker stops at empty) — but no in-tree doc can legitimately do that, so it's a latent rather than active divergence.
    4. **Case handling.** `LinkResolver` compares with `OrdinalIgnoreCase` (`Services/LinkResolver.cs:18`). `DocGraph` lowercases via `NormalizeForKey` (`Services/DocGraph.cs:20,27,36` + `Utils/PathUtils.cs:159`). Equivalent on case-insensitive filesystems; potentially divergent on Linux where filename case is significant.
- **Why this matters for the fix.** Any fix that converges `dydo check` onto the docs-root scan will likely route both consumers through one of these two resolvers (or a third). The fix should be aware that *today* the codebase pretends to have one resolver behaviour while actually carrying two. Reducing to one — and adding a direct test that both consumers agree on every shape — would prevent the next instance of this same family of report.
- **Judge ruling:** CONFIRMED
- **Files examined (Brian, 2026-05-19):** `Services/LinkResolver.cs` (lines 1-45), `Services/ILinkResolver.cs` (lines 1-10), `Services/DocLinkResolver.cs` (lines 1-46), `Services/DocGraph.cs` (lines 1-100), `Services/LinkExtractor.cs` (lines 33, 69-74), `Rules/BrokenLinksRule.cs` (line 46), `Rules/OrphanDocsRule.cs` (lines 132-138), `Utils/PathUtils.cs` (`NormalizeForKey` line 157-160), `Utils/PathUtils.Discovery.cs` (`ResolvePath` lines 54-59).
- **Independent verification:** Each of the four named differences checked against the source:
  1. **Anchor handling.** ✓ Confirmed. `DocLinkResolver.cs:12-15` strips a trailing `#…` from the target. `LinkExtractor.cs:69-74` (`SplitAnchor`) already strips it before `LinkInfo` is constructed. Dead code with respect to today's only caller (`DocGraph.cs:33`).
  2. **Empty-target handling.** ✓ Confirmed. `DocLinkResolver.cs:17-18` early-returns `null` on empty target. `LinkResolver.cs:8-15` has no such check — the empty target flows into `Path.Combine`.
  3. **`.`/`..` walk.** ✓ Confirmed. `DocLinkResolver.cs:27-42` walks segments against the relative path *string*. `LinkResolver` → `PathUtils.ResolvePath:54-59` delegates to `Path.GetFullPath`. Behaviours would diverge for a relative path that walks above the docs root (which the OS resolver would let escape the root; the manual walker would stop at empty).
  4. **Case handling.** ✓ Confirmed. `LinkResolver.cs:18` uses `StringComparison.OrdinalIgnoreCase`. `DocGraph.cs:20,27,36` builds its key set via `PathUtils.NormalizeForKey` which `ToLowerInvariant()`s (`PathUtils.cs:157-160`). Equivalent on case-insensitive filesystems; potentially divergent on Linux for paths with mixed-case characters.
- **Alternative explanations considered:** Could one be staged to replace the other? No TODO, no inquisition, no commit message hint, and both have live callers (`BrokenLinksRule`/`OrphanDocsRule` and `DocGraph` respectively). Could the differences be deliberate (e.g., `DocGraph` needs a relative-string output while `LinkResolver` needs a membership probe)? Partially — `DocLinkResolver` does return a basepath-relative string and `LinkResolver` does membership-test. But the four named differences are *not* required by that contract distinction; they're incidental.
- **Issue:** #0187 (severity: low — antipattern, no current production divergence)

#### 4. Test coverage gap for the failing shapes

- **Category:** missing-test.
- **Severity:** medium (the bug shipped because no test exercises the failing shape).
- **Type:** obvious.
- **Evidence:**
  - `DynaDocs.Tests/Rules/BrokenLinksRuleTests.cs` covers: same-folder `./reference.md`, nested `./guides/how-to.md`, parent `../index.md` *with the target in `allDocs`*, anchor matching, template-skip. **No test passes `allDocs` that omits a real on-disk target file.** All tests construct `allDocs` to include the link target — they cannot detect the bug.
  - **No test for `..` traversing two or more levels** (`../../`) — the failing shape in `decisions/009-crap-per-method-metric.md → ../../understand/architecture.md` is uncovered.
  - **No test for anchor-only `[label](#section)`** in `BrokenLinksRuleTests`. `LinkResolverTests` covers `ValidateAnchor` directly but never exercises the `BrokenLinksRule` path for an empty `link.Target`.
  - **No direct tests for `DocLinkResolver`.** It is only exercised transitively through `DocGraphTests`. Result: behavioural drift between `DocLinkResolver` and `LinkResolver` cannot be detected by the test suite.
  - **No direct tests for `PathUtils.ResolvePath`.** `PathUtilsTests.cs` covers `IsKebabCase`, `NormalizePath`, `NormalizeForPattern`, `NormalizeForKey`, `NormalizeWorktreePath`, `GetMainProjectRoot`, `IsInsideWorktree`, `EnsureLocalDirExists`. `ResolvePath` — the core path-arithmetic primitive used by *every* link-validation rule — has zero direct tests.
  - **No `CheckCommand` integration test for subfolder invocation.** `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs:124` runs `dydo check .` (whole tree). Nothing exercises `dydo check <subfolder>` and asserts that an in-tree link to a sibling folder is *not* reported broken.
- **Suggested regression test names** (for the fix slice — names only, no implementation):
  - `BrokenLinksRuleTests.Validate_AcceptsCrossFolderLink_WhenTargetNotInAllDocs_ButExistsOnDisk` — would have failed today.
  - `BrokenLinksRuleTests.Validate_AcceptsTwoLevelParentLink_AcrossFolders` (`../../foo/bar.md`).
  - `BrokenLinksRuleTests.Validate_AcceptsAnchorOnlyLink_WhenAnchorExistsOnSamePage`.
  - `BrokenLinksRuleTests.Validate_ReportsAnchorOnlyLink_WhenAnchorDoesNotExist`.
  - `BrokenLinksRuleTests.Validate_DoesNotEmitEmptyTargetError_ForAnchorOnlyLink`.
  - `PathUtilsTests.ResolvePath_HandlesMixedSeparators` (basePath has `\`, RelativePath has `/`).
  - `PathUtilsTests.ResolvePath_CollapsesParentSegments` (`../../foo/bar.md`).
  - `LinkResolverTests.ResolveLink_AcceptsCrossFolderRelativeLink` (`../sibling/file.md`).
  - `LinkResolverTests.ResolveLink_AcceptsTwoLevelParentLink` (`../../foo/bar.md`).
  - `LinkResolverTests.ResolveLink_AnchorOnlyLink_ValidatesAgainstSourceDocAnchors`.
  - `DocLinkResolverTests.Resolve_AgreesWithLinkResolver_OnEveryRelativeShape` — *parametrised cross-resolver agreement test*. The single most important regression guard.
  - `CheckCommandTests.Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder` — the integration-level guard. Whether the fix is "scan from root, validate selected subset" or "fail fast when basePath is a strict subfolder of the docs root" or "fall back to `File.Exists` for the .md branch," this test names the contract the fix has to satisfy.
- **Judge ruling:** CONFIRMED (with one inaccuracy noted on the `ResolvePath` count)
- **Files examined (Brian, 2026-05-19):** `DynaDocs.Tests/Rules/BrokenLinksRuleTests.cs` (lines 1-181), `DynaDocs.Tests/Services/LinkResolverTests.cs` (lines 1-248), `DynaDocs.Tests/Services/PathUtilsTests.cs` (lines 1-332), `DynaDocs.Tests/Services/PathUtilsDiscoveryTests.cs` (lines 1-166), `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs` (line 124, `Init_ThenCheck_RunsWithoutCrash`).
- **Independent verification:**
  - Confirmed `BrokenLinksRuleTests` — every test that needs a valid target adds it to `allDocs` (`Validate_AcceptsValidRelativeLink`, `Validate_AcceptsValidAnchor`, `Validate_AcceptsLinkToNestedFile`, `Validate_AcceptsParentDirectoryLink`). The bug's failure shape (target on disk but absent from `allDocs`) is not exercised. No `../../` test. No anchor-only `link.Target = ""` test.
  - Confirmed `LinkResolverTests` covers `ValidateAnchor` and `ResolveLink` with a non-empty target, but never with `link.Target = ""`.
  - Confirmed `DocLinkResolverTests` does **not** exist (`Glob DynaDocs.Tests/**/*LinkResolver*` returns only `LinkResolverTests.cs`; `grep "DocLinkResolver" DynaDocs.Tests/**/*.cs` returns no matches).
  - Confirmed `CliEndToEndTests` only runs `dydo check .` (whole tree) — no `dydo check <subfolder>` integration test.
- **One inaccuracy in Frank's evidence (does not change the ruling):** Frank wrote that `PathUtils.ResolvePath` has *zero* direct tests. There are in fact 3 direct tests in `PathUtilsDiscoveryTests.cs:40-54` (`ResolvePath_ResolvesRelative` with two inline-data cases, `ResolvePath_NormalizesBackslashes`). However, those tests only assert that the output contains *no backslash and no `..`* — they never assert the *correct* resolved path. So Frank's substantive point — that `ResolvePath` is severely under-tested for the core path-arithmetic primitive it's supposed to be — stands. The "zero" claim should be "three weak tests, no positive-correctness tests."
- **Alternative explanations considered:** Could the missing tests be deliberate (e.g., the cross-folder scenario was considered out of scope)? No — there's no test-plan doc or coverage-omission record in `dydo/project/` that excludes this shape. Could `Init_ThenCheck_RunsWithoutCrash` be a sufficient integration guard? No — it asserts only `ExitCode <= 1` and `"Checking" in stdout`; it does not assert the *content* of the check output.
- **Issue:** #0188 (severity: medium)

### Hypotheses Not Reproduced

- **Reporter's hypothesis: "two resolvers, divergent algorithms."** Refuted as the *cause of the visible failure*. The two resolvers produce identical results on the failing inputs. The visible divergence comes from `dydo check` and `dydo graph` operating against different scan scopes, not different resolver logic. *Caveat:* there really are two resolver implementations (finding 3); they just don't disagree on the inputs that surface in this bug. A future input could expose a real algorithm-level divergence — but #0184 is not that today.
- **Reporter's hypothesis: folder-meta `_*.md` files have a special path-anchoring bug.** Refuted. No code path branches on `_` prefixes in the link-validation chain. Folder-meta files appear in the failure set because they concentrate outward navigation links by purpose. The hand-crafted repro tree reproduces the failure on a non-`_` `index.md` source as readily as on `_index.md`. (`HubFilesRule` and `OrphanDocsRule` *do* branch on `_index.md` / `IsHubFile`, but those branches are about coverage-reachability, not link resolution.)
- **Suspected: URL decoding mismatch.** Refuted. Neither resolver URL-decodes link targets. No `%20`-style links exist in the failing set.
- **Suspected: junction/symlink boundary corruption in worktrees.** Refuted for this bug class. The four junction-shared trees (`dydo/agents/`, `dydo/_system/roles/`, `dydo/project/issues/`, `dydo/project/inquisitions/`) are not implicated in the failures: the false positives reproduce with the same multiplicity whether the link target is in a junctioned folder (`021 → ../issues/resolved/0133`, `022 → ../issues/0130`) or a non-junctioned folder (`_decisions → ../pitfalls/_index.md`, `009 → ../../understand/architecture.md`). The bug class is independent of the worktree junction layout.
- **Suspected: case sensitivity bug.** Refuted on this Windows host (the failing paths are all lowercase). Possibly latent on Linux — `LinkResolver` uses `OrdinalIgnoreCase`, `DocGraph` lowercases via `NormalizeForKey`. Calling that out under finding 3 rather than as its own finding.

**Judge verification of refutations (Brian, 2026-05-19):** All five refutations stand.
- Resolver-divergence-as-cause-of-#0184: verified — `dydo check` (root) and `dydo graph` both succeed on the same input that `dydo check <subfolder>` fails. The visible failure is upstream of resolver code.
- Folder-meta `_*.md` special case: verified — grepped `Rules/BrokenLinksRule.cs`, `Services/LinkResolver.cs`, `Services/DocLinkResolver.cs`, `Utils/PathUtils.cs`, `Utils/PathUtils.Discovery.cs`, `Services/LinkExtractor.cs` for `_` prefix branches in the link chain; none exist. `HubFilesRule` / `OrphanDocsRule` branch on hub-ness but only for reachability, not link resolution.
- URL decoding: verified — no `Uri.UnescapeDataString` or `%`-decoding in any resolver path; both `LinkResolver` and `DocLinkResolver` treat targets as literal strings.
- Junction/symlink boundary: verified — the false-positive set spans both junctioned (`issues/`) and non-junctioned (`pitfalls/`, `understand/`) trees with identical multiplicity.
- Case sensitivity: verified — on this Windows host all failing paths are lowercase; the Linux risk under finding 3 is correctly characterised as latent.

### Relationship to the cross-tree `dydo/** → src/**` bug

Out of scope per the brief. From the available evidence I can report:

- The cross-tree `dydo/** → src/**` issue (the LC-side report about links crossing *out* of the docs tree) is a **different bug class with a different root cause**. That bug involves a link target that is intentionally outside the docs tree (under `src/**`); the validator has no mechanism to reach outside the configured docs root, by design.
- The in-tree `dydo/** → dydo/**` failure reported in #0184 stays *inside* the docs tree — the targets are real, scannable, indexed files; they're just excluded from the *invocation-narrowed* `allDocs`.
- **Shared root cause? No.** The cross-tree case fails because `DocScanner` cannot scan a sibling tree (architectural). The in-tree case fails because `DocScanner` is told to scan less than the full docs tree (invocation-dependent). Fix scoping should treat them independently — a fix that converges `dydo check`'s scope to the docs root resolves #0184 but does nothing for cross-tree.
- **Implication for the fix slice for #0184:** the fix can ignore the cross-tree case. The cross-tree case will need its own ticket and its own design (link kind that opts out of scanning, or a configured cross-tree manifest, etc.).

### Workaround footprint in this repo

The reporter's workaround — convert `[label](../foo.md)` into the backtick code-ref `` `dydo/foo.md` `` — has **not spread** through this repo's docs tree. `grep -rn '\`dydo/[a-z][a-z-]*\.md\`'` matches several files in `dydo/guides/`, `dydo/reference/`, `dydo/understand/`, `dydo/project/decisions/`, but on inspection these are all **natural in-prose mentions** of file paths (e.g. "the AI reads `CLAUDE.md`, gets redirected to `dydo/index.md`"), not conversions of broken links. The relative-markdown-link form is still the dominant shape across the docs tree (957 files, 0 broken-link false positives on whole-tree check).

**Implication for the fix slice:** post-fix, there is no widespread workaround to undo. The cleanup is bounded to whatever LC has converted and any *future* conversions Kate/the reporter perform on this side. If Adele or another agent has been advising on workarounds, that guidance should be retracted once the fix lands.

**`RelativeLinksRule` does NOT validate backtick code refs.** `Rules/RelativeLinksRule.cs:14-41` only inspects `doc.Links` (the markdown-parsed link list); the backtick code-ref workaround flies past it because it never enters `doc.Links` at all. No rule conflicts with the workaround. The workaround's only cost is loss of click-through navigation in editors — and the entropic risk that prose-mentions of file paths drift out of sync with the filesystem without anyone noticing.

### Test evidence — preserved in this report (worktree is temporary)

The hand-crafted 4-file repro tree lives under `dydo/agents/Frank/repro/` in this worktree. The relevant files, for the code-writer who fixes this:

```
dydo/agents/Frank/repro/index.md      # ./sibling.md, ./_index.md, ./sub/inner.md, anchor-only #repro-tree
dydo/agents/Frank/repro/_index.md     # anchor-only #repro-hub, ../../../understand/architecture.md (out of scope)
dydo/agents/Frank/repro/sibling.md    # plain doc, has a "Header A" anchor
dydo/agents/Frank/repro/sub/inner.md  # ../index.md (in scope), ../sibling.md (in scope), anchor-only #inner
```

Expected post-fix behaviour for `dydo check dydo/agents/Frank/repro`:
- Zero broken-link errors for in-scope links (`./sibling.md`, `./_index.md`, `./sub/inner.md`, `../index.md`, `../sibling.md`, `./sibling.md#header-a`).
- Zero broken-link error for valid anchor-only links (`#repro-tree` on `index.md`, `#repro-hub` on `_index.md`, `#inner` on `sub/inner.md`).
- For the cross-folder out-of-scope link (`_index.md` line 7 → `../../../understand/architecture.md`): the answer depends on which fix shape is chosen — if the fix walks up to the docs root, *no error*; if the fix keeps the subfolder scope but falls back to `File.Exists` for `.md` targets, *no error*; if the fix simply *rejects out-of-scope basePath invocation*, the entire invocation rejects with a clear message instead of yielding false positives.

### Confidence: high

- **Hard-evidenced (confidence: high):** finding 1 (root cause, severity matrix, surface map, refutation of resolver-divergence-as-algorithm), finding 2 (anchor-only bug), the folder-meta refutation, the workaround-footprint claim, the test-coverage list.
- **Hard-evidenced but bounded (confidence: medium):** finding 3 — the algorithmic differences between `LinkResolver` and `DocLinkResolver` are observable in code, but I have not constructed an input where they actually produce divergent answers in production. The case-sensitivity divergence in particular is Windows-only verified; Linux behaviour is by inspection only.
- **Scoped out (no claim made):** the cross-tree `dydo/** → src/**` bug class. I have stated only the relationship to it, not investigated its mechanics.
- **Not investigated:** performance characteristics of `dydo check` on the full tree (957 files, ~1.5s), Markdig anchor-extraction edge cases, behaviour when basePath has a trailing slash or when invoked with a relative `./project/decisions` vs absolute path (both surfaced identical false-positive counts in spot-checks — no further divergence found).

---

### For the planner (Brian — judge — 2026-05-19)

A consolidated brief for whoever plans the fix slice. Issues #0185–#0188 (filed) capture each finding's surface; the cross-references below are the bits a planner would otherwise have to dig out of the report body.

**Fix-slice scope** — one slice should resolve issues **#0185, #0186, #0188**. Issue **#0187** (parallel resolvers) is the *enabling* code-health change that makes the fix safe; if the slice doesn't fold #0187 in, the next fix in this area will hit the same tripwires. Recommend bundling all four.

**What the slice must do (acceptance criteria):**

1. **`dydo check <subfolder>` accepts cross-folder links to in-tree targets that exist on disk** (issue #0185). Three fix shapes are viable — see finding 1 for the trade-offs:
   - **(a) Scan from docs root, validate the user-supplied subset.** Most flexible; preserves the user's expected scope semantics for the violation output.
   - **(b) Keep subfolder scan, fall back to `File.Exists` for `.md` link targets that miss `allDocs`.** Smallest diff; mildly weakens the validator (cannot then validate anchors on out-of-scope targets — they fall through `File.Exists` only).
   - **(c) Reject subfolder invocation with a clear error** instructing the user to run from the docs root. Simplest behavioural fix; least useful UX.
   - **Recommendation:** (a). Matches `dydo graph`'s already-correct scope decision and preserves anchor validation across the tree.
2. **Anchor-only `[label](#section)` links validate against the source doc's own anchors** (issue #0186). Two paths through the code:
   - In `BrokenLinksRule.cs:24-51`, add an explicit branch *before* the `EndsWith(".md")` check: when `link.Target == "" && link.Anchor != null`, validate via `_linkResolver.ValidateAnchor(link.Anchor, doc)`.
   - **OR** make `LinkResolver.ResolveLink` recognise empty-target as "source doc is the target." This is the more orthogonal fix and lines up with #0187 (one resolver path, not multiple call sites coding around it).
3. **Collapse `LinkResolver` and `DocLinkResolver` to one implementation** (issue #0187). Recommendation: delete `Services/DocLinkResolver.cs`, extend `ILinkResolver` with whatever shape `DocGraph` needs (probably a `string? ResolveToRelativeKey(...)` method that returns the basepath-relative target string), and have `DocGraph.cs:33` call into it. This eliminates the four named tripwires in finding 3.
4. **Add the regression tests named in finding 4** (issue #0188). Minimum set:
   - `BrokenLinksRuleTests.Validate_AcceptsCrossFolderLink_WhenTargetNotInAllDocs_ButExistsOnDisk` — guards #0185 directly.
   - `BrokenLinksRuleTests.Validate_AcceptsTwoLevelParentLink_AcrossFolders` (`../../foo/bar.md`).
   - `BrokenLinksRuleTests.Validate_AcceptsAnchorOnlyLink_WhenAnchorExistsOnSamePage` — guards #0186.
   - `BrokenLinksRuleTests.Validate_ReportsAnchorOnlyLink_WhenAnchorDoesNotExist`.
   - `BrokenLinksRuleTests.Validate_DoesNotEmitEmptyTargetError_ForAnchorOnlyLink`.
   - `DocLinkResolverTests.Resolve_AgreesWithLinkResolver_OnEveryRelativeShape` — *parametrised* cross-resolver agreement test. If #0187 is folded in by deletion, this becomes `LinkResolverTests.Resolve_HandlesEveryRelativeShape_UsedByBothCallers` instead.
   - `CheckCommandTests.Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder` — integration-level guard for #0185.
   - `PathUtilsTests.ResolvePath_HandlesMixedSeparators` + `PathUtilsTests.ResolvePath_CollapsesParentSegments` — positive-correctness tests; `PathUtilsDiscoveryTests` currently only checks the result contains no `\` and no `..`.

**Test fixture for the planner / code-writer**

Hand-crafted at `dydo/agents/Frank/repro/` (4 files). Source-of-truth for "every link shape the fix has to handle correctly." Use these as the basis for `CheckCommandTests.Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder` and as a manual smoke-test target during development. The expected post-fix behaviour for `dydo check dydo/agents/Frank/repro` is enumerated in **Test evidence — preserved in this report** above.

**Note on git tracking:** the repro tree is currently **untracked** in this worktree's git index (`dydo/agents/` is junction-shared so the files exist in the main tree on disk, but neither tree has committed them). The planner should either commit the repro tree to `master` before the fix slice begins, or fold an equivalent fixture into the test suite as `DynaDocs.Tests/TestData/`.

**Out of scope for this fix slice** — confirm before expanding:
- The cross-tree `dydo/** → src/**` bug class (LC-side report). Different root cause; needs its own ticket and design (see **Relationship to the cross-tree…** section above). Folding it in would broaden the slice unnecessarily.
- Backtick code-ref `` `dydo/foo.md` `` workaround undo. The workaround has not spread; relative-markdown links are still the dominant shape across 957 files. No bulk-undo needed; future advice from Adele or other agents to use the workaround should be retracted post-fix.

**Verification recipe (planner can hand this to the reviewer):**

| Command | Expected post-fix |
|---|---|
| `dydo check` | unchanged from today (0 broken-link errors) |
| `dydo check dydo/project/decisions` | 0 broken-link errors (was 5 false positives) |
| `dydo check dydo/guides` | 0 broken-link errors (was 29) |
| `dydo check dydo/reference` | 0 broken-link errors (was 27) |
| `dydo check dydo/agents/Frank/repro` | 0 broken-link errors on in-scope and anchor-only links; out-of-scope link behaves per chosen fix shape |
| `dotnet test --filter "FullyQualifiedName~BrokenLinksRule\|FullyQualifiedName~LinkResolver\|FullyQualifiedName~CheckCommand\|FullyQualifiedName~PathUtils"` | all pass, including the new regression tests |

**Files the planner should expect to touch:**
- `Commands/CheckCommand.cs` (scope decision)
- `Commands/CheckDocValidator.cs` (if shape (a) — needs second pass or different `allDocs` source)
- `Rules/BrokenLinksRule.cs` (anchor-only branch)
- `Services/LinkResolver.cs` (potentially: empty-target handling, the public method `DocGraph` needs)
- `Services/DocLinkResolver.cs` (delete if #0187 folded in)
- `Services/DocGraph.cs` (re-route through `ILinkResolver`)
- `Services/ILinkResolver.cs` (extend if `DocGraph` needs a new method)
- `DynaDocs.Tests/Rules/BrokenLinksRuleTests.cs` (regression tests)
- `DynaDocs.Tests/Services/LinkResolverTests.cs` (regression tests)
- `DynaDocs.Tests/Services/PathUtilsDiscoveryTests.cs` (positive `ResolvePath` tests)
- `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs` (new `Check_Subfolder_…` test)
- Possibly new: `DynaDocs.Tests/Services/DocLinkResolverTests.cs` (only if #0187 keeps both resolvers)

**Rule-of-three / anti-slop reminder.** This report names a real second resolver (#0187) and a real test gap (#0188). The temptation will be to "while we're here" expand into the cross-tree bug, refactor the rule framework, etc. Don't. The slice ends when the four verification commands above pass.
