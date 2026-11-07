---
area: project
type: changelog
date: 2026-11-05
---

# DYD-560 statechart java `__pycache__` ignored

## Summary

`skills/engineering/statechart/.gitignore` now ignores `scripts/java/__pycache__/`
([DYD-560](https://linear.app/bodnar-balazs/issue/DYD-560), DYD-415 statechart area). The Python
coverage test `DynaDocs.Tests/coverage/tests/test_statechart_java_build.py` imports
`skills/engineering/statechart/scripts/java/build.py`, so the Python row wrote
`scripts/java/__pycache__/build.cpython-314.pyc`. No `.gitignore` covered it, so the facade's Node and
viewer rows then recorded `candidate.dirty=true`, which unbinds their coverage evidence from the
pinned source tree. The DYD-500 round-1 review failed on exactly this (reproduced on candidate
`a4256e9a`).

The fix is ignore-only. No code or test changed.

## Proof

Before the fix, `git status --porcelain` after `gap_check.py gate coverage` listed
`?? skills/engineering/statechart/scripts/java/__pycache__/`. After it, the tree stays clean with no
`PYTHONDONTWRITEBYTECODE` workaround.

## Files Changed

`skills/engineering/statechart/.gitignore` (commit 4856744b), and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
