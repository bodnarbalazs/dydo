---
area: general
type: changelog
date: 2026-04-26
---

# Task: triage-roles-docs

Verification-only triage of ten roles/permissions/docs issues an initial pass had marked STILL-VALID. The triager's job was to read code and docs, gather concrete file:line evidence for each claim, and report findings — no code, doc, or issue mutations. Output feeds Brian's housekeeping pass on the issue tracker.

# Triage verification: Roles, permissions, docs issues

## Why you're here

Brian (orchestrator) is housekeeping the issue tracker. Ten issues below are mostly about documentation drift and role-system details. An assistant's initial pass classified most as STILL-VALID; we need concrete file:line evidence to confirm before closing anything.

Your job is **verification only** — do not modify code or docs, do not open new issues, do not dispatch sub-agents. Read, check, report.

## Scope

Ten issues — roles, permissions, guard/role docs:

| ID | Claim summary |
|----|---------------|
| [#0043](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0043-roles-and-permissions-md-incomplete-glob-pattern-documentation.md) | `roles-and-permissions.md` — incomplete glob pattern documentation |
| [#0044](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0044-roles-and-permissions-md-role-schema-sample-missing-canorchestrate-and-condition.md) | `roles-and-permissions.md` — role schema sample missing `canOrchestrate` and `conditionalMustReads` |
| [#0045](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0045-inconsistent-case-sensitivity-in-roleconstraintevaluator.md) | Inconsistent case sensitivity in `RoleConstraintEvaluator` |
| [#0046](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0046-globmatcher-recompiles-regex-on-every-call-without-caching.md) | `GlobMatcher` recompiles regex on every call without caching |
| [#0047](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0047-panel-limit-constraint-counts-requesting-agent-against-itself.md) | Panel-limit constraint counts requesting agent against itself |
| [#0048](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0048-h10-h11-h12-labels-are-doc-only-with-no-code-traceability.md) | H10/H11/H12 labels are doc-only with no code traceability |
| [#0064](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0064-h19-indirect-dydo-invocation-documented-as-hard-coded-but-is-configurable-nudge.md) | H19 indirect-dydo invocation documented as hard-coded but is configurable nudge |
| [#0066](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0066-git-merge-worktree-block-and-human-only-command-restriction-lack-guardrail-ids.md) | Git-merge worktree block and human-only command restriction lack guardrail IDs |
| [#0067](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0067-s3-unread-message-delivery-behaves-as-hard-rule-but-categorized-as-soft-block.md) | S3 unread message delivery behaves as hard-rule but categorized as soft-block |
| [#0069](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0069-stage-2-agents-can-read-all-agents-mode-files-via-off-limits-bypass-undocumented.md) | Stage-2 agents can read all agents' mode files via off-limits bypass — undocumented |

Issue files: `dydo/project/issues/00{43,44,45,46,47,48,64,66,67,69}-*.md`

## Method

For each issue:

1. Read the issue file end to end — note exact symbol/file/section cited.
2. Inspect the relevant code or doc. Primary locations:
   - `dydo/understand/roles-and-permissions.md` ([#0043](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0043-roles-and-permissions-md-incomplete-glob-pattern-documentation.md), [#0044](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0044-roles-and-permissions-md-role-schema-sample-missing-canorchestrate-and-condition.md), [#0047](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0047-panel-limit-constraint-counts-requesting-agent-against-itself.md), [#0048](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0048-h10-h11-h12-labels-are-doc-only-with-no-code-traceability.md))
   - `dydo/understand/guard-system.md` ([#0066](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0066-git-merge-worktree-block-and-human-only-command-restriction-lack-guardrail-ids.md), [#0069](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0069-stage-2-agents-can-read-all-agents-mode-files-via-off-limits-bypass-undocumented.md))
   - `dydo/reference/guardrails.md` (all `H##` label issues — [#0048](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0048-h10-h11-h12-labels-are-doc-only-with-no-code-traceability.md), [#0064](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0064-h19-indirect-dydo-invocation-documented-as-hard-coded-but-is-configurable-nudge.md), [#0066](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0066-git-merge-worktree-block-and-human-only-command-restriction-lack-guardrail-ids.md), [#0067](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0067-s3-unread-message-delivery-behaves-as-hard-rule-but-categorized-as-soft-block.md))
   - `Services/RoleConstraintEvaluator.cs` ([#0045](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0045-inconsistent-case-sensitivity-in-roleconstraintevaluator.md), [#0047](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0047-panel-limit-constraint-counts-requesting-agent-against-itself.md))
   - `Services/GlobMatcher.cs` ([#0046](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0046-globmatcher-recompiles-regex-on-every-call-without-caching.md))
   - `Services/OffLimitsService.cs` / guard bootstrap path ([#0069](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0069-stage-2-agents-can-read-all-agents-mode-files-via-off-limits-bypass-undocumented.md))
3. For "docs-say-X" claims: read the current doc paragraph and quote it in your verdict (so Brian can see without re-reading). For code claims: grep + read.
4. Classify FIXED / STILL-VALID / UNCLEAR with concrete evidence.

Pay attention to:
- **[#0048](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0048-h10-h11-h12-labels-are-doc-only-with-no-code-traceability.md), [#0064](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0064-h19-indirect-dydo-invocation-documented-as-hard-coded-but-is-configurable-nudge.md), [#0066](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0066-git-merge-worktree-block-and-human-only-command-restriction-lack-guardrail-ids.md), [#0067](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0067-s3-unread-message-delivery-behaves-as-hard-rule-but-categorized-as-soft-block.md)** — these are all about H## guardrail-label taxonomy. Understand the current labelling scheme before judging individually; a recent consolidation may have touched several at once.
- **[#0045](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0045-inconsistent-case-sensitivity-in-roleconstraintevaluator.md)** — read the specific comparator. "Inconsistent" means some paths use `Ordinal` and others `OrdinalIgnoreCase`, or the casing varies by input side. Verify both sides.
- **[#0046](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0046-globmatcher-recompiles-regex-on-every-call-without-caching.md)** — `GlobMatcher` regex cache. Check whether compiled patterns are cached on the object or per-call-recomputed. A `ConcurrentDictionary<string, Regex>` on the class = fixed; method-local compile = still-valid.
- **[#0069](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0069-stage-2-agents-can-read-all-agents-mode-files-via-off-limits-bypass-undocumented.md)** — this is a security-adjacent claim ("agents can read mode files they shouldn't"). Verify the actual bypass path still exists before calling it fixed; don't assume absence of grep hits means absence of bypass.

## Context hints (hypotheses, verify)

- Commit `7faf851` reportedly touched several role/permissions doc pages.
- No fix is known for [#0046](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0046-globmatcher-recompiles-regex-on-every-call-without-caching.md) — GlobMatcher caching. Probably still-valid, but confirm.

## Deliverable

Send one message to Brian:

```bash
dydo msg --to Brian --subject triage-roles-docs --body "
[#0043](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0043-roles-and-permissions-md-incomplete-glob-pattern-documentation.md) — <verdict> — <evidence>
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
| [#0043](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0043-roles-and-permissions-md-incomplete-glob-pattern-documentation.md) | `roles-and-permissions.md` — incomplete glob pattern documentation |
| [#0044](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0044-roles-and-permissions-md-role-schema-sample-missing-canorchestrate-and-condition.md) | `roles-and-permissions.md` — role schema sample missing `canOrchestrate` and `conditionalMustReads` |
| [#0045](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0045-inconsistent-case-sensitivity-in-roleconstraintevaluator.md) | Inconsistent case sensitivity in `RoleConstraintEvaluator` |
| [#0046](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0046-globmatcher-recompiles-regex-on-every-call-without-caching.md) | `GlobMatcher` recompiles regex on every call without caching |
| [#0047](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0047-panel-limit-constraint-counts-requesting-agent-against-itself.md) | Panel-limit constraint counts requesting agent against itself |
| [#0048](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0048-h10-h11-h12-labels-are-doc-only-with-no-code-traceability.md) | H10/H11/H12 labels are doc-only with no code traceability |
| [#0064](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0064-h19-indirect-dydo-invocation-documented-as-hard-coded-but-is-configurable-nudge.md) | H19 indirect-dydo invocation documented as hard-coded but is configurable nudge |
| [#0066](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0066-git-merge-worktree-block-and-human-only-command-restriction-lack-guardrail-ids.md) | Git-merge worktree block and human-only command restriction lack guardrail IDs |
| [#0067](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0067-s3-unread-message-delivery-behaves-as-hard-rule-but-categorized-as-soft-block.md) | S3 unread message delivery behaves as hard-rule but categorized as soft-block |
| [#0069](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0069-stage-2-agents-can-read-all-agents-mode-files-via-off-limits-bypass-undocumented.md) | Stage-2 agents can read all agents' mode files via off-limits bypass — undocumented |

Issue files: `dydo/project/issues/00{43,44,45,46,47,48,64,66,67,69}-*.md`

## Method

For each issue:

1. Read the issue file end to end — note exact symbol/file/section cited.
2. Inspect the relevant code or doc. Primary locations:
   - `dydo/understand/roles-and-permissions.md` ([#0043](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0043-roles-and-permissions-md-incomplete-glob-pattern-documentation.md), [#0044](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0044-roles-and-permissions-md-role-schema-sample-missing-canorchestrate-and-condition.md), [#0047](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0047-panel-limit-constraint-counts-requesting-agent-against-itself.md), [#0048](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0048-h10-h11-h12-labels-are-doc-only-with-no-code-traceability.md))
   - `dydo/understand/guard-system.md` ([#0066](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0066-git-merge-worktree-block-and-human-only-command-restriction-lack-guardrail-ids.md), [#0069](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0069-stage-2-agents-can-read-all-agents-mode-files-via-off-limits-bypass-undocumented.md))
   - `dydo/reference/guardrails.md` (all `H##` label issues — [#0048](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0048-h10-h11-h12-labels-are-doc-only-with-no-code-traceability.md), [#0064](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0064-h19-indirect-dydo-invocation-documented-as-hard-coded-but-is-configurable-nudge.md), [#0066](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0066-git-merge-worktree-block-and-human-only-command-restriction-lack-guardrail-ids.md), [#0067](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0067-s3-unread-message-delivery-behaves-as-hard-rule-but-categorized-as-soft-block.md))
   - `Services/RoleConstraintEvaluator.cs` ([#0045](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0045-inconsistent-case-sensitivity-in-roleconstraintevaluator.md), [#0047](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0047-panel-limit-constraint-counts-requesting-agent-against-itself.md))
   - `Services/GlobMatcher.cs` ([#0046](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0046-globmatcher-recompiles-regex-on-every-call-without-caching.md))
   - `Services/OffLimitsService.cs` / guard bootstrap path ([#0069](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0069-stage-2-agents-can-read-all-agents-mode-files-via-off-limits-bypass-undocumented.md))
3. For "docs-say-X" claims: read the current doc paragraph and quote it in your verdict (so Brian can see without re-reading). For code claims: grep + read.
4. Classify FIXED / STILL-VALID / UNCLEAR with concrete evidence.

Pay attention to:
- **[#0048](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0048-h10-h11-h12-labels-are-doc-only-with-no-code-traceability.md), [#0064](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0064-h19-indirect-dydo-invocation-documented-as-hard-coded-but-is-configurable-nudge.md), [#0066](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0066-git-merge-worktree-block-and-human-only-command-restriction-lack-guardrail-ids.md), [#0067](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0067-s3-unread-message-delivery-behaves-as-hard-rule-but-categorized-as-soft-block.md)** — these are all about H## guardrail-label taxonomy. Understand the current labelling scheme before judging individually; a recent consolidation may have touched several at once.
- **[#0045](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0045-inconsistent-case-sensitivity-in-roleconstraintevaluator.md)** — read the specific comparator. "Inconsistent" means some paths use `Ordinal` and others `OrdinalIgnoreCase`, or the casing varies by input side. Verify both sides.
- **[#0046](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0046-globmatcher-recompiles-regex-on-every-call-without-caching.md)** — `GlobMatcher` regex cache. Check whether compiled patterns are cached on the object or per-call-recomputed. A `ConcurrentDictionary<string, Regex>` on the class = fixed; method-local compile = still-valid.
- **[#0069](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0069-stage-2-agents-can-read-all-agents-mode-files-via-off-limits-bypass-undocumented.md)** — this is a security-adjacent claim ("agents can read mode files they shouldn't"). Verify the actual bypass path still exists before calling it fixed; don't assume absence of grep hits means absence of bypass.

## Context hints (hypotheses, verify)

- Commit `7faf851` reportedly touched several role/permissions doc pages.
- No fix is known for [#0046](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0046-globmatcher-recompiles-regex-on-every-call-without-caching.md) — GlobMatcher caching. Probably still-valid, but confirm.

## Deliverable

Send one message to Brian:

```bash
dydo msg --to Brian --subject triage-roles-docs --body "
[#0043](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0043-roles-and-permissions-md-incomplete-glob-pattern-documentation.md) — <verdict> — <evidence>
... (one line per issue)

Summary: X FIXED, Y STILL-VALID, Z UNCLEAR.
Notes: <anything Brian needs to know — especially if any H## label drift was found>."
```

Concrete evidence per line, quote docs where relevant. Then release.

## Approval

- Approved: 2026-04-26 19:39
