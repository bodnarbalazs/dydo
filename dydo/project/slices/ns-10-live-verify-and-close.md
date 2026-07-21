---
title: ns-10 Live Verification and Issue Closure
blocked-by: ns-9-live-smoke-harness, ns-5-client-retry-nuances, ns-6-depth-limit-append, ns-7-converter-hardening, ns-8-canonical-hash
due:
needs-human: true
priority: Critical
sprint: notion-stabilization
status: done
work-type: chore
area: backend
type: context
---

# ns-10 Live Verification and Issue Closure

The sprint's verification gate: run the ns-9 harness against real Notion with the human-provided scratch credentials, confirm the fixes that have only ever been fake-verified, and close the open issues with live evidence. **Blocked on the human**: needs `DYDO_NOTION_TEST_TOKEN` (integration token) and `DYDO_NOTION_TEST_PARENT` (scratch parent page id) from balazs before starting.

## Task

1. Run the live collection (`dotnet test --filter Category=notion-live` with the env vars). Triage every failure: fix-forward small issues within this slice; anything structural goes back to its owning slice's lane as a reopened review.
2. On a green live run, update and close with live evidence (run date, scratch parent, observed behavior):
   - **0290** (spine titles) — cards show real titles;
   - **0291** (>100-block create) — large body lands chunked;
   - **0278** (FutureFeature title/options) — verify against the *model's* option list (`raw/shaping/promoted/dropped` — note the issue text says `idea`; reconcile the text to the model while closing). **Color half disposition:** under the sprint's locked "colors are Notion-owned" decision, option colors on an already-provisioned board are explicitly WONTFIX (a human recolors in Notion once; sync never touches colors again); a fresh mint gets whatever colors the model's create payload specifies. Record exactly this in the closure;
   - **0257** (reset scoping) — scratch reset leaves other-parent state untouched (ns-1);
   - **0236** (phantom spine conflicts) — sync → no edits → sync is a no-op live (ns-8).
3. Record the smoke run in `dydo/reference/notion-sync.md` (same format as the 2026-07-06/07-09 entries).
4. Also do one manual `dydo notion sync` against the scratch parent and eyeball the board (titles, colors, relations) — automated assertions don't see rendering.
5. Verify the ns-5 recovery wire shapes live: search hit `name` + `parent.database_id`; view list `name`; database retrieve `parent` (used by the CreateDatabase/CreatePage/CreateView adoption recoveries — a wrong key degrades to re-create, never a wrong adopt, but confirm the exact keys live).
6. Verify the ns-11 additive-provisioning wire shapes live (both fake-verified only):
   - **Data-source rename** — `PATCH /v1/data_sources/{id}` with `title: [{type:text,text:{content}}]` (rich-text array, mirroring create) actually renames the data source. A wrong key silently no-ops the rename; confirm the board title changes live.
   - **Select option-union PATCH** — `ApplyModelAdditions` re-sends the existing options WITHOUT their ids (name+color only) plus the new option by name; Notion must match the existing options BY NAME (not id) so their colors/values survive the union. Confirm adding one option leaves the others' colors and existing rows' values intact.
   - **Data-source title on retrieve (F1 wire shape)** — `GET /v1/data_sources/{id}` returns the data source's live title under `name` (the key `NotionDataSource.Name` reads). The additive pass seeds a pre-ns-11 record's title from this so a model rename before the first post-upgrade sync still fires. Confirm the retrieve response actually carries the title under `name`; if it is absent/under a different key, the seed degrades to the model (no rename) — verify which, and adjust the DTO key if live disagrees.
7. Verify two remaining live assumptions the fake cannot settle:
   - **Batch-append an existing table past 100 rows** — `NotionBlockAppender.GuardTableWidth` throws for a table wider than 100 rows because there is no known way to append rows to an already-created table (a `table.children` array caps at 100). Confirm live whether row-batching an existing table IS possible; if so, lift the guard and implement batching, otherwise keep it and record the confirmation.
   - **Markdown `---` imports as a `divider` block** — `NotionLiveBodyRoundTripTests` assumes a thematic break round-trips through Notion's markdown import as a real `divider` block (which the spine converter surfaces as `[!missing]`). Confirm the live import actually produces a `divider` (not a heading/paragraph), so the round-trip assertion rests on observed behavior.

## Files

- `dydo/project/issues/` (the five issues → `resolved/` with resolution sections)
- `dydo/reference/notion-sync.md`

## Success criteria

- Live collection green on a real run; the five issues resolved with live evidence and moved to `resolved/`; smoke run recorded.
- Full (fake) ratchet still green afterward.
