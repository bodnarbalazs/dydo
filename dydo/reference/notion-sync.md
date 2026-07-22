---
area: reference
type: reference
---

# Notion Sync API Reference

Working notes for the Notion sync adapter (`Sync/Notion/**`): the API version and data-source model the adapter targets, the endpoints it uses, and the live-API validation constraints that only surface against real Notion — none of which `FakeNotionClient` can catch. Architecture and design live in [Decision 025](../project/decisions/025-notion-sync-architecture.md) and [Decision 029](../project/decisions/029-notion-board-design.md)/[030](../project/decisions/030-board-attention-and-pm-properties.md); this doc is the API-facing detail.

---

## API Essentials

Verified against live docs (2026-06-17). **Exception:** the DR-035 native-markdown rows (marked † below) were confirmed by the 2026-07-09 live smoke against the real dydo root; the endpoints, create-with-body, PATCH shape, and child-safety behavior are landed and correct ([Decision 035](../project/decisions/035-docs-body-sync-via-notion-native-markdown-api.md)).

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
| Create a page under a page (nested) **†** | `POST /v1/pages` with `parent: { "type": "page_id", "page_id": "{id}" }` + `properties` + optional `markdown` (create-with-body, DR 035 §1 — mutually exclusive with `children`; the docs mirror carries the body here so create+body is atomic, closing the wipe window of issue 0235). A read-back guard throws if a non-empty body reads back empty, so a silent field-ignore surfaces loudly instead of corrupting; create-with-body was confirmed by the 2026-07-09 live smoke ([Decision 035](../project/decisions/035-docs-body-sync-via-notion-native-markdown-api.md)). |
| Update page properties | `PATCH /v1/pages/{page_id}` |
| Read page body as markdown **†** | `GET /v1/pages/{page_id}/markdown` → `{ "object": "page_markdown", "id", "markdown", "truncated", "unknown_block_ids" }` (Notion maps blocks→markdown server-side, DR 035 — the **docs mirror** body-read path). `truncated: true` means the body exceeded Notion's ~20k-block export ceiling and was cut short — reuse the last-synced body, never persist a truncated read. The export also emits each **child page** as a `<page url="…">title</page>` tag; those are structure (repo-owned via the filesystem tree, DR 035 §3), stripped on read so they never enter a body compare or a canonical file (issue 0235). |
| Replace page body from markdown **†** | `PATCH /v1/pages/{page_id}/markdown` — a **discriminated command**, NOT a flat object: `{ "type": "replace_content", "replace_content": { "new_str": "…", "allow_deleting_content": <bool> } }` (the markdown is `new_str`; a bare `{ markdown }` is rejected with `body.type should be defined`). For a folder page, `allow_deleting_content: false` is child-safe **only when** `new_str` re-appends its existing child items as `<page url="…">title</page>` tags; otherwise Notion rejects the write with 400. Alternatively, `allow_deleting_content: true` permits the write but can delete the child pages. Use `true` only for a leaf page. Notion maps markdown→blocks server-side, DR 035 — the **docs mirror** body-write path. |
| Read body content — **spine only** | `GET /v1/blocks/{block_id}/children` (block-children API; the docs mirror uses the markdown endpoints above, DR 035 — the converter stays for the spine, issue 0236) |
| Append body content — **spine only** | `PATCH /v1/blocks/{block_id}/children` (block-children API unchanged; docs-mirror bodies go through the markdown endpoints, DR 035) |
| Search | filter accepts `value: "page" \| "data_source"` (no longer `"database"`) |

### Provisioning

Verified empirically under the data-source model:

- **Create database:** `POST /v1/databases` with `parent: {"type":"page_id","page_id":"<id>"}`, `title: [{type:text,text:{content}}]`, `properties: {...}` (Name = `{title:{}}`, select = `{select:{options:[...]}}`, etc.). The response includes `data_sources[0].id` — capture it.
- **Relation property:** `{"relation": {"data_source_id": "<target>", "single_property": {}}}` — references the **data_source_id**, not the database_id. Provision in two passes: create all DBs, then PATCH relation properties referencing the now-known data-source ids.
- **Archive a DB:** `PATCH /v1/databases/{id}` with body `{"in_trash": true}`.
- **Trash traps:** a workspace-level page cannot be un-trashed via the API ("Unarchiving workspace level pages not supported") — the human must restore it from Notion Trash. Creating under a trashed parent fails with "Can't edit block that is archived" — check the page's `in_trash` before building under it. `NotionProvisioner.StillValid()` now detects a **trashed** (soft-deleted, still API-retrievable) database via its `in_trash`/`archived` flags and re-mints via the 404 path automatically (ns-3), so no manual `provision-<hash8>.json` clear is needed for a trashed DB — the un-trash-via-API limitation on the parent PAGE above still stands.

