---
name: walkthrough
description: Before I land it — show me what changed, where to look first, and how to try it.
emit: skill
invocation: explicit
---

# Walkthrough

Give the human the tour of what he is about to land.

This is the Land stage: he reads the tour, then merges the feature into main with his own hands. On
main afterwards he calls the same tour to see what he now owns.

The argument names what to walk — a feature branch, a Project, an Issue, a diff range. Ask which one
when it is missing.

Walk it yourself first: the diff, the Issues it closed with their review blocks, the DRs those Issues
cite, the notes their writers returned. Then brief him in four parts.

- **What changed and why** — the outcome in one paragraph, each part traced to the Issue that carried
  it and the DR that decided it.
- **Where to look first** — a route for his own file-by-file pass: the files that drew findings, then
  the ones a writer left an open doubt on, then the seams the change moved.
- **How to try it** — the exact commands to build it and see the new behaviour, in order, each one run
  once by you so it is true when he types it.
- **What reviewers flagged or deferred** — the findings the review blocks recorded and where each
  deferred one now lives, reported as they stand.

The tour ends where he can act: he opens the files you named, types the commands you gave, and clicks
the merge himself.

Keep the brief in the terminal, or in a scratch file when he wants to read it beside the diff.
The durable record stays where it already is: the Issues, the review blocks and the DRs the
tour points at.
