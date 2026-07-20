---
title: dydo agent status / whoami still print per-role Writable/Read-only paths that no longer constrain a claimed main-thread agent (per-role RBAC removed in DR 024); misleading display
id: 223
area: general
type: issue
severity: low
status: resolved
resolved-date: 2026-07-20
found-by: review
date: 2026-07-07
---

# dydo agent status / whoami still print per-role Writable/Read-only paths that no longer constrain a claimed main-thread agent (per-role RBAC removed in DR 024); misleading display

dydo agent status / whoami printed per-role Writable/Read-only path lists that stopped constraining anything when DR-024 removed per-role RBAC — a misleading display.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-20 as OBSOLETE: the `dydo agent` command group (status, whoami, and the rest of the claim-era surface) was deleted wholesale in the DR-041 simplification campaign. Nothing prints per-role paths anymore. The sibling display bug was tracked and resolved as #0244.