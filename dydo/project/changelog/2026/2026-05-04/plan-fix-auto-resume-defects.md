---
area: general
type: changelog
date: 2026-05-04
---

# Task: plan-fix-auto-resume-defects

Plan the fix for the auto-resume defect bundle ([#0150](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/0150-auto-resume-on-crash-sometimes-fails-to-trigger-watchdog-misses-dead-claude-process.md) umbrella; [#0151](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0151-watchdog-never-registers-anchors-on-windows-orphan-cap-is-the-only-thing-keeping.md), [#0152](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0152-auto-resume-race-watchdog-fires-duplicate-launches-during-the-resumed-claude-war.md), [#0153](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0153-resume-attempts-is-not-reset-on-same-session-reclaims-so-the-counter-accumulates.md), [#0154](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/resolved/0154-linux-mac-watchdog-dies-via-anchor-gone-when-all-dispatchers-exit-while-leaf-dis.md), [#0144](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/issues/0144-auto-resume-opens-in-new-window-should-reuse-the-original-window-as-a-new-tab-wh.md)) plus the new bad-session-ID fail-fast finding observed live on 2026-05-01. Source material: `dydo/project/inquisitions/auto-resume.md` Section 2026-05-01 (Brian).

## Progress

- [x] Read inquisition source material and trace the relevant code paths
- [x] Synthesize execution plan covering all 5 confirmed findings + the new bad-session-ID finding (`dydo/agents/Charlie/plan-fix-auto-resume-defects.md`)
- [ ] Code-writer dispatch (Noah's call)

## Files Changed

(None — planner output only)

## Review Summary

(Pending)

## Approval

- Approved: 2026-05-04 21:52
