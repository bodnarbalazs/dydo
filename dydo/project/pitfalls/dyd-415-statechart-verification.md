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
| `STATECHART_PELE`: the Pele 1.9.0 module entry | Native renderer and SVG acceptance |
| `STATECHART_TEST_DEPENDENCY_ROOT`: a verified managed installation | The installed ready-invocation test |
| Windows | Job Object lifetime, the probe fixture, `statechart doctor` and native PowerShell acceptance |

The .NET native tests run the `dydo` build beside the test assembly;
`STATECHART_TEST_BINARY` points them at another build, such as a NativeAOT publish.

## Linux proof recipe

Use the managed copy that `dydo statechart` installs on first use: run any chart once,
then run the exact `installAndContinue` command its `approval-required` event names.
Point each test command at that copy; change no PATH, JAVA_HOME or profile.

```bash
deps=~/.local/share/dydo/statechart/deps
m=$(echo "$deps"/*/linux-x64)
export JAVA=$m/java/content/jdk-21.0.8+9-jre/bin/java TLA2TOOLS=$m/tlc/tla2tools.jar
export STATECHART_PELE=$m/pele/content/package/dist/pele.js STATECHART_TEST_DEPENDENCY_ROOT=$deps
node setup-skills.mjs   # one acceptance test reads both host projections
(cd skills/engineering/statechart/scripts && XDG_CACHE_HOME=$(mktemp -d) npm_config_cache=$(mktemp -d) npm test)
python3 DynaDocs.Tests/coverage/run_tests.py -c Release --filter "FullyQualifiedName~DynaDocs.Tests.Statechart"
```

Only Windows-only tests and config-fuzz should skip. The managed copy survives rebuilds:
a build that keeps the dependency lock re-verifies and re-stamps it on the next run, and a
damaged component comes back as an `approval-required` repair plan.

## History

The Windows-era investigation, its gate logs and the custody records were removed from the
tree after the move to Linux. They remain at
[67cd2127](https://github.com/bodnarbalazs/dydo/tree/67cd21278872679398479601b99d95125678b7cd/dydo/project/pitfalls/dyd-415-statechart-evidence).
Proof for the current candidate lives on [DYD-415](https://linear.app/bodnar-balazs/issue/DYD-415).

## Related

- [Changelog](../changelog/2026/2026-10-29/dyd-415-statechart.md)
- [Control flow](../../understand/control-flow.md)
- [Working-tree contract](../../guides/working-tree-contract.md)
