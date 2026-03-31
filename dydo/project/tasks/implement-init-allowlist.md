---
area: general
name: implement-init-allowlist
status: human-reviewed
created: 2026-03-31T13:42:48.3239942Z
assigned: Grace
updated: 2026-03-31T14:06:31.3297229Z
---

# Task: implement-init-allowlist

Implemented Bash(dydo:*) wildcard entry in permissions.allow during dydo init claude. The ConfigureClaudeHooks method was split into ConfigureGuardHook and ConfigureAllowList for clarity. Allowlist merges with existing entries (no duplicates, no overwrites). template update does not need refresh logic since the wildcard already covers all current and future dydo subcommands. 6 new tests cover: entry creation, merge with existing entries, no-duplicate on rejoin, creation when permissions section is missing, and no-op for init none.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented Bash(dydo:*) wildcard entry in permissions.allow during dydo init claude. The ConfigureClaudeHooks method was split into ConfigureGuardHook and ConfigureAllowList for clarity. Allowlist merges with existing entries (no duplicates, no overwrites). template update does not need refresh logic since the wildcard already covers all current and future dydo subcommands. 6 new tests cover: entry creation, merge with existing entries, no-duplicate on rejoin, creation when permissions section is missing, and no-op for init none.

## Code Review

- Reviewed by: Henry
- Date: 2026-03-31 14:16
- Result: PASSED
- Notes: LGTM. Code is clean, tests pass. ConfigureClaudeHooks split is well-structured. Allowlist merging is idempotent with proper null handling. 5 meaningful tests cover all edge cases. gap_check passes (132/132 modules). Pre-existing test failure (dydo completions missing from about-dynadocs.md quick reference) is unrelated.

Awaiting human approval.