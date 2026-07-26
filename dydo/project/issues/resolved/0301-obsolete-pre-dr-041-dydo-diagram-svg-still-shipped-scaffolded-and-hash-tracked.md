---
title: Obsolete pre-DR-041 dydo-diagram.svg still shipped, scaffolded, and hash-tracked
id: 301
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-07-24
resolved-date: 2026-07-26
---

# Obsolete pre-DR-041 dydo-diagram.svg still shipped, scaffolded, and hash-tracked

The 193KB embedded diagram depicts the removed claim/inbox/agent-workspace architecture, is referenced by no scaffolded doc, yet is copied into every new project and special-cased in template update.

## Description

The SVG (UTF-16) shows 'dydo claim', inbox folders, dydo/agents/<Name> workspaces, welcome.md — all removed by DR-041. In a fresh scaffold no doc embeds it (inquisition coverage already lists it as a score-0 orphan); only the fallback about-dynadocs template (TemplateGenerator.GenerateFallbackAboutDynadocsMd) references it, and that fallback is itself stale (--inbox workflow flag, old role/edit table). Retire the asset: remove from GetAssetNames/FolderScaffolder/TemplateCommand binary list/frameworkHashes, refresh the fallback template, and have dydo template update delete the retired file from existing projects when it is hash-clean.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Resolved in v2.2.3. The obsolete diagram is no longer embedded or scaffolded; template
update removes known framework copies while preserving modified copies as user-owned assets.
The stale fallback documentation was updated to the native-runtime architecture.
