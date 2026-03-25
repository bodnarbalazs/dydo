---
area: general
name: fix-ci-linux-round2
status: human-reviewed
created: 2026-03-25T17:56:00.7120395Z
assigned: Brian
updated: 2026-03-25T18:12:24.9998962Z
---

# Task: fix-ci-linux-round2

Fixed the single remaining CI failure on Linux: WhoamiConcurrencyTests.FileContention_RetrySucceeds was flaky due to Task.Delay(100) on the thread pool being unreliable on busy CI runners. Replaced with a dedicated Thread + Thread.Sleep(10) for reliable lock release timing. All 3187 tests pass, coverage gate passes.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed the single remaining CI failure on Linux: WhoamiConcurrencyTests.FileContention_RetrySucceeds was flaky due to Task.Delay(100) on the thread pool being unreliable on busy CI runners. Replaced with a dedicated Thread + Thread.Sleep(10) for reliable lock release timing. All 3187 tests pass, coverage gate passes.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-25 18:16
- Result: PASSED
- Notes: LGTM. Clean fix — Task.Delay replaced with dedicated Thread + Thread.Sleep for reliable lock timing on CI. Correct async-to-sync signature change. All 3187 tests pass, coverage gate 129/129.

Awaiting human approval.