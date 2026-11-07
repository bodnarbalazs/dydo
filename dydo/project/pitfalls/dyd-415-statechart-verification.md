---
area: project
type: pitfall
---

# DYD-415: statechart verification

The statechart skill vendors the pstack compiler at `1ba91fc25c0977741dd0e267849e6234b037ed0a`.
Its charts are only as trustworthy as the TLC output they parse, so each platform needs its
own real-TLC proof. This page lists the traps found so far and what a proof run needs.

## Platform traps

- **CRLF frames.** Native Windows TLC writes CRLF. The streamed parser treated those
  frames as plain text until line endings were normalized at the input boundary.
- **Empty output directories.** Windows refuses to rename over an existing empty
  directory. The runtime removes only an empty directory, non-recursively, before the
  rename; content that arrives during a run is kept.
- **Process ownership.** A worker may start Java only after its owner holds it: a
  Job Object on Windows, a registered POSIX lifetime on Linux. Scratch is deleted only
  after an authoritative drain; uncertainty keeps it with a reason.
- **Desktop prompts.** TLC's first-use RMI listener can raise firewall dialogs on
  Windows. Routine use and recurring tests must not need them; see
  [Decision 062](../decisions/062-statechart-invocation-manages-prerequisites.md).

## What a proof run needs

A skipped test is missing proof, not a pass. These inputs decide which tests run:

| Input | Tests that need it |
|---|---|
| `TLA2TOOLS`: the pinned `tla2tools.jar` 1.7.4 | The runtime checks the jar's SHA-256, so the upstream replay suite (`cli.test.ts`), checker isolation, TLC bootstrap and real-TLC tests skip without it, in both TypeScript and .NET |
| `JAVA`: a Java 11+ runtime | Real TLC acceptance, corpus recordings, plain-TLC agreement and differential tests |
| A JDK as `JAVA` | The config-fuzz oracle, which runs in Java's source-file mode; the managed JRE has no compiler, so it skips there |
| `STATECHART_PELE`: the Pele 1.9.0 module entry | Native renderer, SVG acceptance and the renderer tests |
| `pnpm install --frozen-lockfile` in `scripts/`: fast-check 4.5.3, Mermaid 11.16.0 and jsdom 26.1.0 from `pnpm-lock.yaml` | The differential tests, and the renderer tests' Mermaid parse |
| `node setup-skills.mjs`: the `.claude` and `.agents` projections | One native acceptance test, which reads both |
| `STATECHART_TEST_DEPENDENCY_ROOT`: a verified managed source installation | Readiness tests copy the current lock/RID into each test's private temporary directory before running |
| Windows | Job Object lifetime, the probe fixture, `statechart doctor` and native PowerShell acceptance |

The .NET native tests run the `dydo` build beside the test assembly;
`STATECHART_TEST_BINARY` points them at another build, such as a NativeAOT publish.

## Linux proof recipe

Restore the assurance toolchain first, from its locks, as
[Coverage Tools](../../reference/coverage-tools.md#provisioning) lists it. Use the managed copy
that `dydo statechart` installs on first use: run any chart once, then run the exact
`installAndContinue` command its `approval-required` event names. Point each test command at that
copy as a source; change no PATH, JAVA_HOME or profile. Readiness tests never run or re-stamp
the source store: each owns a full copy, including independent receipt files, and removes it
with its test directory. Do not replace that copy with hard links or directory links. A source
receipt re-stamped by another build cannot change the test's receipt assertions. From the
repository root, after supplying the absolute path of a verified source installation:

```bash
deps=${STATECHART_TEST_DEPENDENCY_ROOT:?Set the absolute path of a verified managed source installation}
m=$(echo "$deps"/*/linux-x64)
export JAVA=$m/java/content/jdk-21.0.8+9-jre/bin/java TLA2TOOLS=$m/tlc/tla2tools.jar
export STATECHART_PELE=$m/pele/content/package/dist/pele.js STATECHART_TEST_DEPENDENCY_ROOT=$deps
py=.static-gates/python/bin/python
node setup-skills.mjs
(cd skills/engineering/statechart/scripts && pnpm install --frozen-lockfile && pnpm test)
node --test DynaDocs.Tests/coverage/tests/setup-skills.test.cjs DynaDocs.Tests/coverage/tests/statechart-corpus-views.test.cjs npm/test/statechart.test.cjs
PYTHONPATH=DynaDocs.Tests/coverage/tests "$py" -m unittest test_statechart_java_build
python3 DynaDocs.Tests/coverage/run_tests.py -c Release --filter "FullyQualifiedName~DynaDocs.Tests.Statechart"
```

Run each command under the memory cap and shared-host admission rules in
[Testing Strategy](../../guides/testing-strategy.md#runner-prerequisites-and-shared-hosts).
Set `TMPDIR` to the run's private scratch directory on shared hosts; copies and their cleanup
belong to that run. No test setup should install into or re-stamp another session's store.

Only Windows-only tests and config-fuzz should skip. The managed source survives rebuilds:
a build that keeps the dependency lock re-verifies and re-stamps it on the next run, and a
damaged component comes back as an `approval-required` repair plan. The installed
ready-invocation test re-verifies and re-stamps only its private copy after a payload change;
both the source receipts and the copied content stay unchanged. The isolation regression
restamps a second private source between the ready invocation and its receipt assertion,
without touching the supplied source installation.

## History

The Windows-era investigation, its gate logs and the custody records were removed from the
tree after the move to Linux. They remain at
[67cd2127](https://github.com/bodnarbalazs/dydo/tree/67cd21278872679398479601b99d95125678b7cd/dydo/project/pitfalls/dyd-415-statechart-evidence).
Proof for the current candidate lives on [DYD-415](https://linear.app/bodnar-balazs/issue/DYD-415).

## Related

- [Changelog](../changelog/2026/2026-10-29/dyd-415-statechart.md)
- [Control flow](../../understand/control-flow.md)
- [Working-tree contract](../../guides/working-tree-contract.md)
