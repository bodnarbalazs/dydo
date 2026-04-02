---
area: general
type: changelog
date: 2026-04-02
---

# Task: fix-template-hash-matching

Implemented Option A: normalize content before hashing to fix false user-edit detection from CRLF/LF and BOM differences. Changes: (1) Added NormalizeForHash() that strips BOM and normalizes CRLF→LF. (2) Applied normalization inside ComputeHash and at direct string comparison sites. (3) Added MigrateHashFormat() to convert pre-normalization stored hashes on first run. (4) Added unit tests for normalization and migration, plus integration test for CRLF doc file scenario. All 3390 tests pass, coverage gate clean.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsCollection.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\QueueServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateUpdateTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TemplateCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified


## Review Summary

Implemented Option A: normalize content before hashing to fix false user-edit detection from CRLF/LF and BOM differences. Changes: (1) Added NormalizeForHash() that strips BOM and normalizes CRLF→LF. (2) Applied normalization inside ComputeHash and at direct string comparison sites. (3) Added MigrateHashFormat() to convert pre-normalization stored hashes on first run. (4) Added unit tests for normalization and migration, plus integration test for CRLF doc file scenario. All 3390 tests pass, coverage gate clean.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-02 13:23
- Result: PASSED
- Notes: LGTM. NormalizeForHash is clean and idempotent, ComputeHash correctly normalizes before hashing, MigrateHashFormat handles the upgrade path safely, tests are meaningful and comprehensive. All 3390 tests pass, gap_check clean (132/132 modules).

Awaiting human approval.

## Approval

- Approved: 2026-04-02 18:56
