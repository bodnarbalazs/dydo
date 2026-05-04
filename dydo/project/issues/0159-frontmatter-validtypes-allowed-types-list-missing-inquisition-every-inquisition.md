---
id: 159
area: project
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-04
---

# Frontmatter.ValidTypes allowed-types list missing 'inquisition' - every inquisition report errors on dydo check

## Description

Models/Frontmatter.cs:12 hardcodes ValidTypes = ["hub", "concept", "guide", "reference", "decision", "pitfall", "changelog", "context", "folder-meta", "issue"]. The 'inquisition' type is missing despite being a first-class concept (inquisitor.role.json exists; architecture.md has an Inquisition Coverage section; dydo/project/inquisitions/ has 12 populated reports). Every inquisition file errors on dydo check with: Invalid type value 'inquisition'. Brian acknowledged and deferred this exact gap to a parallel dydo-tool fix batch on 2026-05-01 (cleanup-docs-check-backlog.md:22-23). Confirmed by inquisition dydo-check-drift.md finding #1 (judge: Dexter).\n\nFix: append "inquisition" to Frontmatter.ValidTypes (one-line change). No template/doc changes needed.\n\nOpen question for planner: should Frontmatter.ValidTypes ever stop being a hardcoded array and become project-overridable (data-driven, like roles)? See also issues for findings #4 and #6 - same nudge.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)