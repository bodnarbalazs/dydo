---
area: general
name: docs-batch-c
status: review-failed
created: 2026-03-16T19:13:28.6900157Z
assigned: Emma
updated: 2026-03-16T19:20:54.7239805Z
---

# Task: docs-batch-c

Docs Batch C: Fill in placeholder guide docs with complete content.

## Progress

- [x] guides/getting-started.md — First-time setup walkthrough
- [x] guides/customizing-roles.md — Custom role creation guide
- [x] guides/writing-good-briefs.md — Dispatch brief writing guide
- [x] guides/troubleshooting.md — Common errors and recovery

## Files Changed

- dydo/guides/getting-started.md
- dydo/guides/customizing-roles.md
- dydo/guides/writing-good-briefs.md
- dydo/guides/troubleshooting.md

## Review Summary

Documentation ready for review.

## Files written (all in dydo/guides/)

### 1. getting-started.md
First-time setup walkthrough. Covers: prerequisites, installation, dydo init claude, DYDO_HUMAN setup, first agent session, verifying setup, joining existing projects, configuring AI tool.

### 2. customizing-roles.md
Custom role creation guide. Covers: when to create vs use base roles, dydo roles create, .role.json schema, constraint types (role-transition, requires-prior, panel-limit), dydo roles list/reset, example DBA and DevOps roles.

### 3. writing-good-briefs.md
Dispatch brief writing guide. Covers: good brief qualities, brief anatomy, --brief vs --brief-file, common mistakes, examples for code-writer/reviewer/test-writer/docs-writer dispatches.

### 4. troubleshooting.md
Common errors and recovery. Covers: guard blocks, stuck states, recovery commands, validation errors, platform issues.

## Validation
dydo check passes — no errors introduced by these changes.

## Code Review (2026-03-16 20:35)

- Reviewed by: Charlie
- Result: FAILED
- Issues: One factual error: getting-started.md says .NET 8+ but actual requirement is .NET 10 (per DynaDocs.csproj TargetFramework). Dispatched Brian to fix.

Requires rework.