---
id: 163
area: project
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-04
---

# DocScanner recurses into _system/.local/worktrees/ - dydo check reports tens of spurious errors in projects with live worktrees

## Description

Services/DocScanner.cs:14-25 - Directory.GetFiles(path, "*.md", SearchOption.AllDirectories) with no exclusion list. Walks every subdirectory under the dydo root. dydo/_system/.local/worktrees/<id>/ is the live worktree storage (per understand/architecture.md Worktree Dispatch section, step 2). Each entry is a full project clone - *.md files everywhere including src/.../README.md, tests/README.md, etc., none of which are real docs of the host project.\n\nLive reproduction at C:\Users\User\Desktop\LC: dydo check produces ~12 spurious errors from one worktree alone (frontend-slice-05-scene-editor-structure), scaling linearly with worktree count. Files like _system/.local/worktrees/.../src/microservices/asset_processing/README.md get flagged for naming (asset_processing not kebab-case), missing frontmatter, and broken links (worktree is on a different branch with different relative paths). Total 38 errors, 6 warnings at LC.\n\nFrom inside a worktree (current run from dydo-check-drift), _system/.local/worktrees/ is empty and the gap is invisible. From the main project root, it isn't.\n\nServices/HubGenerator.IsExcludedPath:279-289 already excludes _system/, agents/, and dotfile dirs for hub generation. Commands/FixHubHandler.IsExcludedFolder:134-146 excludes _system, agents, _assets. Three independent exclusion lists, all slightly different, none applied to the scan stage feeding the rule pipeline.\n\nFix: add a single, central exclusion check at the scan boundary. In Services/DocScanner.ScanDirectory, after GetFiles, filter out paths whose normalized relative path starts with _system/.local/. Optional but recommended: also filter _system/audit/ defensively (only contains JSON today).\n\nVerify _system/templates/ and _system/template-additions/ STAY scanned so per-rule logical skips can keep firing (they contain real source-of-truth template content that other parts of the system surface).\n\nOpen questions for planner: (a) value in a dydo.json configurable scan-exclude list (so projects can add node_modules, target, etc.)? (b) sanity-stop at .git on Unix?\n\nThis issue is the highest-priority of the batch - it materially blocks LC from running clean. Recommended PR1 in the fix order; clearing this first makes verifying issues for findings #1/#2/#4 mechanical instead of manual.\n\nConfirmed by inquisition dydo-check-drift.md finding #5 (judge: Dexter).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)