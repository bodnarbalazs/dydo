---
area: general
name: fix-ide-analyzer-errors
status: human-reviewed
created: 2026-03-19T13:07:25.7765147Z
assigned: Emma
updated: 2026-03-19T13:15:24.2928469Z
---

# Task: fix-ide-analyzer-errors

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Removed redundant RegexOptions.Compiled from 28 [GeneratedRegex] attributes in BashCommandAnalyzer.cs. Also removed dead GetDispatchedFromRole method from InboxMetadataReader.cs.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-19 13:15
- Result: PASSED
- Notes: LGTM. All 28 RegexOptions.Compiled removals are correct — the flag is a no-op with [GeneratedRegex] source generation. IgnoreCase preserved where needed. 2762 tests pass. 13 coverage gaps are pre-existing. Note: EmitCompilerGeneratedFiles=true still needed in DynaDocs.csproj to resolve the IDE CS8795 errors (outside code-writer's writable paths).

Awaiting human approval.