---
area: general
name: dispatch-wait-integration
status: pending
created: 2026-03-08T13:02:15.0594278Z
assigned: Olivia
---

# Task: dispatch-wait-integration

Add `--wait`/`--no-wait` as required flags on `dydo dispatch`. `--wait` creates a wait marker and enters a poll loop (combining dispatch + wait). `--no-wait` returns immediately with a release hint when appropriate. Add double-dispatch protection, wait marker infrastructure for release blocking, channel isolation in `dydo wait`, and `--cancel` support. Update all templates and docs.

## Progress

- [x] Plan written: `dydo/agents/Olivia/plan-dispatch-wait-integration.md`
- [ ] Step 1: Wait marker infrastructure (AgentRegistry)
- [ ] Step 2: Release blocking on markers
- [ ] Step 3: Double-dispatch protection
- [ ] Step 4: `--wait` / `--no-wait` flags on dispatch
- [ ] Step 5: `--wait` poll loop behavior
- [ ] Step 6: `--no-wait` release hint
- [ ] Step 7: Wait command `--cancel`
- [ ] Step 8: Channel isolation in general wait
- [ ] Step 9: Update existing tests
- [ ] Step 10: New test files
- [ ] Step 11: Documentation updates

## Files Changed

(None yet)

## Review Summary

(Pending)