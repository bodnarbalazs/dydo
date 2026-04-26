---
id: 109
area: backend
type: issue
severity: medium
status: open
found-by: manual
date: 2026-04-26
---

# Reviewer cannot dispatch merge code-writer when dispatch chain originates from a released test-writer (or other ineligible role)

## Description

The reviewer role has a `dispatch-restriction` constraint that limits when a reviewer can dispatch a code-writer:

```json
{
  "type": "dispatch-restriction",
  "fromRole": null,
  "targetRole": "code-writer",
  "requiredRoles": ["code-writer", "inquisitor"],
  "requireAll": true,
  "onlyWhenDispatched": true,
  "message": "Reviewers can only dispatch a code-writer when dispatched by a code-writer or inquisitor. ..."
}
```

This is intended to keep reviewers from dispatching arbitrary code-writers — they should only dispatch a *merge* code-writer when their own dispatch chain came from a code-writer (the writer whose work they're reviewing) or inquisitor (deep-QA case).

The constraint as written checks the *immediate* dispatcher role. When the dispatch chain is `code-writer → test-writer → reviewer` — for example, an inquisitor that dispatches a test-writer to add coverage, who then dispatches a reviewer to verify, or a code-writer that dispatches a test-writer for a follow-up — the reviewer's `dispatchedByRole` is `test-writer`, which is **not** in the `requiredRoles` list. The reviewer therefore cannot dispatch the merge code-writer that the worktree merge flow expects.

If the test-writer (or whoever is in the middle of the chain) has already released by the time the reviewer reaches the merge step, there's no way for the chain to retroactively be "fixed" — the reviewer is stuck with the `.needs-merge` marker set and the only escape is for an orchestrator to dispatch the merge code-writer manually on the reviewer's behalf. The reviewer cannot release until the marker clears.

This was hit in this project today (2026-04-26): a reviewer reported "Blocked on: worktree merge dispatch. The reviewer role cannot dispatch a merge code-writer when the chain originates from a test-writer (Rose, already released). I sent a merge-needed message to Adele (orchestrator). Same state as Brian's A1. Once Adele dispatches the merge, my .needs-merge marker clears and I can release."

A downstream post-mortem (Adele/LC project, 2026-04-26 — saved at `dydo/agents/Brian/incoming-post-mortem-LC-2026-04-26.md`) corroborates that this isn't a one-off:

- **Four occurrences in a single session.** R1 reviewer, A1 reviewer, B-3 reviewer, C5 reviewer — all originated from test-writer dispatches by orchestrator Adele. Specific surviving evidence: Brian's reviewer message on `frontend-wave3-A1-scene-editor-trivial-bumps` (*"reviewer-dispatched-by-test-writer is blocked by policy. Please dispatch the merge code-writer."*) and Iris's reviewer message on `frontend-wave3-C5-trivial-bumps` (*"my reviewer role cannot dispatch a merge code-writer because the chain originated from a test-writer (Rose, since released)."*).
- **A v1.3.5 fix was applied earlier but did not fully resolve.** The downstream observer flagged this earlier as `dydo/project/issues/0001-reviewer-cannot-dispatch-merger-when-work-agent-is-a-test-writer.md` (downstream tracker, not this repo). The user reported a v1.3.5 fix went out for it. The recurrence pattern shows the fix covers one chain shape but not the `code-writer → test-writer → reviewer` shape (or covers it for some dispatch flag combinations but not the merge-queue dispatch specifically).
- **Cascades with #0111 (stale active-queue).** Each time this fires, the orchestrator dispatches the merger manually. If that manual merger dies for any reason while holding the merge queue's active slot, #0111 wedges every subsequent merger indefinitely. In the downstream session, this cascade fired three times.

## Reproduction

1. Dispatch chain: orchestrator → code-writer (in worktree) → test-writer (in same worktree) → reviewer.
2. Test-writer in step 2 releases after handing off to reviewer.
3. Reviewer completes review, gets a `.needs-merge` marker.
4. Reviewer attempts the standard merge-code-writer dispatch:
   `dydo dispatch --no-wait --auto-close --queue merge --role code-writer --task <task>-merge --brief "..."`
5. Dispatch is blocked by the `dispatch-restriction` constraint because `dispatchedByRole` is `test-writer`, not `code-writer` or `inquisitor`.
6. Reviewer cannot release while `.needs-merge` is set; reviewer is stuck.
7. Recovery: a human or orchestrator must dispatch the merge code-writer out-of-band, which clears the marker and lets the reviewer release. (Recovery may itself trigger #0111 if the manual merger dies.)

## Likely root cause

The `requiredRoles` list checks the immediate dispatcher only. It doesn't model the case where the chain is multi-hop and an intermediate role (test-writer) is the proximate dispatcher but the *originating* role (code-writer or inquisitor) is the one the constraint is really trying to authorize.

The deeper issue: the constraint gates on the **source** of the dispatch chain (chain-origin role) when the actual safety property worth protecting is about the **destination** of this dispatch (is it a merge code-writer or arbitrary work?). Source-based gating is brittle to chain-shape variation; destination-based gating is robust.

## Suggested fix

**Prefer option 3 (destination-based gating).** Allow reviewers to dispatch a code-writer with the `--queue merge` flag (or with a `.needs-merge` marker present in the reviewer's workspace) regardless of who dispatched the reviewer. This decouples the merge handoff from the chain — which is the part that's brittle. The "no arbitrary code-writer dispatch" intent is preserved by gating on the merge condition.

The downstream post-mortem makes the same recommendation: *"gate reviewer→code-writer dispatches by the `--queue merge` flag (or an equivalent merge-only dispatch type) and allow them universally regardless of chain root. The constraint that matters is 'this is a merge code-writer, not arbitrary implementation work.'"*

Fallback option: add `test-writer` to `requiredRoles` for the existing constraint. Simpler change but doesn't generalize — the next chain shape that doesn't include code-writer/inquisitor/test-writer in the immediate-dispatcher slot will hit the same wall.

Add a regression test covering the chain `code-writer → test-writer → reviewer → merge code-writer dispatch (with --queue merge)` to assert it succeeds without orchestrator intervention. Add another covering `inquisitor → test-writer → reviewer → merge code-writer dispatch` (the "deep-QA" case the original constraint was trying to permit).

## Impact

- Workflow stall: reviewer is unrelease-able until an external orchestrator/human intervenes. Disrupts the autonomous merge flow that worktree dispatch is designed to enable.
- Reported pattern is "same state as Brian's A1" suggesting this isn't the first occurrence; agents are being trained to message orchestrators when they hit it.
- The error path is recovery-via-out-of-band-message — easy to miss, easy to leave a worktree in `.needs-merge` limbo.
- Cascades with #0111. Each manual merger the orchestrator dispatches is a fresh chance for the merge queue to wedge if that merger dies. In the post-mortem's session, this cascade fired three times in hours.
- Persistent across versions. The v1.3.5 fix did not fully resolve; the bug shape just shifts to different chain configurations.

## Related context

- `dydo/_system/roles/reviewer.role.json` — current reviewer constraints (`fromRole=code-writer` only, no destination-based gating).
- `dydo/_system/roles/test-writer.role.json` — relevant if option 1 (chain-walking) is pursued.
- `dydo/guides/how-to-merge-worktrees.md` — describes the reviewer-dispatches-merger flow but doesn't cover this failure mode.
- `Services/RoleConstraintEvaluator.cs` — the evaluator that resolves the `dispatchedByRole` lookup; relevant for option 1, and the location of the new destination-flag check for option 3.
- `dydo/agents/Brian/incoming-post-mortem-LC-2026-04-26.md` — full downstream post-mortem; documents the four occurrences and the v1.3.5-fix-not-fully-effective evidence.
- Issue #0111 — the cascade partner (stale active-queue marker). #0109 + #0111 together produce indefinite agent stalls.
- Downstream issue tracker reference: `dydo/project/issues/0001-reviewer-cannot-dispatch-merger-when-work-agent-is-a-test-writer.md` (in the LC project, not this repo) — the original report and v1.3.5 partial-fix history.
- This conversation: 2026-04-26 reviewer report stating "blocked on worktree merge dispatch ... once Adele dispatches the merge, my .needs-merge marker clears and I can release."

## Resolution

(Filled when resolved)
