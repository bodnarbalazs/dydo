---
id: 172
area: general
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-06
---

# dydo audit replay subcommand referenced by plans/inquisitions but not exposed in the CLI

Plans and inquisition reports reference a 'dydo audit replay' verification subcommand that does not exist in Commands/. Either implement it or rewrite the language to use a real verification command.

## Description

### Evidence

- `grep -rn 'audit replay' Commands/` returns zero matches at HEAD.
- The PR4 plan referenced 'dydo audit replay byte-for-byte' as verification step 4. Charlie substituted `dydo inquisition coverage --since 30` as a live-data probe (documented in the PR4 implementation changelog).
- The surface gap was flagged in PR4's review brief and Charlie's completion message.

### Fix path

Either:

1. Implement the subcommand — emit replayable event JSON for a session ID, byte-for-byte, so verifications like 'compare audit replay output before/after a refactor' have a real CLI surface; or
2. Audit and rewrite plan/inquisition templates that reference 'dydo audit replay' so they cite real commands (`dydo audit`, `dydo inquisition coverage`, etc.).

### Why this matters

Without one of the two fixes, future plans will keep referencing a phantom command. Surfaced by pre-tag-audit Finding #5.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)