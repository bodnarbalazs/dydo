---
title: Release push glob nupkg/*.nupkg sweeps stale committed packages - replace with explicit pack output path
id: 245
area: backend
type: issue
severity: low
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-08
resolved-date: 2026-07-12
---

# Release push glob nupkg/*.nupkg sweeps stale committed packages - replace with explicit pack output path

The v2.0.x 409 already-exists failures were stale committed nupkgs swept in by the nupkg/*.nupkg push glob, not a version-source bug. Mitigations landed (gitignore /nupkg/, --skip-duplicate, stale files removed) but the glob itself remains - push the freshly-packed artifact by explicit path (or clean before pack) so the class of failure is structurally gone rather than masked by skip-duplicate. Routed from auto-memory per DR 038 initial sweep.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-13 (landed 12576f44). release.yml push now targets the explicit freshly-packed versioned nupkg path instead of the nupkg glob, so a stale committed package can never be swept into the push - structurally closes the 409-already-exists class. Codex Grace (Terra), reviewed.