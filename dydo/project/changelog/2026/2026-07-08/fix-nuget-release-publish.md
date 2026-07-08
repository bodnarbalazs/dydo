---
area: general
type: changelog
date: 2026-07-08
needs-human: false
---

# Task: fix-nuget-release-publish

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

NuGet publish fix: removed 3 stale committed nupkgs (incl dydo.1.5.0) that the push glob swept in causing 409; gitignored /nupkg/; added --skip-duplicate. Sonnet reviewer PASS. Brief's version-source diagnosis was wrong.

## Approval

- Approved: 2026-07-08 10:15
