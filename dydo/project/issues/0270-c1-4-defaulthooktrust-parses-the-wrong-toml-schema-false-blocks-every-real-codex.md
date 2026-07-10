---
title: c1-4 DefaultHookTrust parses the wrong TOML schema - false-BLOCKS every real codex dispatch (v2.0.7 headline feature non-functional)
id: 270
area: backend
type: issue
severity: high
status: open
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-10
---

# c1-4 DefaultHookTrust parses the wrong TOML schema - false-BLOCKS every real codex dispatch (v2.0.7 headline feature non-functional)

v2.0.7 c1-8 live acceptance smoke (2026-07-10) CONFIRMED the sprint-auditor's flagged risk: Services/DispatchPreflight.DefaultHookTrust false-BLOCKS a correctly-trusted codex hook, so NO codex dispatch can proceed against a real codex config - the release's headline feature (codex under the guard) is non-functional as shipped. Root cause: FindHookStateEntry assumes [hooks.state] holds INLINE entries ('path' = { ... }) on lines directly under a bare [hooks.state] header. Real codex writes DOTTED SUB-TABLE headers: [hooks.state.'C:\...\hooks.json:pre_tool_use:0:0'] with trusted_hash/enabled as child lines. The parser's inSection check (line is '[hooks.state]') goes false at the first sub-table header (any line starting with '['), so it never finds the entry -> returns null -> untrusted -> BLOCK. Two sibling defects: ExtractTomlString looks up key 'sha256' but the real key is 'trusted_hash' with value 'sha256:<hex>'; and HashFile returns bare UPPERCASE hex while codex stores 'sha256:'-prefixed LOWERCASE, so even a found entry would mis-compare. Fails SAFE (blocks, never runs unguarded). Fix: parse the dotted-subtable schema (match the [hooks.state.'<path>:<event>:0:0'] header by resolved path, read child trusted_hash/enabled lines until the next header); strip the 'sha256:' prefix and compare case-insensitively to lowercase SHA256 of the file bytes; enabled defaults true unless enabled=false. Interacts with 0269 (regen invalidates the pinned hash - genuine re-trust still needed once parsing works). Found live by balazs+Adele in c1-8; this is THE c1-8 hard finding. Routed to Grace (C1 sprint, on standby).

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)