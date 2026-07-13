---
title: Notion sync: spine database rows created with no title (every card shows 'New page')
id: 290
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-13
---

# Notion sync: spine database rows created with no title (every card shows 'New page')

Synced spine rows (Tasks, Future Features) show Notion's default 'New page' instead of the object title; non-title properties populate fine.

## Description

## Observed

After `dydo notion connect --parent-page <id>` + `dydo notion sync` against a real workspace, **every row in the provisioned spine databases** (Tasks, Future Features, etc.) shows Notion's default **"New page"** title instead of the object's real name/title. Non-title select properties (`status`, `area`, `needs-human`) ARE populated correctly, so the row upsert itself works — only the title/Name property is missing. Confirmed visually on the Tasks and Future Features boards (all cards read "New page").

## Root-cause direction

The docs-tree page path (`Sync/Notion/DocsPageAdapter.cs:155`) correctly sets `["title"] = new() { Type = "title", Title = NotionRichText.Of(TitleFor(localId)) }`. The **spine ROW path** (object → database-row reconcile, `NotionSpineSync` + the row property mapper) does NOT populate the title/Name property from the object's title.

Find where spine objects are written as database rows and ensure the title property is set from the object's title (frontmatter `title`/`name`, or a sensible fallback like the slug — mirror `DocsPageAdapter.TitleFor`'s empty-guard).

## Acceptance

- Synced spine rows show their real titles (no "New page").
- Add a wire-shape / adapter test asserting the title property is populated on row upsert (and falls back sanely when the object title is empty).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)