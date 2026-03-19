---
area: general
type: changelog
date: 2026-03-19
---

# Task: fix-coverage-excludes

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\gap_check.py — Modified


## Review Summary

Applied three fixes to exclude generated code from coverage: (1) Added GeneratedCodeAttribute to ExcludeByAttribute in coverage.runsettings — the primary Coverlet-level fix since both RegexGenerator and JsonSourceGenerator use [GeneratedCode]. (2) Added -filefilters to ReportGenerator command in report.py so HTML reports exclude generated files even if Coverlet misses them. (3) Hardened gap_check.py GENERATED_PATTERNS to match both slash directions for obj paths, consistent with report.py. Verified: gap_check --skip-tests shows 147 source modules with zero generated class leakage.

## Code Review (2026-03-19 14:45)

- Reviewed by: Emma
- Result: FAILED
- Issues: Two of three changes are correct (coverage.runsettings GeneratedCodeAttribute, report.py -filefilters). FAIL: gap_check.py line 46 adds dead code -- backslash pattern \obj\ can never match because resolve_filename() normalizes all paths to forward slashes before is_generated() is called. The /obj/ pattern already handles this. Remove \obj\ from GENERATED_PATTERNS.

Requires rework.

## Approval

- Approved: 2026-03-19 18:47
