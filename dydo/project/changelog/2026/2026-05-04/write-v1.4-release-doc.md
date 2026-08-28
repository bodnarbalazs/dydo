---
area: general
type: changelog
date: 2026-05-04
---

# Task: write-v1.4-release-doc

Review commit 2054f5d for write-v1.4-release-doc. Brief: dydo/agents/Grace/inbox/a6ef87ba-write-v1.4-release-doc.md. Verify: format mirrors [dydo/project/v1.3-release.md](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/v1.3-release.md), content aligns with git log v1.3.9..HEAD (10 commits — actual scope is much smaller than Brian's earlier draft), no overclaiming (Brian's draft attributed several v1.3.9-shipped fixes to v1.4 — those were excluded; commits 8d3e3b1, 762eeda, 3532bd9, 4dd5d03, e1eac2e, eeaf93f all live in v1.3.9 per 'git tag --contains'), succinct. One known limitation: doc is currently orphaned (not linked from project/_index.md) because Brian's brief said 'Don't touch any other doc' — I left it as-is and flagged this to Brian. Approve or reject. Reply to Brian on this task.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review commit 2054f5d for write-v1.4-release-doc. Brief: dydo/agents/Grace/inbox/a6ef87ba-write-v1.4-release-doc.md. Verify: format mirrors [dydo/project/v1.3-release.md](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/v1.3-release.md), content aligns with git log v1.3.9..HEAD (10 commits — actual scope is much smaller than Brian's earlier draft), no overclaiming (Brian's draft attributed several v1.3.9-shipped fixes to v1.4 — those were excluded; commits 8d3e3b1, 762eeda, 3532bd9, 4dd5d03, e1eac2e, eeaf93f all live in v1.3.9 per 'git tag --contains'), succinct. One known limitation: doc is currently orphaned (not linked from project/_index.md) because Brian's brief said 'Don't touch any other doc' — I left it as-is and flagged this to Brian. Approve or reject. Reply to Brian on this task.

## Code Review

- Reviewed by: Adele
- Date: 2026-05-01 10:27
- Result: PASSED
- Notes: Doc is accurate, surgical, and on-scope. Format mirrors v1.3-release.md. Verified the 6 flagged commits (8d3e3b1, 762eeda, 3532bd9, 4dd5d03, e1eac2e, eeaf93f) are all v1.3.9 per 'git tag --contains' and correctly excluded. Highlights and bug fixes ([#0134](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0134-auto-close-mechanism-broken-releaseagent-clears-auto-close-prematurely-v1-3-9-re.md), [#0138](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0138-auto-resume-launches-terminal-in-user-home-dir-not-project-root.md), [#0141](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0141-wait-guard-deadlock-dydo-wait-auto-exits-on-already-unread-inbox-state-guard-the.md)) faithfully match the actual v1.4 work; Decisions 021 and 022 exist and align with doc claims. gap_check green: 4007/4007 tests, 137/137 modules at tier, exit 0. Known limitation (orphan from project/_index.md) is acknowledged and is a 'dydo fix' housekeeping follow-up, not a content defect.

Awaiting human approval.

## Approval

- Approved: 2026-05-04 21:52
