---
area: project
type: pitfall
---

# DYD-415: statechart verification on Windows

The pinned pstack compiler needed Windows process ownership, CRLF parsing and
output-directory adaptations before its charts could be trusted on this host.
This evidence uses actual TLC 1.7.4, not the upstream replay fixtures.

This initial record describes `53df22e002a0e5c5e4429cc6908e0b01d4fb6ab3`.
The [correction-hop record](dyd-415-statechart-evidence/fix1/verification.md) retains
both review FAILs and the successor's platform, coverage and static measurements.

The [fix4 proof record](dyd-415-statechart-evidence/fix4/verification.md) completes
the remaining scenario-value mutation pairs and supplies the superseding portable
Coverlet recipe; earlier executed recipes remain historical records. DYD-434's
PowerShell metrics and full changed-code mutation dispositions remain unresolved.

## Inputs and provenance

The implementation starts at `8553172dfaa7601d34f6b884c10350183f9faaa5`, fetched and
confirmed against origin/master before edits. The clean shared host-isolated
checkout was renamed `DYD-415-statechart` and fast-forwarded from `dcac6436`.
The five pre-edit checks passed: ticket branch, isolated repository root, pinned
base ancestry, clean tree, and edits confined to the ticket's named paths.

The complete upstream skill comes from `cursor/plugins`
`1ba91fc25c0977741dd0e267849e6234b037ed0a`, canonical `pstack/skills/statechart`.
Its `skills/engineering/statechart/PROVENANCE.md` records the
bounded upstream issue/PR check, local changes and license. The package and
renderer versions were read, not inferred from the earlier research.

Host: Windows, Node 22.23.2, Microsoft OpenJDK 11.0.16.1, Git Bash, Windows
PowerShell. TLC 1.7.4 has SHA-256
`936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88`.
Renderer: Pele 1.9.0. Test dependency: fast-check 4.5.3; renderer tests use
Mermaid 11.16.0 and jsdom 26.1.0. Local static check: TypeScript 5.9.3 with
`@types/node` 22.18.0. Installs and caches are task-local under TEMP.

## Real red and green

The PowerShell broken case (`dyd-415-statechart-evidence/broken-powershell/report.txt`)
and Git Bash broken case (`dyd-415-statechart-evidence/broken-bash/report.txt`) check
the same bounded counter. `Safe == x <= 2` fails on the one-step `jump` from 0 to 3.
Both launchers return 1 and produce identical reports and Mermaid charts. The
[rendered SVG](dyd-415-statechart-evidence/broken-powershell/chart.svg) was produced
by Pele from that chart, with its `breaks` and `bugPath` classes asserted.
Each case directory retains the model, config, view, raw TLC output, chart,
report and invocation with exit code.

The corrected case (`dyd-415-statechart-evidence/corrected/report.txt`) limits the
increment to 2 and disables the jump. The invariant and config are byte-identical
to the broken case. TLC returns 0, reaches three states and the chart has no bug
mark. These are demonstration models, not a verification claim about dydo product
code. Their checked inputs are committed together with this record; a product
verification would additionally need the source coverage and sync commit.

The bounded 502-state cases prove the upstream chart budget:
failure (`dyd-415-statechart-evidence/budget-broken/report.txt`) draws only the
three-step shortest bug path; pass (`dyd-415-statechart-evidence/budget-passing/report.txt`)
writes no chart. These two cases intentionally use different safety bounds to
exercise both budget outcomes; the corrected-model proof above keeps its invariant.

## Windows regressions

The first native TLC run returned a real invariant error, but the compiler could
not recognize its CRLF frames. The focused red test (`dyd-415-statechart-evidence/crlf-red.txt`)
shows the raw frames being treated as plain text. Normalizing line endings at the
input boundary preserves the checker verdict and fixes the streamed parser.

Native Windows also refuses to rename over an existing empty output directory.
The red Windows test (`dyd-415-statechart-evidence/windows-red.txt`) captures that
failure. The fix removes only an empty directory with non-recursive rmdir before
rename. Content arriving during a run is preserved.

The job guard owns the worker before it may start Java. A detached Node janitor
cleans scratch even if Windows terminates the attached PowerShell host with the
parent console. Lifecycle tests (`dyd-415-statechart-evidence/windows.txt`) prove
normal and killed-parent cleanup using idle processes, plus relative Java/jar
paths and empty output with actual TLC. No CPU-burn fixture was added.

## Gates and reservation

The package's original test command is recorded in provenance. The adopted command
adds only serial test-file execution:
`npm test` resolves to
`node --disable-warning=ExperimentalWarning --test --test-concurrency=1 test/*.test.ts`
from `skills/engineering/statechart/scripts`.

