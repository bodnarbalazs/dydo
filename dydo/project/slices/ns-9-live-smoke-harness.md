---
title: ns-9 Live Smoke Harness
blocked-by: ns-1-parent-scoped-state, ns-2-deletion-fuse
due:
needs-human: false
priority: Critical
sprint: notion-stabilization
status: backlog
work-type: feature
area: backend
type: context
---

# ns-9 Live Smoke Harness

The root blocker behind every escaped Notion bug (0290/0291/0278/0220): all 27 Notion test files run against `FakeNotionClient`, which treats formulas and bodies as opaque strings — every live-only API constraint is invisible to CI (`dydo/reference/notion-sync.md` says so explicitly). Build a token-gated live test rig so live behavior is testable on demand without ever entering the normal CI path.

## Task

1. Create a live test collection (xunit `[Trait("Category", "notion-live")]` or a separate `DynaDocs.LiveTests` project — prefer the trait inside the existing project unless the coverage gate fights it; check `gap_check.py` behavior for skipped traits) whose fixtures read `DYDO_NOTION_TEST_TOKEN` and `DYDO_NOTION_TEST_PARENT` env vars. **Both absent → every live test skips with a clear reason; either set but invalid → tests FAIL loudly** (no silent no-op — Watch-outs in the sprint root).
2. Fixture pattern: each test class provisions into a uniquely named child page under the test parent (`smoke-<utcstamp>-<rand4>`), runs against the real `NotionClient`, and archives its child page in teardown (best-effort; leaked pages are visible in the scratch parent, acceptable).
3. Cover the fake-invisible classes, each as one focused live test:
   - spine provisioning mints all 7 types; formulas accepted (the inlined attention formulas);
   - page create with title fallback shows a real title (0290's class);
   - >100-block body creates then appends without 400 (0291's class);
   - a 3-deep nested list body lands (ns-6's algorithm against the real API);
   - every language-alias output is accepted by the code-block endpoint;
   - FutureFeature type: title renders, status options present (0278's class);
   - reset against the scratch parent leaves a second parent's state untouched (0257 — state files only, no second live board needed).
4. Wire nothing into CI. Document invocation (`dotnet test --filter Category=notion-live` + env vars) in `dydo/reference/notion-sync.md`, replacing the "manual smoke" note.

## Files

- `DynaDocs.Tests/Sync/Notion/Live/` (new folder) + fixture
- `DynaDocs.Tests/coverage/gap_check.py` — only if skipped-trait handling needs a carve-out (verify first, change minimally)
- `dydo/reference/notion-sync.md`

## Success criteria

- Without env vars: full suite green, live tests reported skipped, gap_check unaffected.
- With deliberately bad token: live tests fail loudly (no silent skip).
- Code review confirms each listed constraint class has exactly one focused live test. (Actually running live is ns-10 — needs the human's token.)
- Full ratchet green.
