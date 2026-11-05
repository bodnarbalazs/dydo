---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - Python a run subcommand starts on stdin is refused (unreleased)

## Summary

The guard now refuses a Python program on stdin when a runner's `run` subcommand starts that
Python, as it already refused `uv run python -c`
([DYD-509](https://linear.app/bodnar-balazs/issue/DYD-509)). Before, `uv run python - <<'EOF'`,
`poetry run python3 -` and `printf ... | uv run python` passed, while the docs said every Python
program on stdin was refused.

## Changed

- One shape, for any runner: the runner's first operand is `run`, and Python is the first operand
  after it, with options allowed before each. A word after an option may be that option's value, so
  `uv --directory tools run python -`, `uv run --with requests python -` and
  `conda run -n env python3 -` are refused too
  ([Guard System](../../../../understand/guard-system.md)).
- The refusal and its message are the same as for Python at command position.
- `uv run python script.py`, `uv run python -m pytest`, `poetry run pytest`, `uv pip install ...`,
  `ps aux | grep python` and `xargs uv run python` still pass.
- Remaining limit: Python behind a runner with no `run` subcommand (`uvx python -`,
  `pyenv exec python -`) or behind a second runner that `run` starts (`uv run poetry run python -`)
  is not recognised. The guard stops accidents, not a deliberate route around it.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Files Changed

`Services/BashCommandAnalyzer.cs`, `DynaDocs.Tests/Services/BashCommandAnalyzerTests.cs`,
`dydo/understand/guard-system.md`, `Scaffold/dydo/files-off-limits.md`, and this entry.

## Related

- [dydo 3.6.0 - Merge report waits for GitHub](dyd-535-merge-report.md)
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