### State files

All spine state under the gitignored `dydo/_system/.local/` tree is **parent-page-scoped** (issue 0257), keyed by `ParentPageKey.Hash8(parentPageId)` — the same helper `DocsTreeSync.SnapshotAdapterName` uses for the docs mirror — so a scratch `--parent-page` target's state is disjoint from the configured board's and a `notion reset --parent-page <scratch>` can never archive or poison the real board. `NotionSpineState` is the one place that resolves these paths (for both `sync` and `reset`). The parent id is canonicalized first (`ParentPageKey.Normalize`: lowercase, strip dashes), so the dashed and undashed forms of the same page id resolve to one state and an override equal to the configured page — in either form — counts as non-override.

- **Provision state:** `notion/provision-<hash8>.json` — the tracked `{objectType → databaseId, dataSourceId}` per parent.
- **Base snapshots:** `sync/notion-<hash8>-<type>/snapshot.json` — the per-type 3-way-merge base per parent.
- **Migration:** the first non-dry configured run renames the legacy project-scoped files (`notion/provision.json`, `sync/notion-<type>/snapshot.json`) into the scoped names, once, logging one line per file. A `--parent-page` override equal to the configured page counts as non-override (it migrates); an override to any other page starts clean.

Docs: developers.notion.com/reference/versioning · developers.notion.com/docs/upgrade-guide-2025-09-03 · developers.notion.com/llms.txt (full index).

**Adapter shape:** direct `HttpClient` + source-generated JSON DTOs behind a generic `ISyncAdapter` ([Decision 025](../project/decisions/025-notion-sync-architecture.md) §6) — no third-party SDK (Native AOT + loose coupling). Model only the slice above.

---

## Conflict shadows (PM spine)

When the spine reconcile hits a genuine two-sided edit it can't auto-merge — the same line changed differently in the repo and on the board — it **never** writes conflict markers into the canonical PM file (the corruption class of issues [0235](../project/issues/0235-docs-mirror-bidirectional-body-sync-corrupts-repo-with-phantom-conflicts-from-lossy-converter.md)/[0236](../project/issues/resolved/0236-pm-spine-body-sync-shares-the-same-lossy-converter-phantom-conflict-risk-latent.md)/0291). Instead it diverts the conflicted body to a **shadow file** and leaves the canonical file byte-identical to your repo edit, un-pushed, with the base snapshot un-advanced (so the conflict re-detects every sync until resolved). Each diverted conflict is reported with both paths ([Decision 035](../project/decisions/035-docs-body-sync-via-notion-native-markdown-api.md) §4/§5, the spine sibling of the docs-mirror shadow tree).

