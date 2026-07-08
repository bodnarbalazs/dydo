---
area: general
type: decision
status: proposed
date: 2026-07-06
participants: [balazs, Charlie]
---

# 033 — Docs → Notion Nested-Page Mirror: Repo-Owned Tree, Bidirectional Bodies

The knowledge-base docs tree is mirrored to Notion as a **nested-page hierarchy** that follows the
repo folder shape. This is a **second Notion surface** alongside the PM spine of [DR 025](./025-notion-sync-architecture.md):
the spine syncs queryable PM *records* as databases; this syncs reference *documents* as pages you
read and browse. It reuses 025's sync engine rather than inventing a new one — the only genuinely new
rule is an **asymmetry**: the **tree structure is repo-owned** (one-way, repo → Notion) while each
document's **body + frontmatter are bidirectional** (edit in Obsidian, the IDE, or Notion; the 3-way
merge reconciles). DR 025 already owns the canonical-repo philosophy and bidirectional authored
content; this record captures only what 025 does not — the page-tree surface, the structure/body
split, and the concrete mechanics.

## Context

Docs (`understand/`, `guides/`, `reference/`, and any other non-`project/` reference material) are
material to *read and browse*, not records to query and filter like the Campaign → Sprint → Task
spine. [DR 034 — PM Record Taxonomy](./034-pm-record-taxonomy.md) confirms almost everything under
`project/` is a queryable **record** (its own spine DB or a property of one), so in practice the
mirror is essentially the non-`project/` reference tree. Notion's **nested pages are the folder
equivalent**, so mirroring the tree gives natural sidebar navigation and handles any custom folder
structure by plain recursion.

025 never contemplated a hierarchy: its object model (§7) is flat database rows, so it never had to
rule on who owns tree *shape*. It did, however, already establish (§4) that some things are **not
symmetric because they simply aren't synced documents** — presentation, rollups, the live agent
board. The page-tree shape slots cleanly onto that same list as a fourth entry: the hierarchy is a
repo-owned projection; the *content inside each page* is the symmetric, authored, bidirectional part.
So this extends 025's own logic; it does not contradict it. The acceptance test from 025 §1 still
holds: **delete the Notion adapter and the repo is whole.**

## Decision

### 1. Docs mirror as a nested-page tree (new adapter surface)
Recurse the docs tree. Each **folder → a Notion page**; each **`.md` doc → a child page** under its
folder's page; the doc's markdown body → page content via the existing **`NotionBlockConverter`**. A
folder's page body is that folder's **`_index.md` / `index.md`** if present (so folder pages aren't
empty containers), otherwise just a parent holding its children. This is a distinct surface from the
DB-and-row spine — model-driven `NotionSpineSync` is untouched.

### 2. Structure is repo-owned; body + frontmatter are bidirectional
The **set of pages and their parent/child arrangement is dictated by the repo tree** and flows one
way (repo → Notion). Create / rename / move / delete are **repo actions** that project into Notion.
Each document's **body and frontmatter sync bidirectionally**. Consequences:
- The sync manages **only pages it created** (tracked in its store — §6). A page a colleague creates
  or moves *in Notion*, outside the store, is **ignored, not adopted** — structure is repo-owned, so
  we never reverse-engineer a repo file/hierarchy from Notion-born structure.
- A repo doc that disappears → its mapped Notion page is **archived** (§6).
- Body edits made in Notion on a *managed* page still merge back (§3), because that page maps to a
  repo doc. Structural drift in Notion (reordering, a stray page) is simply reconciled toward repo
  shape on the next tick; it never rewrites the repo.

### 3. Reuse the 025 engine for bodies — no new merge machinery
Bodies + frontmatter go through 025's **base-snapshot + 3-way merge** (§3): a shadow of the
last-synced state per doc, diff `base→repo` and `base→external`, one-sided change applies, two-sided
change 3-way-merges against the base and writes both sides. A **new page-tree adapter** feeds the
existing `SyncRunner` / `BaseSnapshotStore` — it enumerates the Notion page tree and reads page
bodies instead of querying a data source. The merge logic, snapshot store, and converter round-trip
are reused as-is.

### 4. Client surface extension (coordinated with the spine track)
The existing client only nests *databases* under a page and only creates DB *rows*
(`NotionParent` is hardcoded to `type: "data_source_id"`). This surface gains:
- a **`page_id` parent variant** on `NotionParent` (mirroring `NotionDatabaseParent`);
- a **child-page create** path and **page-tree enumeration + body read** on `INotionClient` /
  `NotionClient` (+ the `FakeNotionClient` fixture).

These touch the shared `Sync/Notion` surface. Per the parallel-track protocol they land **after**
Brian's board-views changes (done, green) so the two tracks never dirty the tree against the global
test gate at once. The `FakeNotionClient` cannot catch live-API constraints (§Consequences), so a
live smoke against a scratch workspace is mandatory.

