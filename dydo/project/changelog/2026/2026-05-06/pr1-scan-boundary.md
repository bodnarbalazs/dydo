---
area: general
type: changelog
date: 2026-05-06
---

# Task: pr1-scan-boundary

PR1 of dydo-check-drift batch (#0163 + D5 scaffold). Implements scan-boundary fix and the RuleBase.ShouldSkip + RuleSkipPaths.IsTemplateOrAddition scaffold. PR2 will move the per-rule skip blocks onto the scaffold; PR1 only adds it.

FILES CHANGED
Source (modified):
- Models/DydoConfig.cs - added ScanExclude property (after Queues, line 35)
- Services/ConfigFactory.cs - DydoInternalScanExclude constant ([_system/.local/, _system/audit/]); EnsureDefaultScanExclude; FindMissingScanExcludeInvariants; CreateDefault now seeds ScanExclude
- Services/DocScanner.cs - optional IConfigService injection (default null -> new ConfigService); ScanDirectory filters by merged scan-exclude (DydoInternalScanExclude UNION user entries from dydo.json); GetScanExcludes loads config via basePath
- Rules/RuleBase.cs - added 'protected virtual bool ShouldSkip(DocFile) => false' (default false; rules opt-in in PR2)
- Commands/CheckCommand.cs - invokes CheckConfigValidator before doc validation; surfaces invariant errors to the top of the report
- Commands/FixCommand.cs - calls ConfigFactory.EnsureDefaultScanExclude(config) under FIXED block; reports 'Restored N scanExclude invariant(s) in dydo.json'
- Commands/TemplateCommand.cs - ExecuteUpdate calls new EnsureScanExcludeWithReport helper alongside existing EnsureDefaultNudges/EnsureDefaultQueues blocks (helper extracted to keep ExecuteUpdate's CC unchanged - see CRAP note below)

Source (new):
- Commands/CheckConfigValidator.cs - emits one error per missing entry from DydoInternalScanExclude
- Utils/RuleSkipPaths.cs - static helper IsTemplateOrAddition(normalizedRelativePath) for _system/templates/ and _system/template-additions/

Tests added (4 files, ~22 new tests):
- DynaDocs.Tests/Utils/RuleSkipPathsTests.cs (new) - 11 cases (true/false/case-insensitive)
- DynaDocs.Tests/Services/DocScannerTests.cs (new) - 5 cases (excludes .local/audit; doesn't exclude templates/template-additions; honors user entries; applies invariants even when config drops them)
- DynaDocs.Tests/Integration/ScanExcludeInvariantsTests.cs (new) - 3 cases (fresh init populates invariants; check errors on missing invariant; fix restores while preserving user entries)
- DynaDocs.Tests/Integration/TemplateCommandTests.cs (modified) - 3 new cases (template update restores missing invariant; no-op when invariants already present; --diff doesn't mutate)

Config:
- dydo.json - scanExclude block added (BC migration applied to this project via dydo fix)

VERIFICATION GATE - all green:
- dotnet build: 0 warnings, 0 errors
- python DynaDocs.Tests/coverage/run_tests.py: 4076/4076 pass (was 4073)
- python DynaDocs.Tests/coverage/gap_check.py: 139/139 modules pass tier requirements (T1)
- dydo check on dydo project: regression-free. Old binary baseline: 54 errors, 23 warnings, 894 files. New binary: 54 errors, 23 warnings, 893 files. The 1-file delta is _system/audit/_audit.md now excluded by the invariant (see surprise #1 below). All 54 errors and 23 warnings are pre-existing PR2/PR3 territory.
- scanExclude invariants check+fix loop manually verified on this project: removed scanExclude entries -> dydo check emits 'dydo.json scanExclude is missing required entry' for both invariants (exit code 2) -> dydo fix prints 'Restored 2 scanExclude invariant(s) in dydo.json' -> dydo check no longer reports the invariant errors.

DEVIATIONS FROM PLAN
None in scope. Per-rule moves of IsTemplateOrAddition deferred to PR2 as plan instructed (RuleBase.ShouldSkip virtual added but not yet consumed by any rule). Plan's invariant-validation 'TBD by code-writer' choice: I created Commands/CheckConfigValidator.cs to mirror the existing CheckAgentValidator/CheckDocValidator pattern in CheckCommand.

CRAP NOTE
First gap_check pass after my ExecuteUpdate edit failed Commands/TemplateCommand.cs at CRAP 33.8 (T1 limit 30) because adding an inline 'if (scanExcludeAdded > 0)' bumped ExecuteUpdate's CC by 1 over the file's already-borderline budget. Refactored to extract a small EnsureScanExcludeWithReport helper - same shape as EnsureDefaultNudges/EnsureDefaultQueues should arguably be (but aren't today; out of scope to refactor). Final gap_check: 139/139 pass.

SURPRISES FOR PR2/PR3
1. The plan asserts '_system/audit/' currently produces zero *.md (so excluding it is preventive). It actually does produce one: dydo/_system/audit/_audit.md (scaffolded by FolderScaffolder.GenerateAuditMetaMd at line 209). With PR1's invariant active, this file is now excluded from rule validation. It passed all rules vacuously today (it's well-formed framework-owned content), so no error/warning delta - but PR2/PR3 should decide: (a) refine the invariant to '_system/audit/<year>/' patterns, (b) accept that _audit.md is no longer subject to rule validation and document it. Lean (b) for simplicity but flagging it.

2. Hub format drift: running 'dydo fix' on this dydo project regenerates ~18 changelog _index.md files because committed _index.md uses KebabToTitleCase(filename) but current HubGenerator uses doc.Title which produces 'Task: <name>' prefix on changelog entries. Not a PR1 issue (pre-existing) but PR2's BC migration probe ('dydo template update' then 'dydo fix' on this project) will produce a noisy diff. I reverted these from PR1's footprint via 'git checkout HEAD --' since they're not in scope.

3. The plan's surface-surprise #1 (HubFilesRule needing project/tasks/ skip after D4) still holds - PR2 will need to add that skip when _index.md autogen for tasks/ stops.

4. Plan's surface-surprise #2 (three layered exclusion mechanisms: scan boundary, HubGenerator.IsExcludedPath, FixHubHandler.IsExcludedFolder) is now load-bearing in PR1's design. Each serves a different purpose. PR2/PR3 docs sweep should describe all three layers in reference/configuration.md.

DOC SCANNER SIGNATURE NOTE
Made the IConfigService param optional (default null -> new ConfigService()) following the existing SnapshotService pattern, so all 7 'new DocScanner(parser)' call sites continue to compile unchanged. CheckCommand/FixCommand use the explicit two-arg form for clarity in the FIXED-block helper.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

PR1 of dydo-check-drift batch (#0163 + D5 scaffold). Implements scan-boundary fix and the RuleBase.ShouldSkip + RuleSkipPaths.IsTemplateOrAddition scaffold. PR2 will move the per-rule skip blocks onto the scaffold; PR1 only adds it.

FILES CHANGED
Source (modified):
- Models/DydoConfig.cs - added ScanExclude property (after Queues, line 35)
- Services/ConfigFactory.cs - DydoInternalScanExclude constant ([_system/.local/, _system/audit/]); EnsureDefaultScanExclude; FindMissingScanExcludeInvariants; CreateDefault now seeds ScanExclude
- Services/DocScanner.cs - optional IConfigService injection (default null -> new ConfigService); ScanDirectory filters by merged scan-exclude (DydoInternalScanExclude UNION user entries from dydo.json); GetScanExcludes loads config via basePath
- Rules/RuleBase.cs - added 'protected virtual bool ShouldSkip(DocFile) => false' (default false; rules opt-in in PR2)
- Commands/CheckCommand.cs - invokes CheckConfigValidator before doc validation; surfaces invariant errors to the top of the report
- Commands/FixCommand.cs - calls ConfigFactory.EnsureDefaultScanExclude(config) under FIXED block; reports 'Restored N scanExclude invariant(s) in dydo.json'
- Commands/TemplateCommand.cs - ExecuteUpdate calls new EnsureScanExcludeWithReport helper alongside existing EnsureDefaultNudges/EnsureDefaultQueues blocks (helper extracted to keep ExecuteUpdate's CC unchanged - see CRAP note below)

Source (new):
- Commands/CheckConfigValidator.cs - emits one error per missing entry from DydoInternalScanExclude
- Utils/RuleSkipPaths.cs - static helper IsTemplateOrAddition(normalizedRelativePath) for _system/templates/ and _system/template-additions/

Tests added (4 files, ~22 new tests):
- DynaDocs.Tests/Utils/RuleSkipPathsTests.cs (new) - 11 cases (true/false/case-insensitive)
- DynaDocs.Tests/Services/DocScannerTests.cs (new) - 5 cases (excludes .local/audit; doesn't exclude templates/template-additions; honors user entries; applies invariants even when config drops them)
- DynaDocs.Tests/Integration/ScanExcludeInvariantsTests.cs (new) - 3 cases (fresh init populates invariants; check errors on missing invariant; fix restores while preserving user entries)
- DynaDocs.Tests/Integration/TemplateCommandTests.cs (modified) - 3 new cases (template update restores missing invariant; no-op when invariants already present; --diff doesn't mutate)

Config:
- dydo.json - scanExclude block added (BC migration applied to this project via dydo fix)

VERIFICATION GATE - all green:
- dotnet build: 0 warnings, 0 errors
- python DynaDocs.Tests/coverage/run_tests.py: 4076/4076 pass (was 4073)
- python DynaDocs.Tests/coverage/gap_check.py: 139/139 modules pass tier requirements (T1)
- dydo check on dydo project: regression-free. Old binary baseline: 54 errors, 23 warnings, 894 files. New binary: 54 errors, 23 warnings, 893 files. The 1-file delta is _system/audit/_audit.md now excluded by the invariant (see surprise #1 below). All 54 errors and 23 warnings are pre-existing PR2/PR3 territory.
- scanExclude invariants check+fix loop manually verified on this project: removed scanExclude entries -> dydo check emits 'dydo.json scanExclude is missing required entry' for both invariants (exit code 2) -> dydo fix prints 'Restored 2 scanExclude invariant(s) in dydo.json' -> dydo check no longer reports the invariant errors.

DEVIATIONS FROM PLAN
None in scope. Per-rule moves of IsTemplateOrAddition deferred to PR2 as plan instructed (RuleBase.ShouldSkip virtual added but not yet consumed by any rule). Plan's invariant-validation 'TBD by code-writer' choice: I created Commands/CheckConfigValidator.cs to mirror the existing CheckAgentValidator/CheckDocValidator pattern in CheckCommand.

CRAP NOTE
First gap_check pass after my ExecuteUpdate edit failed Commands/TemplateCommand.cs at CRAP 33.8 (T1 limit 30) because adding an inline 'if (scanExcludeAdded > 0)' bumped ExecuteUpdate's CC by 1 over the file's already-borderline budget. Refactored to extract a small EnsureScanExcludeWithReport helper - same shape as EnsureDefaultNudges/EnsureDefaultQueues should arguably be (but aren't today; out of scope to refactor). Final gap_check: 139/139 pass.

SURPRISES FOR PR2/PR3
1. The plan asserts '_system/audit/' currently produces zero *.md (so excluding it is preventive). It actually does produce one: dydo/_system/audit/_audit.md (scaffolded by FolderScaffolder.GenerateAuditMetaMd at line 209). With PR1's invariant active, this file is now excluded from rule validation. It passed all rules vacuously today (it's well-formed framework-owned content), so no error/warning delta - but PR2/PR3 should decide: (a) refine the invariant to '_system/audit/<year>/' patterns, (b) accept that _audit.md is no longer subject to rule validation and document it. Lean (b) for simplicity but flagging it.

2. Hub format drift: running 'dydo fix' on this dydo project regenerates ~18 changelog _index.md files because committed _index.md uses KebabToTitleCase(filename) but current HubGenerator uses doc.Title which produces 'Task: <name>' prefix on changelog entries. Not a PR1 issue (pre-existing) but PR2's BC migration probe ('dydo template update' then 'dydo fix' on this project) will produce a noisy diff. I reverted these from PR1's footprint via 'git checkout HEAD --' since they're not in scope.

3. The plan's surface-surprise #1 (HubFilesRule needing project/tasks/ skip after D4) still holds - PR2 will need to add that skip when _index.md autogen for tasks/ stops.

4. Plan's surface-surprise #2 (three layered exclusion mechanisms: scan boundary, HubGenerator.IsExcludedPath, FixHubHandler.IsExcludedFolder) is now load-bearing in PR1's design. Each serves a different purpose. PR2/PR3 docs sweep should describe all three layers in reference/configuration.md.

DOC SCANNER SIGNATURE NOTE
Made the IConfigService param optional (default null -> new ConfigService()) following the existing SnapshotService pattern, so all 7 'new DocScanner(parser)' call sites continue to compile unchanged. CheckCommand/FixCommand use the explicit two-arg form for clarity in the FIXED-block helper.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-05 13:21
- Result: PASSED
- Notes: PASS on content. Code clean, ~22 new tests, dydo check baseline-equivalent (54e/23w/894f, no regression). Verification-gate caveat filed as issue #0165: gap_check exits 0 (coverage gate 139/139 green), but tests-under-coverage produce 3 unrelated failures (AgentRegistry concurrency + Console-capture cross-over between WorktreeCommand and AuditCompaction). None touch PR1 surface. run_tests.py is reproducibly 4076/4076 clean. Adele has full report; will flesh out #0165 body (reviewer cannot edit issue files).

Awaiting human approval.

## Approval

- Approved: 2026-05-06 17:47