- **Shadow path:** `dydo/_system/notion_sync_spine/<type>/<name>.md` (under `_system`, so it is outside every type's own dir and never re-read as a PM row — a diverted conflict can never cascade back through the sync). It is a **sibling** of the docs mirror's `_system/notion_sync/` shadow root, never nested inside it, so the docs mirror's promote pass never sweeps up a spine shadow. The shadow holds the 3-way merge with `<<<<<<< repo` / `>>>>>>> external` markers so both sides are visible.

### Resolving a spine conflict

Open the shadow file, then pick a resolution and re-run `dydo notion sync`:

- **Take remote (adopt Notion's version):** edit the canonical PM file to Notion's side of the markers — the block below `=======` in the shadow — then delete the shadow (never copy the marker lines themselves). Re-run: with the two sides aligned the reconcile converges clean, no markers ever touch the canonical file.
- **Take local (keep your repo edit):** your edit already sits in the canonical file untouched. Resolve the shadow to that same content (markers removed) so the next sync promotes it and **repo wins** — the base is aligned to the current board body and your version is pushed over it. Deleting the shadow without aligning the canonical only re-diverts the still-live two-sided edit, so resolve it rather than merely deleting it.
- **Hand-merge:** edit the shadow to the final body you want (markers removed) and re-run; it is promoted onto the canonical file and pushed (repo-wins), converging in one tick.

A shadow that still carries markers is treated as unresolved and left alone — the reconcile re-derives the same conflict deterministically until you finish. Promotion reads the board once to align the base; if that page was archived/trashed while the conflict sat unresolved, the alignment is skipped (the reconcile resurrects the doc from your repo edit) rather than wedging the type's sync.

---

## Live-API Validation Constraints

The PM-board sync's first live run (2026-07-06) and the docs-body live smoke (2026-07-09) surfaced the constraints below against real Notion resources. **None are catchable by `FakeNotionClient`** — it treats expressions and bodies as opaque strings — so changes in these areas need a live smoke test, not just the fake-backed suite. The **token-gated live suite** (below) makes each constraint testable on demand.

1. **A formula expression cannot reference another formula property.** `prop("<anotherFormula>")` → `400 "Type error with formula"` (opaque — the error names no property). Proven minimal: referencing a trivial `1 > 0` formula fails. Rollup-over-formula, formula-over-rollup, and `dateBetween` over a date-rollup all *do* work. Fix: **inline** the referenced formula's body. This broke all five `attention` formulas (they read `stale`/`health`).
2. **Code-fence language must be in Notion's fixed vocabulary.** `csharp` → 400 (Notion spells it `c#`); `text` and `pwsh` are also rejected. `NotionBlockConverter.NormalizeLanguage` alias-maps and falls back to `plain text` for anything unknown.
3. **A rich-text run's content caps at 2000 characters** (a 2065-char paragraph 400'd). A block holds many runs and `Flatten` concatenates them, so `NotionRichText.Of` splits into ≤2000-char runs (surrogate-safe).
4. **Issue docs had no `title:` frontmatter** — their title is the H1, so the by-name title map left the Notion title blank. Fixed by conforming issues to the universal `title:` convention: `IssueCreateHandler` emits it and the existing corpus was backfilled from each H1. (Rejected alternative: H1-fallback in the adapter — true non-round-tripping needs reconcile-engine changes, and a uniform read-drop breaks the two-way title sync campaigns and sprints rely on.)
5. **A folder-body `replace_content` without child-page tags is rejected.** With `allow_deleting_content: false`, a folder-page write that omits its existing `<page url="…">title</page>` child items returns 400; re-append those tags to `new_str` or set `allow_deleting_content: true`. The former preserves the children and is the required folder-write shape ([Decision 035](../project/decisions/035-docs-body-sync-via-notion-native-markdown-api.md)).
6. **The native Markdown API round-trip is lossy without dialect convergence.** Notion extracts a leading H1 as the page title, adds backslash escapes, and collapses blank lines (as well as changing list indentation); without H1-as-title, escape, whitespace, and indent normalization, the read-back becomes a phantom external edit and can overwrite a canonical file — the corruption class that hit DR-040 ([Decision 035](../project/decisions/035-docs-body-sync-via-notion-native-markdown-api.md)).

`NotionProvisioner` re-throws schema-push failures tagged with the object type, property, and expression — turning Notion's context-free 400s into actionable errors.

---

## Live Smoke Harness

A token-gated live test suite (ns-9) exercises the spine's live constraints above against real Notion, from inside the normal test project (`DynaDocs.Tests/Sync/Notion/Live/`) but wired into **nothing in CI**. Constraints 5 (folder-body child-tag rule) and 6 (native-markdown round-trip losses) are **docs-mirror** behavior, out of this sprint's spine-first scope, and are not covered by this suite. Each test carries `[Trait("Category", "notion-live")]` and provisions into its own uniquely named child page (`smoke-<utcstamp>-<rand4>`) under the test parent, archiving it in teardown (best-effort — a leaked page is visible in the scratch parent).

**Gating (the `[NotionLiveFact]` contract).** Fixtures read two env vars:

- `DYDO_NOTION_TEST_TOKEN` — a Notion integration token (the integration must be shared with the target parent page).
- `DYDO_NOTION_TEST_PARENT` — the parent page id the smoke child pages are created under.

**Both unset ⇒ every live test is reported skipped** (the fake suite stays green in CI). **Either one set but the pair incomplete ⇒ the fixture throws and the tests FAIL loudly** — a half-configured live run never silently passes zero real tests (unlike `dydo notion sync`, which exits success on missing config). A complete-but-wrong pair fails when the first real API call is rejected.

**Live testing is sanctioned in prod** (balazs, 2026-07-20): the token/parent may target the real workspace — `dydo notion reset` + git restore is the recovery path — so a dedicated scratch page is optional.

**Invocation** (never run in CI — needs a real token):

```
DYDO_NOTION_TEST_TOKEN=<token> DYDO_NOTION_TEST_PARENT=<page-id> \
  dotnet test --filter Category=notion-live
```

The suite covers, one focused test each: spine provisioning of all seven types with formulas accepted; the 0290 title fallback; the 0291 >100-block create-then-append (whose body also carries a >2000-char run, covering constraint 3); a 3-deep nested list (ns-6); every code-fence language alias; the 0278 FutureFeature title + status options; 0257 reset scoping (scratch reset leaves a second parent's state file untouched); the ns-5 recovery DTO wire shapes (`SearchDataSources` computed name + `parent.database_id`, `ListViews` id-only bare refs — names come from `RetrieveView(id)`, `RetrieveDatabase` parent); a table + quote + `[!missing]` body round-trip; and a >100-row table landing via row-batching (issue 0299 F19).

## Live Acceptance Run (2026-07-21, ns-10)

First real-workspace run of the stabilized engine against the actual dydo board (parent `392798c3…`). **8/10 live tests passed on first contact; the harness caught the two the fake could not**, each a wire shape modeled from inference in a previously-dead DTO — fixed against probed ground truth, and the fake was tightened to reject the wrong shape suite-wide:

- **Nested children** must serialize *inside* the type payload (`{type, bulleted_list_item:{…, children}}`), never as a top-level block `children` field (live 400 `body.children[0].children should be not present`).
- **View list** (`GET /v1/views`) returns id-only refs — names require `RetrieveView(id)`; name-based view adoption now retrieves each.
- **Search hit** title is a rich-text array under `title`, not a `name` string.

The real-board sync then surfaced live-only constraints the fake still could not see, each fixed:

- **`gate-result` was a `select`** whose values are free-prose verdict sentences containing commas → `Invalid select option, commas not allowed`. Retyped to `rich_text`; the `health` and `attention` formulas match it case-insensitively with `test(prop("gate-result"), "(?i)fail")` (issue 0299 F4 — the formula dialect supports `test()` regex; `lower()` does NOT exist, live-probed). Known limitation: `test()` still substring-matches "fail" inside passing prose like "no failures", flagging a false Off Track — accepted as the safe direction for a health flag.
- **Field/body equilibrium (four asymmetry classes, issue 0299)**: a full reset+push then `dydo notion sync --dry-run` initially planned rewrites for 196 of 397 records; driven to **zero** by canonicalizing absent==empty==default (checkbox/scalar schema-default echoes), recognizing the synthesized-title echo, preserving the local body on field-only writes, and comparing fields order-insensitively (frontmatter order vs. Notion's canonical echo order). See the ns-10-core commit and issue 0299.
- **Table row-batching** (issue 0299 F19, live-confirmed 2026-07-22): appending `table_row` children to an EXISTING table via `PATCH /blocks/{table_id}/children` works, so a table wider than the 100-row per-request cap now creates with its first 100 rows inline and appends the remainder to the returned table block id in ≤100 chunks — the old `GuardTableWidth` loud-abort is lifted.
- **Property clears** (issue 0299 F5, live-probed): a local clear of a `select`/`date`/`number`/`url` pushes the explicit clear shape (`{"select": null}`, `{"date": null}`, …; `{"rich_text": []}` for rich_text) on an update, so a blanked field is removed on the board instead of silently reverting.

**Result:** `dydo notion sync --dry-run` plans **zero actions across all 397 spine records** — genuine board↔repo equilibrium. Recovery note confirmed live: local files degraded by a mid-debug apply were restored from git and the board rebuilt from repo canon, exactly the sanctioned "reset + git restore" path.

### Slice board rename (ns-12) and a further wire truth

Changing the Slice type's `notionTitle` `"Sprint Tasks"` → `"Slices"` and re-syncing fired ns-11's additive rename **in place** — the data source kept its id (no re-mint), the board title now reads "Slices", relations intact, dry-run back to zero. But confirming it exposed one more shape the fake had modeled wrong: **the full data-source retrieve (`GET /v1/data_sources/{id}`) carries its title as a rich-text array under `title`, not a flat `name` string** (only a database's `data_sources[]` ref uses a flat `name`; a SEARCH hit, like the retrieve, carries a rich-text `title` array — no `name` key). ns-11's F1 rename-seed read `NotionDataSource.Name` from the `name` key, so it silently read null on a live retrieve — dormant, degrade-safe (null ⇒ seed from model), and never exercised on this board (every type was fresh-minted with a recorded title). `NotionDataSource` now reads `title` and flattens it, so the F1 seed works for a genuinely pre-ns-11 board.

---

## Related

- [Decision 025](../project/decisions/025-notion-sync-architecture.md) — Notion sync architecture
- [Decision 027](../project/decisions/027-notion-token-storage.md) — Token storage
- [Decision 029](../project/decisions/029-notion-board-design.md) — Board design
- [Decision 030](../project/decisions/030-board-attention-and-pm-properties.md) — Attention and PM properties
- [Decision 035](../project/decisions/035-docs-body-sync-via-notion-native-markdown-api.md) — Docs body sync via native Markdown API
- [Configuration Reference](./configuration.md) — dydo configuration
