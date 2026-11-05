---
area: project
type: changelog
date: 2026-11-05
---

# DYD-545 host canaries pre-warm the Claude guard hook

## What changed

Once DYD-500 commits the `dydo guard --host claude` hook in `.claude/settings.json`, every Claude
canary Read runs the guard. The guard's first call of the day writes the gitignored
`dydo/_system/.local/last-validation` into the checkout, and the run fingerprints every byte but the
top-level `.git`, so the candidate fingerprint check failed
([DYD-545](https://linear.app/bodnar-balazs/issue/DYD-545)).

- `DynaDocs.Tests/HostCanaries/run-host-canaries.mjs`: when the Claude leg is selected, it runs the
  hook command once, with a candidate Read payload, before taking the "before" fingerprint. The
  fingerprint still excludes nothing but the top-level `.git`, so any other write still fails the
  run. The project hook stays on.
- The Claude leg fails with a named error when `dydo` is not on the PATH it inherits. The manifest and
  `claude-guard-prewarm.json` record the resolved `dydo` path and version, plus the pre-warm's exit
  code and stderr. A non-zero exit fails the run.
- The fingerprint and the pre-warm move into `DynaDocs.Tests/HostCanaries/candidate-fingerprint.mjs`.
  It is a coverage target, associated with
  `DynaDocs.Tests/coverage/tests/candidate-fingerprint.test.mjs`. The driver keeps its DR 048
  coverage exemption.
- The expected skill count is derived from the source commit's tracked `skills/**/SKILL.md`
  (`git ls-tree`), replacing the hard-coded 29 that went stale with DYD-222. The tree has 41.
- The canary README records the decision, the two rejected options (excluding the guard's
  `.local/` state, disabling project hooks), the `dydo`-on-PATH requirement, and the Codex finding:
  the Codex leg's isolated `CODEX_HOME` loads no project hooks, so the pre-warm does not apply to it.

## Upgrade steps

None. The change is to this repository's test harness only.

## Proof

The node test uses a stub `dydo` that keeps the guard's write contract. Without the pre-warm, a hook
Read changes the fingerprint. With it, hook Reads leave the fingerprint unchanged. After the pre-warm,
these writes still change it: another `.local/` file, a rewritten `last-validation`, a file
elsewhere, and a per-call guard write. With the pre-warm's guard call removed, the pre-warm test
fails. A real-guard run on Linux with the feature build passed: on a materialized candidate, one hook
Read changed the fingerprint, and after a pre-warm two hook Reads left it equal. The full Windows
host-canary run is not reachable here: `prepareCandidate` requires Windows, and the Windows host was
lost in the Linux migration.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
