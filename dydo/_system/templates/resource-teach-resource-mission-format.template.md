<!-- Adapted from mattpocock/skills teach at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# MISSION.md Format

`MISSION.md` lives at the workspace root. It captures the _reason_ the human is learning this topic.
Every teaching decision (what to teach next, which resources to surface, which exercises to design)
should trace back to this document.

## Template

```md
# Mission: {Topic}

## Why
{1-3 sentences. The concrete real-world goal the human is chasing. What changes in their life or work when they have this skill? Avoid abstract framings like "to understand X"; push for the underlying outcome.}

## Success looks like
- {A specific, observable thing the human will be able to do}
- {Another specific thing}
- {…}

## Constraints
- {Time, budget, prior commitments, learning preferences, anything that bounds the approach}

## Out of scope
- {Adjacent topics the human explicitly does not want to chase right now, protecting the zone of proximal development}
```

## Rules

- **One mission per workspace.** If the human wants to learn two unrelated things, that is two
  workspaces.
- **Concrete over abstract.** "Run a half marathon by October" beats "get fitter." "Ship a Rust CLI to
  my team" beats "learn Rust."
- **Push back on vagueness.** If the human cannot articulate why, interview them before writing
  anything. A bad mission is worse than no mission.
- **Revise when reality shifts.** Missions change. When the human's goal moves, update this file:
  don't leave a stale mission steering future sessions.
- **Keep it short.** If `MISSION.md` runs past a screen, it has stopped being a compass and started
  being a plan.
