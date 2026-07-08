---
area: backend
name: adopt-0227-0231-codex-launch-resume
status: review-pending
created: 2026-07-08T10:34:44.7865727Z
assigned: Grace
assigned-vendor: claude
assigned-model: unknown
updated: 2026-07-08T18:59:15.4226207Z
---

# Task: adopt-0227-0231-codex-launch-resume

Adopt orphaned slice: Codex executable resolution (0227) + host-aware watchdog resume (0231) incl. DispatchService pre-flight, CRAP gate; review, finish, land

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Adopted, reviewed (incl. codex resume subcommand fix verified against the official CLI reference), sprint-audited, landed as de0d63f: per-platform executable resolution with WindowsApps alias rejection, host-aware watchdog resume, dispatch pre-flight, hijack + resolution regression tests; gap_check 168/168. Issues 0227/0231 resolved (0231 caveat rides on 0233 smoke test).