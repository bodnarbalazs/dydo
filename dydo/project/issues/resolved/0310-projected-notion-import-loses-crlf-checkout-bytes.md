---
title: Projected Notion import loses CRLF checkout bytes
id: 310
area: backend
type: issue
severity: high
status: resolved
found-by: test
date: 2026-08-18
---

# Projected Notion import loses CRLF checkout bytes

Projected imports could change untouched canonical newlines in CRLF checkouts because fixture setup was newline-sensitive and terminal structural edits did not fully preserve source-span ownership.

## Reproduction

The dual-projection full/delta and fidelity paths failed under a clean CRLF checkout when a one-span Notion edit was imported into an existing canonical file.

## Resolution

The hotfix moved newline ownership to the projection source spans: local separators and terminators remain local, while external content owns only the replacement or insertion span. It added hermetic LF/CRLF fixtures, root and nested terminal-boundary regressions, plus mandatory Ubuntu/LF and Windows/CRLF release verification.

Offline projection, full/delta, release-workflow, build, and final coverage gates passed. On 2026-08-18 the authorized live body-fidelity suite ran exactly three scratch-child facts: 3 passed, 0 skipped; cleanup archived the children and no credentials or page identifiers were recorded.
