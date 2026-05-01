---
id: 10
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-03
resolved-date: 2026-04-07
---

# Excessive parameter counts on DispatchService methods

Resolved low-severity code-quality finding: several `DispatchService` methods carried long parameter lists that hurt readability and made call sites brittle. Closed under the recent code-quality cleanup that grouped parameters into request objects.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed in recent code quality work