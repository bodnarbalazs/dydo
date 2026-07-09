---
area: general
type: decision
status: proposed
date: 2026-07-08
participants: [balazs, Charlie]
---

# 035 — Docs Body Sync via Notion's Native Markdown API (supersedes DR 025 §6's custom block conversion)

The docs-mirror ([DR 033](./033-docs-notion-nested-page-mirror.md)) stops hand-rolling a lossy
markdown⇄Notion-block converter and instead uses **Notion's native Markdown Content API** to read and
write page bodies. Notion performs the block↔markdown mapping **server-side, faithfully**; we send and
receive markdown strings. This **retires the phantom-conflict corruption class at its root** ([issue
0235](../issues/0235-docs-mirror-bidirectional-body-sync-corrupts-repo-with-phantom-conflicts-from-lossy-converter.md)),
and — when adopted for the spine too — its latent sibling
([0236](../issues/0236-pm-spine-body-sync-shares-the-same-lossy-converter-phantom-conflict-risk-latent.md)).
It **supersedes [DR 025](./025-notion-sync-architecture.md) §6's "custom block↔markdown conversion"**;
§6's *direct-REST, no-SDK, source-generated-JSON, AOT* stance **stands unchanged**.

## Context

DR 025 §6 chose direct REST plus a **best-effort lossy** in-house converter (`NotionBlockConverter`,
block-children endpoints), explicitly accepting body-fidelity loss. That converter renders ordered/
nested lists, inline formatting, tables, and even **blank lines** as flat text, so the round-trip
`markdown → blocks → markdown` **never equals the source**. Combined with DR 033's **bidirectional**
body merge, that drift is read as a two-sided edit and the 3-way merge **fabricates conflicts, writing
conflict markers into the canonical repo files** and compounding on re-runs (0235 — 176 docs
corrupted in a live smoke). Normalizing *around* the lossy converter is treating the symptom.

DR 025 §6 **predates** Notion's **Markdown Content API** (a.k.a. Enhanced / Notion-flavored Markdown),
gated on **`Notion-Version: 2026-03-11`** — the version the adapter already targets. Notion now maps
blocks↔markdown itself, at higher fidelity than any converter we would build, over a plain one-string
field (zero reflection, AOT-clean). The honest fix is to **stop converting** and let Notion do it.

## Decision

### 1. Adopt Notion's native Markdown API for doc body read/write
- Read a page body: `GET /v1/pages/:id/markdown`.
- Write a page body: `PATCH /v1/pages/:id/markdown` (and/or create-with-body via `POST /v1/pages` with
  a `markdown` field, mutually exclusive with `children`).
- The docs mirror stops using `GetBlockChildren` / `AppendBlockChildren` / `DeleteBlock` +
  `NotionBlockConverter` for **body** content. Structure (child-page create under a parent, DR 033) is
  unchanged. Confirm exact endpoint shapes against live Notion docs before building (DR 025 practice).

### 2. Keep direct REST + source-gen JSON (DR 025 §6 stands)
Only §6's *custom conversion* is superseded. The transport is still `HttpClient` + source-generated
`System.Text.Json` DTOs. The read is one `markdown` string in an envelope; the write is a **discriminated
command** — `{ "type": "replace_content", "replace_content": { "new_str": …, "allow_deleting_content": … } }`,
NOT a flat markdown object (a bare `{ markdown }` is rejected `body.type should be defined`) — so the AOT
surface stays tiny. **No third-party Notion SDK** (still a reflection/AOT liability), and the dormant JS
converters (martian, etc.) are not viable in-process.

### 3. Thin dialect-normalization for the 3-way merge (Markdig)
Notion returns **Notion-flavored** markdown (HTML tables, callout/toggle syntax, `<unknown/>` tags for
unsupported blocks), so `local.md` still won't be byte-identical to Notion's echo — but this is now a
**well-defined dialect difference**, not arbitrary lossy noise. Normalize **both** sides to one
canonical form before comparing (parse via **Markdig** — already a dependency, AOT-friendly — to a
normalized AST/dialect), so dialect drift doesn't register as an edit. This is the honest, bounded
normalization; DR 025's base-snapshot + 3-way text merge otherwise stays.

### 4. Shadow-file conflicts — never corrupt a canonical file
A **genuine** conflict (a real two-sided edit that can't auto-merge — now rare) is written to a
**shadow tree** under `dydo/_system/notion_sync/` that mirrors the repo structure and contains **only**
the conflicted files. The canonical repo file is left at its last-good state. The shadow tree is **not
synced**, so a conflict can never cascade back through the mirror. Resolution flow: the human resolves
the shadow file; the next sync promotes it.

### 5. Safety-rail invariant (backstop)
Independent of the above: the sync MUST NEVER write conflict-marker text into a canonical repo file. If
a body about to be persisted/pushed contains merge sentinels, route it to the shadow tree (§4) and
warn; never the canonical file. A pure refuse-on-markers guard, a no-op in normal operation.

## Consequences & caveats (resolve during build)
- **Scope: docs mirror first; spine follows (coordinated with Brian).** The PM spine (`NotionSpineSync`)
  shares `NotionBlockConverter` for row bodies (0236). Migrating it to the markdown API too would retire
  the converter entirely and 0236 — but it touches Brian's live board, so it's a **scheduled** follow-on,
  not part of this sprint. Until then the converter stays for the spine only.
- **Expiring URLs:** read-back image/file URLs are pre-signed and expire — never persist them as
  canonical.
- **Limits:** ~20k-block truncation ceiling (the GET response carries a `truncated` flag — reuse the
  last-synced body, never persist a truncated read); newlines must be real `\n` in the JSON string.
- **`allow_deleting_content` is child-safe, not always-true (amended):** a body replace with
  `allow_deleting_content: true` can trash a page's **child pages** (makenotion/notion-mcp-server#171),
  and folder pages carry the nested docs as children. So the write sends `false` for any page that still
  has child pages and `true` only for a leaf page; on a fresh sync bodies are written at create-time,
  before children exist, so the destructive replace never runs against a page with children.
- **Child-page export tags are stripped on read (amended, issue 0235):** Notion's markdown export emits
  each child page as a `<page url="…">title</page>` tag. Child pages are STRUCTURE (repo-owned via the
  filesystem tree, DR 033 §5 / DR 025) — never body — so those tags are stripped in BOTH normalize
  (compare) and clean-for-persist (write) paths. Without this a folder read-back (prose + child tags)
  reads as a one-sided external edit → external-wins → the tag soup lands on the canonical `_index.md`
  (not shadow-diverted, since it is not a two-sided conflict). Every canonical write goes through
  clean-for-persist, including shadow promotion, so no path can carry the tags to disk.
- **Deletion of `NotionBlockConverter`** for bodies happens only once the spine also migrates.

## Status
Proposed. Sprint (docs mirror only): (a) add the markdown endpoints (+ DTOs) to `INotionClient`/
`NotionClient`/fakes; (b) rewire `DocsPageAdapter` body read/write onto them, off the block converter;
(c) Markdig dialect-normalization for the merge; (d) shadow-file conflict handling + the safety-rail
invariant; (e) a **fidelity test suite** round-tripping real docs (blank lines, ordered/nested lists,
tables, code-with-language, inline formatting) asserting convergence; (f) a live smoke against a scratch
page. Then a co-thinker + Brian round to schedule the spine migration (retiring the converter + 0236).
