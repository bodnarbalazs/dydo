---
id: 46
area: backend
type: issue
severity: low
status: resolved
found-by: inquisition
date: 2026-04-08
resolved-date: 2026-04-27
---

# GlobMatcher recompiles regex on every call without caching

Resolved low-severity perf finding: `GlobMatcher.IsMatch` rebuilt the underlying `Regex` on every call. Fixed by caching compiled regexes in a static `ConcurrentDictionary`; cherry-picked from Emma's recovery branch as `653e102`.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

GlobMatcher now caches compiled regexes via static ConcurrentDictionary. IsMatch no longer rebuilds Regex per call. Cherry-picked from Emma's recovery branch onto master as 653e102 (originally 9e182e7). Verified by CI run 24998191977.