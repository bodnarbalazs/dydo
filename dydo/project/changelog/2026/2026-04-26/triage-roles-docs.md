---
area: general
type: changelog
date: 2026-04-26
---

# Task: triage-roles-docs

# Triage verification: Roles, permissions, docs issues

## Why you're here

Brian (orchestrator) is housekeeping the issue tracker. Ten issues below are mostly about documentation drift and role-system details. An assistant's initial pass classified most as STILL-VALID; we need concrete file:line evidence to confirm before closing anything.

Your job is **verification only** — do not modify code or docs, do not open new issues, do not dispatch sub-agents. Read, check, report.

## Scope

Ten issues — roles, permissions, guard/role docs:

| ID | Claim summary |
|----|---------------|
| #0043 | `roles-and-permissions.md` — incomplete glob pattern documentation |
| #0044 | `roles-and-permissions.md` — role schema sample missing `canOrchestrate` and `conditionalMustReads` |
| #0045 | Inconsistent case sensitivity in `RoleConstraintEvaluator` |
| #0046 | `GlobMatcher` recompiles regex on every call without caching |
| #0047 | Panel-limit constraint counts requesting agent against itself |
| #0048 | H10/H11/H12 labels are doc-only with no code traceability |
| #0064 | H19 indirect-dydo invocation documented as hard-coded but is configurable nudge |
| #0066 | Git-merge worktree block and human-only command restriction lack guardrail IDs |
| #0067 | S3 unread message delivery behaves as hard-rule but categorized as soft-block |
| #0069 | Stage-2 agents can read all agents' mode files via off-limits bypass — undocumented |

Issue files: `dydo/project/issues/00{43,44,45,46,47,48,64,66,67,69}-*.md`

## Method

For each issue:

