---
title: Swarm 0217
area: general
name: swarm-0217
status: stale
created: 2026-07-12T15:05:56.2927352Z
assigned: Brian
needs-human: false
---

# Task: swarm-0217

CODEX swarm fix ROUND 2 — issue 0217. Your round-1 change to `DynaDocs.Tests/coverage/gap_check.py` is PARTIALLY correct: a Claude reviewer EMPIRICALLY VERIFIED that Sync/ detection and Program.cs detection now work (mtime-bump tests: both caught, full gate exit 0). KEEP that work. The review FAILED on ONE remaining blind spot. Self-contained; report then RELEASE YOURSELF. Under the dydo guard + auto mode.

THE PROBLEM: `Templates/` is STILL blind. All entries in `SOURCE_DIRS` are scanned with `SOURCE_GLOBS = ["*.cs"]` (gap_check.py line 53, used in `_find_changed_files_since` lines 605-606). But `Templates/` contains ZERO `.cs` files — it's 40 `.md`, 1 `.json`, 1 `.svg`, 1 `.template`. So adding `Templates` to `SOURCE_DIRS` (line 51) is a NO-OP: `d.rglob("*.cs")` under Templates matches nothing. The reviewer confirmed empirically: bumping the mtime of `Templates/about-dynadocs.template.md` and running gap_check still printed "fresh=True ... no source/test changes detected" — a template edit is invisible, the stale coverage XML is re-scored, false green. Worse, `SOURCE_DIRS` now FALSELY advertises that Templates is covered, so the next reader assumes the blind spot is closed.
Why it matters: `DynaDocs.csproj` has `<EmbeddedResource Include="Templates\**\*" />`, and 10+ test files assert on template CONTENT (TemplateGeneratorTests, TemplateOverrideTests, CliEndToEndTests, CommandDocConsistencyTests, ...). A template edit can flip those tests red — the exact false-green class #0217 is about, on a root the issue explicitly names.

FIX (build ON your round-1 diff — do NOT revert the Sync/Program.cs work):
The problem is that Templates needs a DIFFERENT glob than the code dirs. Introduce a per-directory glob rather than one blanket `SOURCE_GLOBS` for every dir. Since the WHOLE `Templates/` tree is an embedded resource, ANY file change there matters — scan it for ALL files, not just `.cs`. Suggested shape (adapt to the code's style):
1. Keep the `.cs` code dirs on the `*.cs` glob: `Commands, Services, Models, Rules, Utils, Serialization, Sync`.
2. Add a per-dir glob map, e.g. `SOURCE_DIR_GLOBS = {"Templates": ["*"]}`, defaulting other dirs to `SOURCE_GLOBS` (`["*.cs"]`).
3. In `_find_changed_files_since`, pick the glob list per `src_dir` (`SOURCE_DIR_GLOBS.get(src_dir, SOURCE_GLOBS)`). When scanning Templates with `*`, `rglob("*")` yields directories too — filter with `if src.is_file()` before the mtime check. Keep the existing `is_generated(...)` skip.
4. Do NOT change the test-dir scan (still `*.cs`), the `SOURCE_FILES`/Program.cs path (verified good), the skip heuristic, XML-age logic, or the verdict. This is an ADDITIVE change to how Templates is globbed.

VERIFY: You CANNOT run the python gate yourself (sandbox — 0282). Run `python -m py_compile DynaDocs.Tests/coverage/gap_check.py` to confirm no syntax error. Do NOT run gap_check.py itself — the Claude reviewer re-runs the real gate + the Templates mtime-bump test.

REPORT + RELEASE: `dydo msg --to Adele --subject swarm-0217-r2` with: exactly how you made Templates glob all files (the per-dir map + is_file filter), confirmation you left Sync/Program.cs/code-dir/test-dir scanning unchanged, py_compile result, ~time. THEN release yourself (`dydo agent release`).

CONSTRAINTS: The ONLY file you may touch is `DynaDocs.Tests/coverage/gap_check.py`. Do NOT touch any `.cs`, any other test, or any other file. Keep the round-1 Sync/Program.cs additions intact.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)

> Mass-closed 2026-07-16 (DR-041 campaign wrap-up): pre-campaign roster-era task; the work either landed before the pivot or was abandoned with the roster. See git history.
