---
area: general
type: changelog
date: 2026-04-02
---

# Task: fix-template-sync

Fixed 3 template sync issues: (1) Regenerated guides/how-to-merge-worktrees.md and guides/how-to-review-worktree-merges.md by deleting and recreating to resolve encoding-caused hash mismatches (line endings only, git-normalized to no diff). (2) Updated Templates/Assets/dydo-diagram.svg with current on-disk version containing correct role names (inquisitor, judge instead of stale interviewer, tester), rebuilt binary, and ran template update to recalculate hashes. (3) dydo template update now produces zero warnings. Had to rebuild and reinstall the global dydo tool (1.2.53 -> 1.2.54) since the embedded SVG resource needed updating.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed 3 template sync issues: (1) Regenerated guides/how-to-merge-worktrees.md and guides/how-to-review-worktree-merges.md by deleting and recreating to resolve encoding-caused hash mismatches (line endings only, git-normalized to no diff). (2) Updated Templates/Assets/dydo-diagram.svg with current on-disk version containing correct role names (inquisitor, judge instead of stale interviewer, tester), rebuilt binary, and ran template update to recalculate hashes. (3) dydo template update now produces zero warnings. Had to rebuild and reinstall the global dydo tool (1.2.53 -> 1.2.54) since the embedded SVG resource needed updating.

## Code Review

- Reviewed by: Brian
- Date: 2026-04-02 14:35
- Result: PASSED
- Notes: LGTM. Normalization logic is correct and minimal: NormalizeForHash strips BOM + CRLF, ComputeHash normalizes before hashing, MigrateHashFormat is idempotent and safe. Fallback to true on missing stored hash is the right conservative choice. 6 new tests cover all edge cases. SVG updated with correct role names. Template sync clean (zero warnings). One pre-existing flaky test (QueueServiceTests.FindStaleActiveEntries_IgnoresRunningPid) unrelated to these changes.

Awaiting human approval.

## Approval

- Approved: 2026-04-02 18:56
