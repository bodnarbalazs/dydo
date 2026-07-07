---
id: 220
area: general
type: issue
severity: high
status: open
found-by: manual
date: 2026-07-07
---

# Docs mirror creates ~N pages then dies on Notion 400 (cannot edit page on archived-ancestor block) — live-only regression, runs BY DEFAULT in dydo notion sync, breaks ALL notion-sync users

Docs mirror (`DocsTreeSync`, core landed `6f5bbf0`) failed a live smoke against an empty scratch page: created ~123 pages, then a Notion 400 "cannot edit page on a block with an archived ancestor." The mirror runs BY DEFAULT in `dydo notion sync` (spine + mirror both run unless `--spine-only`/`--docs-only`), so IF the failure is intrinsic, every user's sync would create N pages then exit 2 — a broad regression. Caught only by live smoke; `FakeNotionClient` + two code reviews missed it.

## This specific failure: CONFIRMED external (not a mirror bug) — title's "breaks ALL users" is a false alarm

Root-cause telemetry: the halt occurred during the **pure-CREATE phase, which makes zero archive calls** → the archived ancestor was **pre-existing/external**, not the mirror archiving its own page. balazs had deleted a leaf mid-run (saw structure created, leaves still empty mid-process) — a Notion delete = archive = exactly this. So the "creates ~123 then dies for everyone by default" framing does **not** hold. Severity downgraded critical → high.

## But the diagnostic found GENUINE deterministic bugs (being fixed)

1. **Archive-ordering:** any ordinary sync that deletes a folder PLUS a nested doc in one commit archives the ancestor **before** the descendant → the same 400. Fix: archive **children before parents**.
2. **Robustness:** a single externally-archived page mid-run wedges the whole sync (hard exit 2). Fix: wrap each archive in try/catch.
3. **Snapshot-path landmine:** scratch and prod share one snapshot file. Fix: scope the snapshot **per parent page**.
4. **Inaccurate dry-run** archive prediction. Fix: correct it.
5. **Test blindness:** `FakeNotionClient` doesn't model archived-ancestor rejection, so the class was uncatchable in tests. Fix: model it.
6. **Gate the mirror off by default** (opt-in `--docs`) — prudence for a brand-new feature + guards mid-sync fragility.

Plus the UX note: the "structure created, leaves empty" window misled the operator into thinking the run was complete.

## Resolution

Fix sprint in flight (Charlie) covering all six. Gates on a hands-off re-smoke of the FIXED code against a FRESH scratch page (current page contaminated), plus code review. Release held until that clean re-smoke + review pass.