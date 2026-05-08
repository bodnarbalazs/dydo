---
area: general
type: changelog
date: 2026-05-08
---

# Task: fix-pr2-xmldoc

Re-review of commit 87d9f6f (xmldoc fix on RegisterMainAnchor in Services/WatchdogService.cs:189-204).

WHAT TO REVIEW
Doc-only change. Original (pre-PR2) xmldoc claimed 'Both EnsureRunning() and the agent-claim site... route through here so the main-vs-worktree resolution rule is enforced by construction.' Charlie flagged this as V3 of his PR2 review (review-pr2-worktree-anchor-v2): false. EnsureRunning at lines 97-116 inlines PathUtils.FindMainDydoRoot() and calls RegisterAnchor directly — never RegisterMainAnchor. 'Enforced by construction' was fiction.

87d9f6f rephrases to describe two parallel sites (RegisterMainAnchor for agent-claim, EnsureRunning's inline path) that each uphold the main-vs-worktree rule independently, with an explicit warning that any new anchor-registration site MUST resolve the main dydo root the same way.

Behavior: zero change. RegisterMainAnchor remains agent-claim-only. EnsureRunning still uses its FindMainDydoRoot + RegisterAnchor inline path.

WHY YOU
Adele's PR2 brief named you as the preferred reviewer for any follow-up — you reviewed PR1 (e80730c), have prior context, and Charlie reviewed v2 so he is conflicted on this re-review.

WORKFLOW CONTEXT
Same-agent-review-then-fix: Charlie reviewed PR2 (de50134), found V3, then self-dispatched as code-writer and shipped 87d9f6f himself. Adele dispatched me (Brian) in parallel as code-writer; race condition — Charlie won. My working tree edit was byte-for-byte identical to 87d9f6f (two independent code-writers converged on the same wording from Charlie's prescriptive remediation note in his review verdict). Strong prior signal but does NOT replace the review — that is what you are providing.

VERIFY
- V1 file:line accuracy of the new wording vs the actual call paths (EnsureRunning :97-116 + :116; RegisterMainAnchor :200-205; RegisterAnchor :209-221).
- V2 doc claim is now accurate and the future-maintainer hazard from the original phrasing is closed.
- V3 no behavior change introduced.
- G1 dotnet build clean.
- G2 dydo check — pre-existing 4 orphan-doc warnings carry over from PR2 baseline; 0 errors expected.
- G3 run_tests.py — 4165/4165 baseline (I ran on identical content: 4165/4165 pass, 7m21s).
- G4 gap_check.py --force-run — 140/140 modules (I ran: 140/140 pass).
- G5 CI on master post-87d9f6f.

REPORT BACK on task fix-pr2-xmldoc with verdict + reasoning. After approval, both Brian and Charlie can release.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Re-review of commit 87d9f6f (xmldoc fix on RegisterMainAnchor in Services/WatchdogService.cs:189-204).

WHAT TO REVIEW
Doc-only change. Original (pre-PR2) xmldoc claimed 'Both EnsureRunning() and the agent-claim site... route through here so the main-vs-worktree resolution rule is enforced by construction.' Charlie flagged this as V3 of his PR2 review (review-pr2-worktree-anchor-v2): false. EnsureRunning at lines 97-116 inlines PathUtils.FindMainDydoRoot() and calls RegisterAnchor directly — never RegisterMainAnchor. 'Enforced by construction' was fiction.

87d9f6f rephrases to describe two parallel sites (RegisterMainAnchor for agent-claim, EnsureRunning's inline path) that each uphold the main-vs-worktree rule independently, with an explicit warning that any new anchor-registration site MUST resolve the main dydo root the same way.

Behavior: zero change. RegisterMainAnchor remains agent-claim-only. EnsureRunning still uses its FindMainDydoRoot + RegisterAnchor inline path.

WHY YOU
Adele's PR2 brief named you as the preferred reviewer for any follow-up — you reviewed PR1 (e80730c), have prior context, and Charlie reviewed v2 so he is conflicted on this re-review.

WORKFLOW CONTEXT
Same-agent-review-then-fix: Charlie reviewed PR2 (de50134), found V3, then self-dispatched as code-writer and shipped 87d9f6f himself. Adele dispatched me (Brian) in parallel as code-writer; race condition — Charlie won. My working tree edit was byte-for-byte identical to 87d9f6f (two independent code-writers converged on the same wording from Charlie's prescriptive remediation note in his review verdict). Strong prior signal but does NOT replace the review — that is what you are providing.

VERIFY
- V1 file:line accuracy of the new wording vs the actual call paths (EnsureRunning :97-116 + :116; RegisterMainAnchor :200-205; RegisterAnchor :209-221).
- V2 doc claim is now accurate and the future-maintainer hazard from the original phrasing is closed.
- V3 no behavior change introduced.
- G1 dotnet build clean.
- G2 dydo check — pre-existing 4 orphan-doc warnings carry over from PR2 baseline; 0 errors expected.
- G3 run_tests.py — 4165/4165 baseline (I ran on identical content: 4165/4165 pass, 7m21s).
- G4 gap_check.py --force-run — 140/140 modules (I ran: 140/140 pass).
- G5 CI on master post-87d9f6f.

REPORT BACK on task fix-pr2-xmldoc with verdict + reasoning. After approval, both Brian and Charlie can release.

## Code Review

- Reviewed by: Dexter
- Date: 2026-05-07 18:27
- Result: PASSED
- Notes: PASS. V3 finding from review-pr2-worktree-anchor-v2 closed. New xmldoc accurately describes the two parallel anchor-registration paths (RegisterMainAnchor at WatchdogService.cs:205-210 for agent-claim, EnsureRunning at :97-102/:116 inline) and replaces the false 'enforced by construction' claim with the correct 'upheld at each call site independently' framing plus an explicit MUST-rule for new sites. Zero behavior change (diff is xmldoc-only, +11/-6). All gates: dotnet build 0/0; dydo check 0 errors / 4 orphan-doc warnings (PR2 baseline carry-over); run_tests 4165/4165 in 4m10s; gap_check 140/140 modules; CI run 25511144873 success. Out-of-scope follow-up flagged in workspace notes: EnsureRunning's '?? "."' fallback at :100 (Charlie's option (a) — known, pre-existing, not a regression).

Awaiting human approval.

## Approval

- Approved: 2026-05-08 12:36
