---
area: general
type: changelog
date: 2026-05-04
---

# Task: powershell-cd-chain

Review extension of cd-chain coaching guard to PowerShell forms (from Adele's brief at dydo/agents/Adele/brief-powershell-cd-chain.md).

## Changes

1. **Services/BashCommandAnalyzer.cs:292** — extended `CdThenCommandRegex` leading-verb alternation from just `cd` to `(?:cd|Set-Location|sl|chdir|Push-Location|pushd)`. Existing capture-group numbering preserved (1=double-quoted, 2=single-quoted, 3=unquoted, 4=rest cmd) so `DetectNeedlessCd` body is unchanged.

2. **Commands/GuardCommand.cs:520-522** — block message reworded to `Don't chain cd / Set-Location with other commands` and `run it separately first` (was `run cd separately first`).

3. **DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs** — added 3 new theories in the `DetectNeedlessCd` region:
   - `PowerShellForms_ReturnsMatch` (7 cases): Set-Location / set-location / sl / chdir / Push-Location / pushd, mix of `&&` and `;`, case-insensitive form.
   - `PowerShellQuotedPath_ReturnsMatch` (3 cases): single-quoted, double-quoted, lowercase variant.
   - `PowerShellNonLeadingOrAlone_NoMatch` (4 cases): `Set-LocationWeirdSuffix` boundary, `slbang` boundary, right-side chain (`git status; Set-Location /tmp`), single command without chain.

4. **DynaDocs.Tests/Integration/GuardIntegrationTests.cs:693** — updated `AssertStderrContains` to match the new message text.

## Key decisions

- **Push-Location / pushd included** (per Adele's recommendation): if an agent wants save/restore semantics, they should run two separate calls. Same auto-approve breakage applies to Push-Location chains as to Set-Location.
- **Boundary safety**: the existing `\s+` after the verb prevents `Set-LocationWeirdSuffix` / `slbang` false matches without needing explicit `\b`. Verified by the negative theory.
- **Out of scope but worth flagging**: regex still doesn't handle named-parameter forms like `Set-Location -Path C:\foo; cmd` or `cd /d C:\foo; cmd` — the same gap already existed for `cd`. Not addressed; Adele asked for the smallest change.

## Nudge investigation finding (brief item 4)

The message `Compound command contains cd with path operation - manual approval required to prevent path resolution bypass` is **not** in dydo's codebase. Greps across `Services`, `Commands`, `dydo.json`, `ConfigFactory.DefaultNudges`, `Templates`, and the rest of the repo all return zero hits for that phrase, for `Compound command contains cd`, for `manual approval required`, and for `path resolution bypass`.

That nudge originates from **Claude Code itself** (built-in upstream coaching), not from dydo. So:
- There is no duplication inside dydo to remove.
- There is no dydo-side pattern that needs a PowerShell update.
- The dydo block (`DetectNeedlessCd` → guard block message) and Claude Code's own coaching are independent systems firing on the same input. Nothing to delete or edit on the dydo side.

## Verification

- `python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~BashCommandAnalyzerTests` → 217 passed, 0 failed.
- `python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~CdGitCompound|FullyQualifiedName~CdNonGitCompound` → 2 passed (integration message assertion verified).
- `python DynaDocs.Tests/coverage/gap_check.py` → 4054/4054 tests passed, 137/137 modules pass tier requirements (exit 0).

No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review extension of cd-chain coaching guard to PowerShell forms (from Adele's brief at dydo/agents/Adele/brief-powershell-cd-chain.md).

## Changes

1. **Services/BashCommandAnalyzer.cs:292** — extended `CdThenCommandRegex` leading-verb alternation from just `cd` to `(?:cd|Set-Location|sl|chdir|Push-Location|pushd)`. Existing capture-group numbering preserved (1=double-quoted, 2=single-quoted, 3=unquoted, 4=rest cmd) so `DetectNeedlessCd` body is unchanged.

2. **Commands/GuardCommand.cs:520-522** — block message reworded to `Don't chain cd / Set-Location with other commands` and `run it separately first` (was `run cd separately first`).

3. **DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs** — added 3 new theories in the `DetectNeedlessCd` region:
   - `PowerShellForms_ReturnsMatch` (7 cases): Set-Location / set-location / sl / chdir / Push-Location / pushd, mix of `&&` and `;`, case-insensitive form.
   - `PowerShellQuotedPath_ReturnsMatch` (3 cases): single-quoted, double-quoted, lowercase variant.
   - `PowerShellNonLeadingOrAlone_NoMatch` (4 cases): `Set-LocationWeirdSuffix` boundary, `slbang` boundary, right-side chain (`git status; Set-Location /tmp`), single command without chain.

4. **DynaDocs.Tests/Integration/GuardIntegrationTests.cs:693** — updated `AssertStderrContains` to match the new message text.

## Key decisions

- **Push-Location / pushd included** (per Adele's recommendation): if an agent wants save/restore semantics, they should run two separate calls. Same auto-approve breakage applies to Push-Location chains as to Set-Location.
- **Boundary safety**: the existing `\s+` after the verb prevents `Set-LocationWeirdSuffix` / `slbang` false matches without needing explicit `\b`. Verified by the negative theory.
- **Out of scope but worth flagging**: regex still doesn't handle named-parameter forms like `Set-Location -Path C:\foo; cmd` or `cd /d C:\foo; cmd` — the same gap already existed for `cd`. Not addressed; Adele asked for the smallest change.

## Nudge investigation finding (brief item 4)

The message `Compound command contains cd with path operation - manual approval required to prevent path resolution bypass` is **not** in dydo's codebase. Greps across `Services`, `Commands`, `dydo.json`, `ConfigFactory.DefaultNudges`, `Templates`, and the rest of the repo all return zero hits for that phrase, for `Compound command contains cd`, for `manual approval required`, and for `path resolution bypass`.

That nudge originates from **Claude Code itself** (built-in upstream coaching), not from dydo. So:
- There is no duplication inside dydo to remove.
- There is no dydo-side pattern that needs a PowerShell update.
- The dydo block (`DetectNeedlessCd` → guard block message) and Claude Code's own coaching are independent systems firing on the same input. Nothing to delete or edit on the dydo side.

## Verification

- `python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~BashCommandAnalyzerTests` → 217 passed, 0 failed.
- `python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~CdGitCompound|FullyQualifiedName~CdNonGitCompound` → 2 passed (integration message assertion verified).
- `python DynaDocs.Tests/coverage/gap_check.py` → 4054/4054 tests passed, 137/137 modules pass tier requirements (exit 0).

No plan deviations.

## Code Review

- Reviewed by: Charlie
- Date: 2026-05-04 18:54
- Result: PASSED
- Notes: PASS. Regex extension surgical and correct ( added to leading-verb alternation, capture groups preserved). Tests cover 7 positive PS forms, 3 quoted-path cases, and 4 boundary negatives. 217/217 unit + 2/2 integration cd-compound tests green; gap_check 137/137 (exit 0). dydo check errors are all pre-existing (issues 0159-0163). Note for human: working tree also contains unrelated dirty changes (Commands/CheckAgentValidator.cs 24h->48h, csproj+npm version 1.4.4->1.4.5) NOT from Brian's session and NOT part of this review.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:52
