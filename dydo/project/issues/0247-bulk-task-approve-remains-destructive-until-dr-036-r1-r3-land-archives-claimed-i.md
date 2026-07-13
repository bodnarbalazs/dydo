---
title: Bulk task approve remains destructive until DR 036 R1-R3 land - archives claimed in-progress tasks with frontmatter rewrite
id: 247
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
---

# Bulk task approve remains destructive until DR 036 R1-R3 land - archives claimed in-progress tasks with frontmatter rewrite

Verified 2026-07-08 (DR 038 sweep, verify-first item): dydo task approve with the --all flag still archives EVERY task including claimed in-progress ones, rewriting frontmatter destructively (name/status/assigned stripped) - the behavior that wiped the 46-task board on 2026-07-08. The redesign is decided (DR 036: approve/reject removed, verification flips done, CoS/human-only archive sweep with claimed-guard + lossless frontmatter + stem date-suffix) and its R1-R3 implementation is sequenced after DR-034 S2a. This issue tracks the residual danger window: until R1-R3 land, bulk approve is loaded. Supersedes memory bulk-approve-board-wipe.

**RECURRED 2026-07-13 (severity bumped medium->high):** the loaded footgun detonated again — `dydo task approve --all` in the LC project swept 32 tasks including live in-flight ones (bot-defense-*, defense-in-depth-*, dydo-2x-migration, email-wiring-*), minting changelog entries + hub rewrites for never-done work. Five days after this issue was filed. This is no longer a theoretical danger window; it is actively destroying real work. Prioritized for immediate interim de-risk (status-gate `--all` to done/unclaimed only) ahead of the full DR-036 R1-R3 reform.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)