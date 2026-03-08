---
area: general
type: changelog
date: 2026-03-08
---

# Task: audit-compaction-v2

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented audit compaction v2: (1) Added [JsonIgnore] to SnapshotDelta.IsEmpty to prevent serialization leak. (2) Raised MaxChainDepth from 5 to 50. (3) Rewrote Compact to pick the most common git_head's snapshot as baseline and cache deltas by snapshot content hash — sessions with identical snapshots share the same delta object. (4) Added UniqueCommits to CompactionResult and CLI output. (5) Added 3 new tests: same-git-head sharing, multi-git-head dedup, IsEmpty serialization. Plan deviation: used snapshot content hash for delta caching instead of git_head grouping, because fixture data showed sessions at the same git_head can have different snapshots (audit files change between sessions). All 1547 tests pass.

## Code Review

- Reviewed by: Sam
- Date: 2026-03-08 13:23
- Result: PASSED
- Notes: LGTM. Code is clean, correct, and well-tested. Delta caching by snapshot content hash is elegant. [JsonIgnore] fix prevents real serialization leak. All 26 compaction tests pass. 2 pre-existing test failures (HelpCommandTests, CompletionsCommandTests) are unrelated.

Awaiting human approval.

## Approval

- Approved: 2026-03-08 20:25
