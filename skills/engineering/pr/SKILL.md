---
name: pr
description: "Use when writing a PR body."
metadata:
  credits:
    skill: show-me
    author: Dex Horthy
    organisation: Humanlayer
    url: "https://github.com/humanlayer/skills/blob/main/plugins/show-me/skills/show-me/SKILL.md"
---

<!-- Adapted from mattpocock/skills pr at 24fe0ef7737efae15c87225755e9f6f5965e4888 (v1.3.1, MIT). -->

# PR

## Must-Reads

1. From the repository root, read `dydo/glossary.md`.
2. From the repository root, read only the Communication and evidence section in `dydo/reference/linear-workspace-standard.md`.

Use this template for writing the PR body:

```markdown
## Summary

<diagram, diff-sketch, or tree>

## Evidence

- **Before:** <screenshot/output/failing test run>
  **After:** <screenshot/output/passing test run>

## Merge Danger

**Door:** <one-way or two-way>

<optional: description>

**Blast Radius:** <one-word description>

<optional: potential ramifications of merge>

## Independent review

<binding reviewer PASS block and linked evidence>
```

## Sections

Skip all preambles and keep prose brief. Use the user's domain language from the repository-root `dydo/glossary.md`.

### Summary

Pick the smallest view that makes the key point clear.

Load `show-me` for the visual forms and placement guidance.

### Evidence

Concrete evidence that the change works. Show a before and after.

Screenshots are S-tier - when the environment is set up for it and the change is visual.

Execution-based evidence is A-tier. Test results, console output. Show the exact test that now fails and passes, using pseudocode.

Keep proof compact; link the full raw proof and native gate logs once in a durable artifact. Name the candidate, command, environment or session, exit and result location for each gate.

### Merge Danger

Describe whether it's a one-way or two-way door. You can walk back through two-way doors, but not one-way doors. A PR that is cheap to roll back is lower risk. Changes that involve destructive actions or hard-to-reverse decisions are one-way doors.

The blast radius is the potential impact or scope of the changes introduced by this PR. Consider all possibilities. Examples are layout shift, breakages for consumers, mobile responsiveness, etc.

### Independent review

Carry the binding reviewer PASS block under `## Independent review`; the writer's own checks do not substitute for it. This skill writes the body; the owning officer retains review, integration and status, and the human alone lands the feature or atomic Issue on `main`/`master`.