The full suite uses the shared LC FIFO described at
`C:/Users/User/.t3/worktrees/LC/t3-6a75af47/dydo/reference/tools/test-runner.md:303`.
A normal task-local Python driver imports `tools.testing.runner.suite_slot` from
that LC root and books its default live namespace. It keeps the driver alive,
passes `LC_SUITE_SLOT_TICKET` to npm, acquires Heavy without waiting, then creates
`TEMP/dydo-fullsuite.lock` exclusively. It holds no secondary lock while queued.
Booking output (`dyd-415-statechart-evidence/booking.txt`) records nonzero discovery
and the resolved command. Only this run's locks are released.

Native Windows explicitly skips the upstream POSIX CLI and recording harness
(shebang executables, symlinks, signals and process groups). Their source and
assertions remain in the vendored suite. Those skips do not prove POSIX execution;
the independent review must retain that platform limitation. Native acceptance,
plain-TLC comparisons and differential checks supply real Windows evidence.

| Gate | Exit and observed result | Raw evidence under `dyd-415-statechart-evidence/` |
|---|---|---|
| Package `npm test` as resolved above | 0; 342 discovered, 168 passed, 174 explicit platform skips, 0 failed, 0 todo; 248.8 seconds | `upstream-suite.txt` |
| `node --test DynaDocs.Tests/coverage/tests/setup-skills.test.cjs` | 0; 24/24 passed | `setup-tests.txt` |
| `node setup-skills.mjs` | 0; 40 canonical skills; statechart resolves through both hosts, asserted by acceptance | `setup.txt` |
| Four executable acceptance scenarios | 0; 4/4 passed, real TLC and Pele | `acceptance.txt` |
| Feature proof with production stashed | 1; all four cases fail without implementation; restored and green | `feature-without-code.txt` |
| Native Windows lifecycle and path cases | 0; 3/3 passed, also in package suite | `windows.txt` |
| TypeScript 5.9.3 `tsc -p skills/engineering/statechart/scripts/tsconfig.json` | 0; strict, no emit | `typescript.txt` |
| `dydo check` | 0; no errors or warnings | `dydo-check.txt` |
| `git diff --check` and staged equivalent | 0 | `diff-check.txt` |

FIFO ticket `0000000098-5ca78d5d1f71d204`, driver PID 75164, started the suite at
2026-10-29T21:32:52Z after 197 seconds queued. Its exact owner, command and cwd are
in `reservation.json`. The driver exited 0 and released its slot, Heavy and dydo lock.
`run-gates.py` preserves the task-local driver's source; it uses the real LC namespace.
No .NET or Playwright suite was run for this change.

`source-manifest.json` maps every upstream file to its source and candidate SHA-256
and hashes additions and integration files. `proof-manifest.json` hashes the saved
demonstration inputs and outputs. These describe the initial candidate `53df22e`;
the correction record supplies its successor's source identities.
Integration-document hashes use canonical LF text.
Evidence attributes preserve its bytes in fresh checkouts;
skill attributes preserve LF for the shell launcher and byte-compared corpus.
Raw `.out` and `.txt` machine logs are marked binary for Git diffs: their CRLF
frames, indented assertion output and trailing blank lines remain intact. Open
the files to inspect them. PowerShell-captured UTF-16 logs are re-encoded as UTF-8
without changing their text.
The code-absent test is a feature-presence check; the earlier CRLF red test and real
broken/corrected TLC runs provide the behavioral red/green evidence.

## Reproduce

Set `TLA2TOOLS` and `JAVA` to local tools as described by the skill's
`skills/engineering/statechart/references/runtime.md`.
Run `scripts/statechart.ps1` from PowerShell or `bash scripts/statechart` from Git
Bash, relative to the skill folder, on the committed broken case's `Safety.tla`
with its `view.mjs` and a new output directory. Expect exit 1 and a one-step path.
Repeat with the corrected case and expect exit 0. The `Safety.cfg` beside each
model is the config used in the recorded run.

The four cases in `scripts/test/acceptance.feature` are executed by the matching
test module; unrecognized or changed steps fail wiring validation. To retain fresh
proof set `STATECHART_EVIDENCE_DIR` to a new task folder, `STATECHART_BASH` to Git
Bash and `STATECHART_PELE` to Pele 1.9.0's installed entry, then run that module with
`node --test`. Follow the shared reservation contract for a full suite.

## Related

- [Changelog](../changelog/2026/2026-10-29/dyd-415-statechart.md)
- [Control flow](../../understand/control-flow.md)
- [Working-tree contract](../../guides/working-tree-contract.md)
