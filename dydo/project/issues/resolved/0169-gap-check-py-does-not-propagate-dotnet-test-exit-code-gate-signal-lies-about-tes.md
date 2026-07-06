---
title: gap_check.py does not propagate dotnet test exit code: gate signal lies about test outcome
id: 169
area: general
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-05-05
---

# gap_check.py does not propagate dotnet test exit code: gate signal lies about test outcome

`gap_check.py` exits 0 when the embedded `dotnet test` invocation returns non-zero, as long as the coverage tier-pass check still succeeds — the `tests_ok` boolean from `run_tests()` is captured into a local, used to print a console message, and then never consulted by the exit-code logic. This is the trap that hid the three failures documented in #0165, and a runtime/observability hazard in its own right: every regression test for #0167 (collection misconfiguration) and #0168 (git-helper drain) relies on `gap_check.py` being honest about test outcomes.

## Description

`gap_check.py` exits 0 when the embedded `dotnet test` invocation fails, as long as the coverage tier-pass check still succeeds. The `tests_ok` boolean returned by `run_tests()` is captured into a local variable, used to print a console message, and then never consulted again by the exit-code logic. So a run where `dotnet test` returns non-zero (e.g. one `[FAIL]` on a flake or a real assertion failure) but every module still meets its tier exits 0.

This is the trap that hid the three failures documented in #0165, and a runtime/observability hazard in its own right: every regression test for #0167 (collection misconfiguration) and #0168 (git-helper drain) relies on `gap_check.py` being honest about test outcomes.

## Evidence

- `DynaDocs.Tests/coverage/gap_check.py:347-354` — `run_tests()` correctly returns `False` on non-zero `dotnet test` exit code.
- `DynaDocs.Tests/coverage/gap_check.py:673-686` — `main()` calls `run_tests()`, captures the return into `tests_ok`, prints `"Tests failed. Analyzing available coverage data anyway."`, and proceeds. The `tests_ok` variable is never consulted again.
- `DynaDocs.Tests/coverage/gap_check.py:716-719`:

  ```python
  if has_failures or registry_errors:
      sys.exit(1)
  sys.exit(0)
  ```

  Exit code is conditioned only on coverage-tier failures and registry errors — not on `tests_ok`.

- `DynaDocs.Tests/coverage/run_tests.py:160` — `sys.exit(rc)` propagates dotnet test rc correctly. This is why `run_tests.py` returned non-zero on the inquisition's reproduction when `QueueServiceTests.FindStaleActiveEntries_DetectsDeadPid` failed but `gap_check.py --force-run` exits 0 with the same underlying failure mode.

## Fix path

In `gap_check.py:716-719`, OR `tests_ok` into the exit condition:

```python
if has_failures or registry_errors or not tests_ok:
    sys.exit(1)
sys.exit(0)
```

One-line patch. Optionally also surface a top-line `[RESULT]` string before exit so the human sees "Tests failed AND tier check passed — gate FAILS" rather than scrolling for the buried `Tests failed (exit code 1)` line.

Note: the `tests_ok` variable is only bound when a test run actually happened. When `args.force_run` is False and `is_fresh` is True, no test run is invoked and `tests_ok` is unbound. The fix needs to initialise `tests_ok = True` before the if/else, or check `'tests_ok' in locals()`. (The current code never references `tests_ok` outside the branches that bind it, so it does not trip a NameError today.)

## Related

- #0165 — three failures hidden behind `gap_check`'s exit 0; first prosecution of this trap.
- #0167 — test parallelism / collection misconfiguration. Regression tests for that fix need `gap_check` to be honest.
- Inquisition: `dydo/project/inquisitions/test-runtime-regression.md` Finding #4.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)