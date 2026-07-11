---
title: dydo inbox show does not visibly mark read/unread state after dydo read (verify read-ack actually registers vs cosmetic)
id: 275
area: backend
type: issue
severity: low
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
---

# dydo inbox show does not visibly mark read/unread state after dydo read (verify read-ack actually registers vs cosmetic)

c1-8 smoke (2026-07-11): after 'dydo read <inbox-item>' (exit 0, printed content), 'dydo inbox show' still listed the item with no visible read/unread marker. Needs disambiguation (being verified in the c1-8 release test): if 'dydo inbox clear --all' SUCCEEDS afterward, the read registered and this is cosmetic (inbox show paints no marker); if clear is BLOCKED for unread items, dydo read printed-but-did-not-register - a REAL c1-1 read-ack bug that would re-wedge codex release (the exact 0254 symptom). Filed low pending the release-test result; escalate to high if clear wedges. Route to c1-1 follow-up.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)