---
id: 11
area: platform
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-03
resolved-date: 2026-04-07
---

# Judge role missing write permission for dydo/project/inquisitions/**

Resolved medium-severity role-config bug: the judge role lacked write permission for `dydo/project/inquisitions/**`, blocking judges from filing inquisition outcomes. Closed under the recent code-quality cleanup that added the missing path to `judge.role.json`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in recent code quality work