1. Read the issue file end to end — note exact symbol/file/section cited.
2. Inspect the relevant code or doc. Primary locations:
   - `dydo/understand/roles-and-permissions.md` (#0043, #0044, #0047, #0048)
   - `dydo/understand/guard-system.md` (#0066, #0069)
   - `dydo/reference/guardrails.md` (all `H##` label issues — #0048, #0064, #0066, #0067)
   - `Services/RoleConstraintEvaluator.cs` (#0045, #0047)
   - `Services/GlobMatcher.cs` (#0046)
   - `Services/OffLimitsService.cs` / guard bootstrap path (#0069)
3. For "docs-say-X" claims: read the current doc paragraph and quote it in your verdict (so Brian can see without re-reading). For code claims: grep + read.
4. Classify FIXED / STILL-VALID / UNCLEAR with concrete evidence.

Pay attention to:
- **#0048, #0064, #0066, #0067** — these are all about H## guardrail-label taxonomy. Understand the current labelling scheme before judging individually; a recent consolidation may have touched several at once.
- **#0045** — read the specific comparator. "Inconsistent" means some paths use `Ordinal` and others `OrdinalIgnoreCase`, or the casing varies by input side. Verify both sides.
- **#0046** — `GlobMatcher` regex cache. Check whether compiled patterns are cached on the object or per-call-recomputed. A `ConcurrentDictionary<string, Regex>` on the class = fixed; method-local compile = still-valid.
- **#0069** — this is a security-adjacent claim ("agents can read mode files they shouldn't"). Verify the actual bypass path still exists before calling it fixed; don't assume absence of grep hits means absence of bypass.

## Context hints (hypotheses, verify)

- Commit `7faf851` reportedly touched several role/permissions doc pages.
- No fix is known for #0046 — GlobMatcher caching. Probably still-valid, but confirm.

## Deliverable

Send one message to Brian:

```bash
dydo msg --to Brian --subject triage-roles-docs --body "
#0043 — <verdict> — <evidence>
... (one line per issue)

Summary: X FIXED, Y STILL-VALID, Z UNCLEAR.
Notes: <anything Brian needs to know — especially if any H## label drift was found>."
```

Concrete evidence per line, quote docs where relevant. Then release.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

# Triage verification: Roles, permissions, docs issues

## Why you're here

Brian (orchestrator) is housekeeping the issue tracker. Ten issues below are mostly about documentation drift and role-system details. An assistant's initial pass classified most as STILL-VALID; we need concrete file:line evidence to confirm before closing anything.

Your job is **verification only** — do not modify code or docs, do not open new issues, do not dispatch sub-agents. Read, check, report.

## Scope

Ten issues — roles, permissions, guard/role docs:

| ID | Claim summary |
|----|---------------|
| #0043 | `roles-and-permissions.md` — incomplete glob pattern documentation |
| #0044 | `roles-and-permissions.md` — role schema sample missing `canOrchestrate` and `conditionalMustReads` |
| #0045 | Inconsistent case sensitivity in `RoleConstraintEvaluator` |
| #0046 | `GlobMatcher` recompiles regex on every call without caching |
| #0047 | Panel-limit constraint counts requesting agent against itself |
| #0048 | H10/H11/H12 labels are doc-only with no code traceability |
| #0064 | H19 indirect-dydo invocation documented as hard-coded but is configurable nudge |
| #0066 | Git-merge worktree block and human-only command restriction lack guardrail IDs |
| #0067 | S3 unread message delivery behaves as hard-rule but categorized as soft-block |
| #0069 | Stage-2 agents can read all agents' mode files via off-limits bypass — undocumented |

Issue files: `dydo/project/issues/00{43,44,45,46,47,48,64,66,67,69}-*.md`

## Method

For each issue:

1. Read the issue file end to end — note exact symbol/file/section cited.
2. Inspect the relevant code or doc. Primary locations:
   - `dydo/understand/roles-and-permissions.md` (#0043, #0044, #0047, #0048)
   - `dydo/understand/guard-system.md` (#0066, #0069)
   - `dydo/reference/guardrails.md` (all `H##` label issues — #0048, #0064, #0066, #0067)
   - `Services/RoleConstraintEvaluator.cs` (#0045, #0047)
   - `Services/GlobMatcher.cs` (#0046)
   - `Services/OffLimitsService.cs` / guard bootstrap path (#0069)
3. For "docs-say-X" claims: read the current doc paragraph and quote it in your verdict (so Brian can see without re-reading). For code claims: grep + read.
4. Classify FIXED / STILL-VALID / UNCLEAR with concrete evidence.

Pay attention to:
- **#0048, #0064, #0066, #0067** — these are all about H## guardrail-label taxonomy. Understand the current labelling scheme before judging individually; a recent consolidation may have touched several at once.
- **#0045** — read the specific comparator. "Inconsistent" means some paths use `Ordinal` and others `OrdinalIgnoreCase`, or the casing varies by input side. Verify both sides.
- **#0046** — `GlobMatcher` regex cache. Check whether compiled patterns are cached on the object or per-call-recomputed. A `ConcurrentDictionary<string, Regex>` on the class = fixed; method-local compile = still-valid.
- **#0069** — this is a security-adjacent claim ("agents can read mode files they shouldn't"). Verify the actual bypass path still exists before calling it fixed; don't assume absence of grep hits means absence of bypass.

## Context hints (hypotheses, verify)

- Commit `7faf851` reportedly touched several role/permissions doc pages.
- No fix is known for #0046 — GlobMatcher caching. Probably still-valid, but confirm.

## Deliverable

Send one message to Brian:

```bash
dydo msg --to Brian --subject triage-roles-docs --body "
#0043 — <verdict> — <evidence>
... (one line per issue)

Summary: X FIXED, Y STILL-VALID, Z UNCLEAR.
Notes: <anything Brian needs to know — especially if any H## label drift was found>."
```

Concrete evidence per line, quote docs where relevant. Then release.

## Approval

- Approved: 2026-04-26 19:39
