---
area: general
name: fix-base64-pathutils
status: human-reviewed
created: 2026-03-23T20:58:29.6219022Z
assigned: Dexter
updated: 2026-03-23T21:14:04.6428414Z
---

# Task: fix-base64-pathutils

Implemented both fixes from the inbox brief. (1) Added Base64DecodePipeExecRegex as a new dangerous pattern in BashCommandAnalyzer.cs that blocks base64 -d/--decode piped to interpreters (python, bash, sh, zsh, perl, ruby, node, pwsh, powershell). Updated existing warning test data to use non-pipe cases (so they still test the bypass warning), and added both positive and negative dangerous pattern tests. (2) Fixed stale XML doc on NormalizeWorktreePath in PathUtils.cs — changed '{mainRoot}/{projectContent}' to a prose description since mainRoot already includes the trailing slash. The 'dead mainRoot variable' from Grace's review is NOT dead in the current code (used at line 112) — appears to have been a false positive or was fixed in a prior commit. All 186 tests pass, coverage gate clean.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented both fixes from the inbox brief. (1) Added Base64DecodePipeExecRegex as a new dangerous pattern in BashCommandAnalyzer.cs that blocks base64 -d/--decode piped to interpreters (python, bash, sh, zsh, perl, ruby, node, pwsh, powershell). Updated existing warning test data to use non-pipe cases (so they still test the bypass warning), and added both positive and negative dangerous pattern tests. (2) Fixed stale XML doc on NormalizeWorktreePath in PathUtils.cs — changed '{mainRoot}/{projectContent}' to a prose description since mainRoot already includes the trailing slash. The 'dead mainRoot variable' from Grace's review is NOT dead in the current code (used at line 112) — appears to have been a false positive or was fixed in a prior commit. All 186 tests pass, coverage gate clean.

## Code Review

- Reviewed by: Frank
- Date: 2026-03-23 21:18
- Result: PASSED
- Notes: LGTM. Code is clean, tests pass. Regex is well-constructed with no ReDoS risk. Both fixes (base64 pipe-to-interpreter detection, XML doc correction) are correct and minimal. 3161 tests pass, coverage gate clean.

Awaiting human approval.