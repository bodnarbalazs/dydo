---
name: self-improvement
description: The lever is the harness, not the run in front of you. Use when the same friction, correction, or workaround returns a second time; when a mistake could have been caught by a check that does not exist; when a run burns its budget finding what it should have been handed.
---

<!-- Adapted from mattpocock/skills retro at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Self-Improvement

Turn friction that keeps returning into one small, authorized, testable change to the harness: prompts,
skills, guides, nudges, hooks, checks, and the code behind them. Product behaviour changes through its
own Issue. Cross-cutting — any hat reaches for this mid-run, then returns to the work it was doing.

## Threshold

Twice. The same failure, correction, workaround, or avoidable friction appears at least twice in
evidence you can cite, or a reviewed record already establishes the recurrence. One bad run is a story;
the second occurrence is the pattern that earns a change.

## Method

1. **Establish the recurrence.** Read the primary sources — session logs, Issue comments, review
   blocks, the diff — and name the repeated symptom, its occurrences, the work it costs, and the likely
   cause. Done when two occurrences are cited by location.
2. **Deduplicate.** Search the Issues, Decisions, guides, pitfalls, prompts, skills, nudges and hooks
   that already speak to this cause. The existing canonical surface wins; a second one splits it.
3. **Choose one lever.** Scan the lenses for the surface that reaches the cause, then take the smallest
   durable change on it: wording first, then a warn-level nudge, then a hook where behaviour must change
   at action time, then harness code when no earlier layer can express it. When the lever is a prompt
   file, write it under `writing-for-agents`.
4. **Check authority.** Edit where the current Issue, hat and reviewed plan already name the
   destination. Anywhere else, return the evidence and the one recommendation, and build nothing.
5. **State proof and rollback.** One sentence for the recurrence that should stop and the observation
   that would show it stopped; one sentence for removing the change cleanly if it turns into noise.

## Lenses

Where the lever usually sits:

- **Navigation**: how easy was it to find the right files? Are there hidden dependencies between them?
  Would a navigation pointer make it easier? _Use when_ the session took a long time to find a piece of
  information.
- **Automated checks**: is there a check that could catch the error the agent made — linting, typing,
  tests, a documentation check? _Use when_ the agent made a mistake that could have been caught by an
  automated check.
- **Coding standards**: should the review rubric be given a new rule to enforce, or an existing rule
  removed or clarified? _Use when_ the review failed to catch a mistake.
- **Entry point size**: which steering instructions belong in a guide or an automated check instead?
  _Use when_ the always-loaded entry point — in the repo or the human's global scope — is particularly
  large.
- **Tool economy**: did the agent make expensive tool calls that could be streamlined, or is some custom
  tooling token-inefficient? _Use when_ the agent made an expensive tool call.
- **No-ops**: instructions in prompt files that do not modify behaviour against the model's default.
  _Use when_ the prompt files are large and unwieldy.
- **Information access**: more access for the agent — teed logs, read-only reach into a third-party
  service. _Use when_ a crucial piece of information was not available to the agent.

## Boundaries

- One change for one recurring pattern; the Issue in flight keeps its scope, and the next pattern waits.
- Enforcement scales with evidence: guidance and warn-level nudges carry most patterns, and a blocking
  rule earns its place after the warn level has been seen to fail.
- Durable knowledge lands in the narrowest dydo document, live work in Linear.

## Return

Candidates in severity order, each one line: recurrence with its two occurrences → lever → proof →
rollback. The session that reached for this method spends the top candidate itself or carries it to the
human as one question; when no small credible change survives the checks, the pattern is the return.
