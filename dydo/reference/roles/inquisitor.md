---
area: reference
type: reference
---

# Inquisitor

The campaign-end QA sweeper — a read-only agent the `inquisition` workflow spawns to audit landed work. It is **not a claimable dydo role**: it exists only as a Claude Code agent + skill (`.claude/agents/inquisitor.md` + `.claude/skills/inquisitor/`), spawned by the workflow, never claimed by a human. The `inquisitor` (and `judge`) *roles* from earlier versions were retired in [Decision 024](../../project/decisions/024-dydo-2-native-pivot.md); the adversarial-QA job now lives in the workflow.

## What It Does

The `inquisition` workflow fans out one `inquisitor` subagent per QA lens — correctness, test-coverage gaps, security, dead code, and doc drift — then spawns more inquisitors to adversarially verify each finding (refute-by-default: a finding survives only if the code confirms it). Its signature concern is **test-coverage gaps** — what a per-change review never checks. The workflow gates on confirmed high-severity findings.

## Tools

`Read, Grep, Glob, Bash` — read-only. The inquisitor assesses and reports; it never modifies the project.

## Design Notes

- Distinct from the reviewer by design (see [Decision 031](../../project/decisions/031-sprint-auditor-charter-rewrite.md) for the sibling sprint-auditor charter): a per-change reviewer checks a diff; the inquisitor audits a whole landed campaign, codebase-wide, hunting for what slipped through.
- Its methodology skill is kept laser-focused on one lens at a time, so a sweep is many single-purpose passes rather than one diffuse one.

## Related

- [Reviewer](./reviewer.md) — per-change review, the complementary read-only role
- [Test-Writer](./test-writer.md) — writes the tests that coverage gaps call for
- [Guardrails Reference](../guardrails.md) — universal off-limits and nudges
