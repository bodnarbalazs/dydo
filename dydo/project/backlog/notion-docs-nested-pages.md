---
area: project
type: context
status: open
date: 2026-07-06
---

# Docs → Notion Nested-Pages Sync — design brief

A parallel-track feature spun off during the notion-sync live shakedown (2026-07-06). Needs its own
**decision record** then a **run-sprint**; it does not block the PM-board sync work.

## Decision already taken (with Balazs)

Sync the knowledge-base docs tree to Notion as a **nested-page hierarchy mirroring the repo**, **one-way
(repo → Notion, read-only mirror)**.

- **Why nested pages, not a database:** docs are reference material to *read/browse*, not records to
  query/filter like the PM spine. Notion's nested pages ARE the folder equivalent, so mirroring the tree
  gives natural sidebar navigation and handles any custom folder structure by plain recursion.
- **Why one-way:** the repo is the source of truth for docs; a read-only mirror avoids the entire two-way
  reconcile/merge machinery. Notion-side edits are overwritten on the next sync.

## Scope

- Recurse the docs tree under the dydo root, **excluding** the PM dirs already synced as databases
  (`project/releases`, `project/campaigns`, `project/sprints`, `project/sprint-tasks`, `project/issues`)
  and `_system/`. (Confirm the exact include set with Balazs — likely `understand/`, `guides/`,
  `reference/`, `project/decisions/`, `project/inquisitions/`, `project/changelog/`, and the hub `index.md`.)
- Each folder → a Notion page; each `.md` doc → a child page under its folder's page; the doc's markdown
  body → page content via the existing **`NotionBlockConverter`** (already handles code-fence language
  mapping + the 2000-char rich-text cap after this session's fixes).
- **Idempotent:** a persisted `path → pageId` store (mirror `NotionProvisioner`'s `provision.json` /
  `BaseSnapshotStore` pattern) so re-sync updates in place, never duplicates. Handle renames, moves, deletes
  (archive the Notion page for a removed doc).
- Root under the configured parent page (`NotionParentResolver`), likely a dedicated "Docs" page sibling to
  the spine databases.

## Open design questions (for the co-thinker round)

1. Exact include/exclude set of subtrees.
2. Inter-doc links: rewrite `[[wikilinks]]` / relative `.md` links into Notion page links, or leave as text?
3. Page/child ordering — preserve folder + filename order?
4. Deletion/rename policy and how the `path→pageId` store detects them.
5. Where the docs root page lives, and whether folder pages carry the folder's `_index.md` body.

## Reuse / prereqs

`NotionClient` (add a create-child-page-under-page method if absent — the spine only creates DB rows),
`NotionBlockConverter` (done), the `BaseSnapshotStore`/`provision.json` persistence pattern,
`NotionParentResolver`. Live-verify against real Notion: nested-page creation, append-children batch limits,
and archive-on-delete — the same "FakeNotionClient can't catch it" gap that bit the formula sync
(`notion-sync-live-api-constraints`).

## Deliverable

A DR capturing the above, then a `run-sprint` of sliced implementation: `DocsTreeSync` orchestrator + the
client page-nesting method + the `path→pageId` store + tests + a live smoke.
