---
area: platform
name: investigate-printinboxitem-test-regression
status: human-reviewed
created: 2026-04-18T19:15:03.0125476Z
assigned: Brian
updated: 2026-04-18T19:28:27.8258629Z
---

# Task: investigate-printinboxitem-test-regression

Investigate why InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath fails: output has From/Task header but is missing the File: line. Determine if InboxService.PrintInboxItem regressed or the test is stale.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

No-op review: investigation concluded no regression exists. All 3 InboxServiceTests pass on HEAD 580515e; gap_check 136/136 clean. Services/InboxService.cs:192-197 correctly emits the 'File:' line; DynaDocs.Tests/Services/InboxServiceTests.cs:9-24 asserts correctly. The 2026-04-02 failure reported in brief was a console-capture race fixed later by 41b3503 and 0a6e930. No code changes made. Please verify by running: python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~InboxServiceTests — then close out.

## Code Review

- Reviewed by: Grace
- Date: 2026-04-18 19:33
- Result: PASSED
- Notes: Verified Emma's no-op investigation. InboxService.cs:192-197 correctly emits 'File:' line; InboxServiceTests.cs:9-24 asserts correctly. Targeted run: 3/3 InboxServiceTests passed. gap_check: 136/136 modules passing (0 failures). No regression present on HEAD 580515e.

Awaiting human approval.