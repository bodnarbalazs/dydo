---
area: reference
type: reference
---

# Notion Sync OSS Survey (2026-07-20)

Survey of the open-source Markdown/Obsidian ↔ Notion sync ecosystem, distilled into portable lessons for dydo's sync engine. Licenses gate what we may port: **MIT projects (martian, notion-to-md, notion-sdk-js, obsidian-importer, docu-notion, Notion.Net) — logic portable with attribution; the EasyChris lineage (obsidian-to-notion, nobsidion, Notional) is GPL-3.0 — patterns only, never code.**

---

## Projects at a glance

| Project | What | State | License | Take |
|---|---|---|---|---|
| [martian](https://github.com/tryfabric/martian) | md → Notion block payloads | dormant; read master not npm | MIT | Best rich-text algorithm; fails at nesting depth ≥3 |
| [notion-to-md](https://github.com/souvikinator/notion-to-md) | Notion → md | active, ~1.7k★ | MIT | Two-phase (fetch tree, then render); annotation ordering |
| [Notional](https://github.com/bryanbans/Notional) | two-way Obsidian sync | active, tiny | GPL-3.0 | The ecosystem's most honest two-way design |
| [notion-sdk-js](https://github.com/makenotion/notion-sdk-js) | official JS client | active | MIT | Retry taxonomy ground truth |
| [Notion.Net](https://github.com/notion-dotnet/notion-sdk-net) | .NET client | active, v5 = API 2025-09-03 | MIT | Retry policy + block polymorphism reference |
| [obsidian-importer](https://github.com/obsidianmd/obsidian-importer) | Notion export → vault | active | MIT | ID-keyed link resolution |
| [docu-notion](https://github.com/sillsdev/docu-notion) | Notion → docs site | active | MIT | Status-gated publish, image caching |
| [obsidian-to-notion](https://github.com/EasyChris/obsidian-to-notion) | push-only | dormant | GPL-3.0 | The delete-and-recreate anti-pattern |

**Ecosystem ceiling:** nobody does three-way merge. The best conflict handling is Notional's dual-timestamp stop-and-ask. dydo's base-snapshot reconcile is already ahead of the field — keep it.

---

## API limits (verified against official docs)

- **Rate:** ~3 req/s average per integration; bursts tolerated. Retry 429 **and 529 `service_overload`** honoring `Retry-After`, exponential backoff with jitter on top.
- **5xx:** retry 500/503 **only for idempotent requests**. A create that dies on 500 may have succeeded server-side — re-query before re-creating.
- **Blocks:** 100 per children array, 1000 elements per payload, 500KB payload. Chunk appends ≤100; the `position` parameter supersedes the deprecated `after` for ordered multi-batch appends.
- **Nesting:** 2 levels of children per request. Correct algorithm: cut the tree at depth 2, append, collect returned first-level block IDs, append grandchildren iteratively (BFS by depth).
- **rich_text:** 2000 chars per `text.content` (and per URL), ~100 items per array — enforce per block, overflowing into sibling paragraphs, not truncation. Split at leaf level so annotations never cross a split; don't split UTF-16 surrogate pairs.
- **Properties:** multi_select / relation / people cap at 100 **per request** (not stored size) — paginate reads, batch writes.

## md → blocks lessons

- Inline conversion via an inherited-annotation accumulator over the AST (maps 1:1 onto a Markdig inline walk).
- Code-fence languages: lowercase → Notion enum check → alias table (`sh/zsh/bash→shell`, `js→javascript`, `yml→yaml`, `golang→go`, `tex→latex`, …) → `'plain text'` fallback.
- Tables: `table_width` from the widest row; pad ragged rows.
- Blockquotes: first paragraph in the quote's own `rich_text` (else Notion renders "Empty quote"); GFM alerts (`[!NOTE]`…) → callouts.
- Images: no inline images in Notion — hoist to sibling blocks; validate URLs and degrade invalid ones to text paragraphs so one bad image never 400s the page.
- Headings 4–6 clamp to `heading_3`; make math conversion opt-in (dollar amounts false-positive).

## Notion → md and round-trip stability

- Apply annotations in fixed order (code → bold → italic → strikethrough), hoisting leading/trailing whitespace outside markers.
- Render unsupported blocks as a **visible `> [!missing]` marker**, never silent drops — silent loss is what makes round-trips diverge invisibly.
- Never persist raw `file.url` values (S3 presigned, ~1h expiry); download or keep external URLs only.
- **Stability recipe:** compare content by hashing the *normalized canonical rendering* produced at last sync; `last_edited_time` is only a cheap pre-filter (it changes on property touches and has coarse granularity). Timestamps say "changed since"; hashes say "changed meaningfully".

## Sync-state and deletion semantics

- Resolve everything by page ID, never by path/title. Renames are title updates; moves are parent changes.
- Update strategy: keep the **page** stable (ID, properties, comments, backlinks), replace only block children. Delete-and-recreate destroys comments/backlinks — the ecosystem's central anti-pattern.
- API delete = archive (`in_trash`) — restorable; treat it as the undo mechanism. Database queries silently omit archived pages — fetch-by-ID before concluding "deleted remotely".
- Sweep the ID map each sync for orphans on both sides; no surveyed project does this well.

## Database (spine) lessons

- Resolve the title property **by type, not by name**.
- Writing an unknown select/multi-select option auto-creates it with a random color: normalize option casing, treat option colors as Notion-owned cosmetics — ignore them in drift detection.
- Unknown property name in a write = `validation_error`: pre-read schema and drop/report unknown keys.
- Relations need target page IDs → two-pass sync (create pages, then fill relations), ≤100 per request.
- docu-notion's publish-gate pattern (a `status` select gating sync, `Slug` overriding path) maps directly onto a spine database.

## Priority reading list

1. martian `src/parser/internal.ts` + `src/notion/common.ts` — annotation flattening, splitting, limits (MIT)
2. notion-to-md `src/notion-to-md.ts` + `src/utils/md.ts` — block→md walk and renderers (MIT)
3. Notional — frontmatter sync-state, dual-timestamp conflicts, deep-nesting append (GPL: patterns only)
4. notion-sdk-js `src/Client.ts` — retry taxonomy, cursor iteration (MIT)
5. Notion.Net — .NET retry policy, block polymorphism, API 2025-09-03 migration reference (MIT)

## Related

- [notion-sync.md](./notion-sync.md) — our live-API constraints and smoke records
- [Notion Stabilization sprint](../project/sprints/notion-stabilization.md) — the plan this survey feeds
