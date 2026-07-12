---
title: 0285 follow-up: add persisted-PATH WindowsApps-alias rejection test (common wild case, guard currently untested)
id: 289
area: backend
type: issue
severity: low
status: open
found-by: review
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-12
---

# 0285 follow-up: add persisted-PATH WindowsApps-alias rejection test (common wild case, guard currently untested)

0285 correctly rejects a codex WindowsApps alias found on the persisted User/Machine PATH, but no test pins it; since the User PATH always contains WindowsApps, dropping that guard would silently re-resurrect 0227 and pass the suite.

## Description

Follow-up to resolved #0285 (landed 2ad3d325). The 0285 review PASSED the fix as correct but flagged one strongly-recommended missing test: no test exercises the WindowsApps-alias rejection on a PERSISTED PATH source (User/Machine), only on the live PATH.

Why it matters: the default Windows User PATH ALWAYS contains `%LOCALAPPDATA%\Microsoft\WindowsApps`, so "persisted-scan meets the codex WindowsApps alias" is the common real-world case for exactly this fix. The code correctly rejects it today (IsRejectedWindowsCodexAlias is applied to User/Machine candidates), but that guard is unprotected by any test — dropping it from the fallback scan would pass the entire suite and silently re-resurrect #0227/#0285 through the new fallback.

Fix: add one TerminalLauncher test using the `PersistedPathProviderOverride` seam to return a temp `...\WindowsApps` dir containing a `codex.exe`, and assert that the alias is rejected (install-dir probe or the alias-specific throw wins, NOT the aliased path returned). Optionally pin User-vs-Machine and persisted-vs-install relative order (each currently passes with the other source nulled). Test-only, no production change. Low severity (regression insurance on a correct guard).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)