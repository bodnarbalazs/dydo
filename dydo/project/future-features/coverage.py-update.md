---
title: Coverage.py Update
area: project
type: concept
status: idea
---

# Default Per-Method Bottleneck Detail for gap_check.py

`gap_check.py --inspect PATTERN --methods` already reports per-method CC, coverage, and CRAP for the relevant failing methods. This idea is to surface the bottleneck method breakdown by default in `--inspect`, without requiring `--methods`, so the module-level maximum has immediate context.

## Rationale

FutureFeature is a repo-native idea record. It remains unpromoted until a separate human decision creates Linear work.

## Related

- [Coverage Tools](../../reference/coverage-tools.md) — Current coverage and complexity tooling
