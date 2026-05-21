---
id: 185
area: backend
type: issue
severity: high
status: open
found-by: inquisition
date: 2026-05-19
---

# dydo check <subfolder> shrinks allDocs to subfolder, every cross-folder link reports as broken

Running dydo check on a subfolder treats that subfolder as both scan scope and resolver basePath, so any link whose target lives outside the subfolder is reported as a broken link even though it exists on disk.

## Description

**Root cause of issue #0184.**

When dydo check <path> is invoked, CheckCommand.cs:122-132 returns Path.GetFullPath(path) as basePath. CheckDocValidator.cs:20 then calls scanner.ScanDirectory(basePath) which (per DocScanner.cs:20) recursively scans only that subfolder. LinkResolver.cs:17-20 declares a link broken whenever the resolved target file is not present in the narrowed allDocs list — even though PathUtils.ResolvePath correctly produces an on-disk absolute path. There is no fallback to File.Exists, no second pass over the full docs tree to populate the link-target universe, and no warning that the scope is narrower than the resolver requires.

dydo graph is not subject to this because GraphCommand.cs:49 unconditionally uses PathUtils.FindDocsFolder(Environment.CurrentDirectory) as basePath regardless of the file argument.

**Live reproductions (this worktree):**

| Scan target | Broken-link false positives |
|---|---|
| dydo (whole tree) | 0 |
| dydo/project/decisions | 5 |
| dydo/guides | 29 |
| dydo/reference | 27 |

All five false-positive targets in decisions were verified to exist on disk via Test-Path.

**Reproducer evidence:** dydo/agents/Frank/repro/ (preserve via merge or copy when fixing — worktree is temporary).

**Fix shapes to consider:**

1. Always scan from the docs root and treat the user-supplied subfolder as a *filter* on which docs to validate (preserves whole-tree allDocs).
2. Keep subfolder-scoped scanning but fall back to File.Exists for .md targets that miss the allDocs lookup.
3. Reject dydo check <subfolder> invocations with a clear error message instructing the user to run from the docs root.

The fix should ship alongside a regression test CheckCommandTests.Check_Subfolder_AcceptsLinksToTargetsOutsideSubfolder (named in finding 4 of the inquisition report).

**Reference:** dydo/project/inquisitions/link-validator-resolver-divergence.md finding 1.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)