### 5. Include set by exclusion, **derived from `sync-model`** (not a curated or hardcoded list)
Mirror **every `.md` under `dydo/`**, minus:
- `_system/` (framework files) and `agents/` (agent workspaces) — both already gitignored;
- **every dir backing a spine object type** — computed from `sync-model.json`, **never a hardcoded
  list**. Whatever the spine owns as a queryable DATABASE is automatically excluded from the mirror,
  so the two surfaces stay consistent **by construction** as the spine grows and no one has to patch
  a list here again. Per [DR 034](./034-pm-record-taxonomy.md) that set is `campaigns/`, `sprints/`,
  `sprint-tasks/`, `issues/`, `releases/`, `decisions/`, `pitfalls/`, and `future-features/` (the
  confirmed `FutureFeature` type), and — once Brian lands the `Task` type — `tasks/` (incl.
  `tasks/backlog/`) and the `changelog/` archive. `inquisitions/` is **not** mirrored either: DR 034 §8
  dissolves the folder (its reports archive; directed findings flow through the qa-loop / `issues/`);
- `_assets/` and `.obsidian/` (Obsidian machinery; no `.md` content anyway);
- **any file the guard marks off-limits** — the mirror MUST honor the guard's universal off-limits
  patterns and **never** mirror such a file (e.g. `dydo/files-off-limits.md`, the root `index.md`,
  system-state files). These would otherwise become externally editable in Notion and merge back
  through the sync engine's file I/O, **bypassing the `PreToolUse` guard** (the engine's writes are
  not tool calls). This is a hard security invariant, not a nicety.

Special-case: `_index.md` / `index.md` are **consumed as their folder's page body**, not skipped
(the spine loader's blanket `_`-prefix skip does not apply here) — **unless** the file is off-limits,
in which case it is omitted and the folder/root page is a bare container with no body.

**Taxonomy is consumed here, not decided here.** *Which* dirs are queryable spine DBs vs. browsable
docs (still open for `tasks/`, `backlog/`, `changelog/`, …) is a cross-cutting decision that feeds
*both* the spine and this mirror. Because the mirror derives its exclusion from `sync-model`, this
record does not fix that taxonomy — it consumes whatever `sync-model` declares. The taxonomy is
owned by a **separate decision** (pending — see the doc-taxonomy DR).

### 6. Identity, store, and deletion
Key a store by **repo path** → `{ pageId, base snapshot }`, using 025's `BaseSnapshotStore` under
`_system/.local/sync/notion-docs/snapshot.json` (adapter name `notion-docs`; the `.local` tree is
gitignored, so the shadow is never itself synced). **Delete = archive** the Notion page
(`UpdatePage { Archived = true }` — soft, recoverable, mirrors the spine adapter), never hard-delete.
**Rename / move = archive-old + create-new** in v1 (acceptable for a browse mirror; the merge runs
before structural ops each tick so pending body edits merge first). Content-hash rename-detection
that re-points the same `pageId` is optional later polish.

### 7. Ordering
Create children **folders-first, then files, alphabetical**, with an optional frontmatter `nav-order`
override. Notion has no clean block-reorder API, so docs added between full re-mints **append at the
end**; that drift is accepted rather than fought.

### 8. Root page
A dedicated **"Docs"** page under the configured parent (`NotionParentResolver`), a sibling to the
spine databases.

## Consequences & known limitations

- **Lossy converter — SUPERSEDED by [DR 035](./035-docs-body-sync-via-notion-native-markdown-api.md).**
  `NotionBlockConverter` was line-oriented (no inline formatting, nesting, tables, or even blank-line
  fidelity), so the round-trip drifted and the bidirectional merge fabricated conflict markers into
  the canonical repo (issue 0235 — 176 docs corrupted in a live smoke). The fix is **not** converter
  enrichment: body sync moves to Notion's **native Markdown API** (Notion maps blocks↔markdown
  server-side), with a thin Markdig dialect-normalization for the merge and **shadow-file conflicts**
  (never write markers into a canonical file). See DR 035. The two-way body model of this DR is
  unchanged — only the conversion mechanism.
- **100-block append cap.** `AppendBlockChildren` is not currently batched; large docs exceed
  Notion's 100-children-per-request limit. Must chunk. (Same class of live-only constraint that bit
  the PM-board sync — the fake client cannot surface it.)
- **Rate limit.** ~3 req/s throttle → the first full sync of a large tree is slow; skipping
  content-unchanged docs (base snapshot unchanged) keeps re-syncs cheap.
- **Inter-doc links.** `[[wikilinks]]` / relative `.md` links are left as **text** in v1. Rewriting
  them into Notion page-mentions is a fast-follow — cheap-ish because the `path → pageId` map it needs
  already exists — but needs inline rich-text support in the converter, so it does not block v1.
- **Decisions excluded here.** They become a spine database (queryable frontmatter + body-as-page),
  owned by the spine track.

## Status

Proposed. Implementation via **`run-sprint`**, unblocked now that Brian's shared-surface changes have
landed. Slice order: (1) the client page-nesting primitive — `page_id` parent + child-page
create/enumerate/read + 100-block batching, branched from the current surface; (2) the page-tree
adapter + `notion-docs` snapshot store; (3) the `DocsTreeSync` orchestrator (recurse, exclusion set,
`_index.md` folder bodies, archive-on-delete); (4) CLI wiring into `dydo notion sync`; (5) a live
smoke against a scratch workspace.
