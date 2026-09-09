---
name: self-improvement
description: Kaizen for the harness. Use when the same friction, correction, or workaround returns a second time; when a mistake could have been caught by a check that does not exist; when a run burns its budget finding what it should have been handed.
emit: skill
invocation: automatic
---

<!-- Lenses adapted from mattpocock/skills retro at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Self-Improvement

**Kaizen**: turn friction that keeps returning into one small, incremental, testable improvement to
the harness — prompts, skills, guides, nudges, hooks, checks, and the code behind them — and suggest
it rather than ship it. Product behaviour changes through its own Issue.

## Boundary

The chief-of-staff reaches for this when the board shows the same friction across sessions; any hat
may reach for it mid-run, then return to its work. The output is a suggestion. The human decides: an
accepted change becomes an Issue, or lands directly only when it is tiny and inside the scope the
current Issue already grants.

## Threshold

Twice. The same failure, correction, workaround, or avoidable friction appears at least twice in
evidence you can cite, or a reviewed record already establishes the recurrence. One bad run is a
story; the second occurrence is the pattern that earns a change.

## Method

1. **Establish the recurrence.** Read the primary sources — session logs, Issue comments, review
   blocks, the diff — and name the repeated symptom, its occurrences, the work it costs, and the
   likely cause. Done when two occurrences are cited by location.
2. **Deduplicate.** Search the Issues, Decision Records, guides, pitfalls, prompts, skills, nudges and
   hooks that already speak to this cause. The existing canonical surface wins; a second one splits it.
3. **Choose one lever.** Scan the lenses for the surface that reaches the cause, then take the smallest
   durable change on it: wording first, then a warn-level nudge, then a hook where behaviour must
   change at action time, then harness code when no earlier layer can express it. A blocking rule
   earns its place only after the warn level has been seen to fail. When the lever is a prompt file,
   write it under `writing-for-agents`.
4. **State proof and rollback.** One sentence for the recurrence that should stop and the observation
   that would show it stopped; one sentence for removing the change cleanly if it turns into noise.

## Lenses

Where the lever usually sits:

- **Navigation**: how easy was it for the agent to find the right files? Are there hidden dependencies
  between files? Would a **navigation pointer** make it easier? _Use when_ the session took a long time
  to find a piece of information.
- **Automated checks**: are there automated checks that could catch errors the agent made? Linting,
  typing, tests, `dydo check`? _Use when_ the agent made a mistake that could have been caught by an
  automated check.
- **Coding standards**: should the review rubric be given a new rule to enforce? Should an existing
  rule be removed or clarified? The reviewer carries the least context pressure, so standards are
  imposed there, not on the implementer. _Use when_ the review failed to catch a mistake.
- **Entry point size**: are there steering instructions that should move to a guide or an automated
  check instead? _Use when_ the always-loaded entry point is particularly large, in the repo or in the
  human's global scope.
- **Tool economy**: did the agent make expensive tool calls that could be streamlined? Is there any
  custom tooling (CLIs, MCPs) that is particularly token-inefficient? _Use when_ the agent made an
  expensive tool call.
- **No-ops**: look for instructions in prompt files that don't modify the agent's behaviour. _Use when_
  the prompt files are large and unwieldy.
- **Information access**: look for opportunities to increase the agent's access to information. Teed
  dev server logs, read-only access to third-party services. _Use when_ a crucial piece of information
  was not available to the agent.

## Return

Candidates in severity order, each one line: recurrence with its two occurrences → lever → proof →
rollback. When no small credible change survives the checks, the pattern itself is the return.
