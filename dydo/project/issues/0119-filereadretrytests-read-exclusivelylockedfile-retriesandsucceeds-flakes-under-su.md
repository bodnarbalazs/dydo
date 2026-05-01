---
id: 119
area: backend
type: issue
severity: low
status: open
found-by: review
date: 2026-04-27
---

# FileReadRetryTests.Read_ExclusivelyLockedFile_RetriesAndSucceeds flakes under suite load — timing race between test's Thread.Sleep(80) and FileReadRetry 50ms/150ms backoff

Open low-severity flake report: `FileReadRetryTests.Read_ExclusivelyLockedFile_RetriesAndSucceeds` is sensitive to scheduling under suite load, where the test's `Thread.Sleep(80)` races with the `FileReadRetry` 50ms/150ms backoff and occasionally produces a false negative. Awaiting a fix that decouples the test from real-time backoff (e.g., injecting the backoff schedule).

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)