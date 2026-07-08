---
area: reference
type: reference
---

# Notion Sync API Reference

Working notes for the Notion sync adapter (`Sync/Notion/**`): the API version and data-source model the adapter targets, the endpoints it uses, and the live-API validation constraints that only surface against real Notion — none of which `FakeNotionClient` can catch. Architecture and design live in [Decision 025](../project/decisions/025-notion-sync-architecture.md) and [Decision 029](../project/decisions/029-notion-board-design.md)/[030](../project/decisions/030-board-attention-and-pm-properties.md); this doc is the API-facing detail.

---

## API Essentials

Verified against live docs (2026-06-17). **Exception:** the DR-035 native-markdown rows (marked † below) are doc-sourced from the Notion API reference and are **pending Charlie's live smoke** — they were not part of the 2026-06-17 verification, and the create-with-body markdown field in particular has not been confirmed against a live `page_id` parent.

- **`Notion-Version` header:** `2026-03-11`. The **data-source model** landed in `2025-09-03` and is a breaking change.
- **Data-source model:** a "database" is now a *container* of one or more **data sources** (the actual table of pages). Always resolve database → `data_source_id` before querying or creating.
- **Headers:** `Authorization: Bearer {token}`, `Notion-Version: 2026-03-11`, `Content-Type: application/json`.
- **Rate limit:** ~3 requests/sec average (long-standing Notion limit, not in the upgrade doc) — the adapter must throttle.
- **Access:** token from `DYDO_NOTION_TOKEN` env var or gitignored local storage ([Decision 027](../project/decisions/027-notion-token-storage.md)); never committed or synced. The integration must be *shared* with each target page/DB to see it.

### Endpoints

| Operation | Endpoint |
|-----------|----------|
| Retrieve DB + its data sources | `GET /v1/databases/{database_id}` → `data_sources[]` with `id`/`name` |
| Query a data source | `/v1/data_sources/{data_source_id}/query` with filter + pagination (`start_cursor`/`has_more`/`next_cursor`) — verify the HTTP method at build time; docs have said PATCH but Notion query has historically been **POST** |
| Create a page under a data source | `POST /v1/pages` with `parent: { "type": "data_source_id", "data_source_id": "{id}" }` + `properties` + optional `children` |
| Create a page under a page (nested) **†** | `POST /v1/pages` with `parent: { "type": "page_id", "page_id": "{id}" }` + `properties` + optional `markdown` (create-with-body, DR 035 §1 — mutually exclusive with `children`; the docs mirror carries the body here so create+body is atomic, closing the wipe window of issue 0235). A read-back guard throws if a non-empty body reads back empty, so a silent field-ignore surfaces loudly instead of corrupting — but the field itself is still pending the live smoke. |
| Update page properties | `PATCH /v1/pages/{page_id}` |
| Read page body as markdown **†** | `GET /v1/pages/{page_id}/markdown` → `{ "markdown": "…" }` (Notion maps blocks→markdown server-side, DR 035 — the **docs mirror** body-read path) |
| Replace page body from markdown **†** | `PATCH /v1/pages/{page_id}/markdown` with `{ "markdown": "…", "allow_deleting_content": true }` (Notion maps markdown→blocks server-side, DR 035 — the **docs mirror** body-write path) |
| Read body content — **spine only** | `GET /v1/blocks/{block_id}/children` (block-children API; the docs mirror uses the markdown endpoints above, DR 035 — the converter stays for the spine, issue 0236) |
| Append body content — **spine only** | `PATCH /v1/blocks/{block_id}/children` (block-children API unchanged; docs-mirror bodies go through the markdown endpoints, DR 035) |
| Search | filter accepts `value: "page" \| "data_source"` (no longer `"database"`) |

### Provisioning

Verified empirically under the data-source model:

- **Create database:** `POST /v1/databases` with `parent: {"type":"page_id","page_id":"<id>"}`, `title: [{type:text,text:{content}}]`, `properties: {...}` (Name = `{title:{}}`, select = `{select:{options:[...]}}`, etc.). The response includes `data_sources[0].id` — capture it.
- **Relation property:** `{"relation": {"data_source_id": "<target>", "single_property": {}}}` — references the **data_source_id**, not the database_id. Provision in two passes: create all DBs, then PATCH relation properties referencing the now-known data-source ids.
- **Archive a DB:** `PATCH /v1/databases/{id}` with body `{"in_trash": true}`.
- **Trash traps:** a workspace-level page cannot be un-trashed via the API ("Unarchiving workspace level pages not supported") — the human must restore it from Notion Trash. Creating under a trashed parent fails with "Can't edit block that is archived" — check the page's `in_trash` before building under it. Also, `NotionProvisioner.StillValid()` will happily reuse a **trashed** (soft-deleted, still API-retrievable) database instead of re-minting — clear `dydo/_system/.local/notion/provision.json` to force a fresh mint.

Docs: developers.notion.com/reference/versioning · developers.notion.com/docs/upgrade-guide-2025-09-03 · developers.notion.com/llms.txt (full index).

**Adapter shape:** direct `HttpClient` + source-generated JSON DTOs behind a generic `ISyncAdapter` ([Decision 025](../project/decisions/025-notion-sync-architecture.md) §6) — no third-party SDK (Native AOT + loose coupling). Model only the slice above.

---

## Live-API Validation Constraints

The PM-board sync's first live run (2026-07-06) surfaced four latent constraints, all verified by live `curl` bisection against real data sources. **None are catchable by `FakeNotionClient`** — it treats expressions and bodies as opaque strings — so changes in these areas need a live smoke test, not just the fake-backed suite.

1. **A formula expression cannot reference another formula property.** `prop("<anotherFormula>")` → `400 "Type error with formula"` (opaque — the error names no property). Proven minimal: referencing a trivial `1 > 0` formula fails. Rollup-over-formula, formula-over-rollup, and `dateBetween` over a date-rollup all *do* work. Fix: **inline** the referenced formula's body. This broke all five `attention` formulas (they read `stale`/`health`).
2. **Code-fence language must be in Notion's fixed vocabulary.** `csharp` → 400 (Notion spells it `c#`); `text` and `pwsh` are also rejected. `NotionBlockConverter.NormalizeLanguage` alias-maps and falls back to `plain text` for anything unknown.
3. **A rich-text run's content caps at 2000 characters** (a 2065-char paragraph 400'd). A block holds many runs and `Flatten` concatenates them, so `NotionRichText.Of` splits into ≤2000-char runs (surrogate-safe).
4. **Issue docs had no `title:` frontmatter** — their title is the H1, so the by-name title map left the Notion title blank. Fixed by conforming issues to the universal `title:` convention: `IssueCreateHandler` emits it and the existing corpus was backfilled from each H1. (Rejected alternative: H1-fallback in the adapter — true non-round-tripping needs reconcile-engine changes, and a uniform read-drop breaks the two-way title sync campaigns and sprints rely on.)

`NotionProvisioner` re-throws schema-push failures tagged with the object type, property, and expression — turning Notion's context-free 400s into actionable errors.

---

## Related

- [Decision 025](../project/decisions/025-notion-sync-architecture.md) — Notion sync architecture
- [Decision 027](../project/decisions/027-notion-token-storage.md) — Token storage
- [Decision 029](../project/decisions/029-notion-board-design.md) — Board design
- [Decision 030](../project/decisions/030-board-attention-and-pm-properties.md) — Attention and PM properties
- [Decision 035](../project/decisions/035-docs-body-sync-via-notion-native-markdown-api.md) — Docs body sync via native Markdown API
- [Configuration Reference](./configuration.md) — dydo configuration
