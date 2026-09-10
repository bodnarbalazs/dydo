---
area: reference
type: reference
---

# Coverage and Assurance Tools

`DynaDocs.Tests/coverage/gap_check.py` is the canonical project testing facade. Its adjacent
`gap_check.json` is the schema 1 manifest that declares each stack's test, static, coverage and
mutation adapters. `dydo/reference/gap-check.example.py` is a mechanically derived byte copy of that
runner for other projects; the [Testing Strategy](../guides/testing-strategy.md) holds the copy rule
and the adopted policy.

---

## Operations

Run the facade with the pinned local interpreter, not an arbitrary Python:

```powershell
$py = "dydo/_system/.local/static-gates/python/Scripts/python.exe"
& $py DynaDocs.Tests/coverage/gap_check.py --help
& $py DynaDocs.Tests/coverage/gap_check.py all
& $py DynaDocs.Tests/coverage/gap_check.py test --stack dotnet -- --filter FullyQualifiedName~ParserTests
& $py DynaDocs.Tests/coverage/gap_check.py gate static
& $py DynaDocs.Tests/coverage/gap_check.py gate coverage --stack python,node
& $py DynaDocs.Tests/coverage/gap_check.py gate mutation --since BASE --stack dotnet
& $py DynaDocs.Tests/coverage/gap_check.py capabilities
& $py DynaDocs.Tests/coverage/gap_check.py --force-run
```

`all` runs test rows only. Each `gate` operation runs exactly the named capability, defaulting to
every declared stack in manifest order. `--force-run` is the compatibility full-G operation: it
selects test, static and coverage for every stack — nine rows here — and never selects mutation.

Rows run one at a time, stack by stack and capability by capability. Each row gets a fresh deadline
of 1800 seconds of execution plus 30 seconds of cleanup, and the facade exports `DYDO_ROW_DEADLINE`
and `PYTHON` (the caller's own interpreter) to the child. An interrupted row stops every row after
it.

The facade runs argv arrays without a shell. It appends native arguments only after `test ... --`;
a gate row forwards nothing, and the coverage gate rejects any narrowing of the suite. Executable
paths in an argv row resolve from that stack's working directory without changing the recorded
vector; bare executable names use the platform search rules.

Every started operation writes `<artifactRoot>/run-<epoch-milliseconds>-<8 hex>/result.json` and
prints its path. It records schema, candidate commit and dirty state, operation, selected stacks,
ordered rows and aggregate exit. `DynaDocs.Tests/coverage/.gitignore` ignores `results/`, so every
artifact named on this page is local evidence, never a committed file.

`capabilities` applies the same stack, isolation, command and path validation without running
children or creating result artifacts. It reports invalid entries alongside valid peers and returns
2 for malformed configuration; a valid manifest returns 0 even where capabilities are declared
unavailable. Inspection needs no mutation comparison base.

---

## Declared rows

`kind: current-python` prefixes the argv with the interpreter that is running the facade, so the
caller's identity propagates into every adapter.

| Stack | Capability | Command | Required artifact |
|---|---|---|---|
| `dotnet` | test | `<python> -u DynaDocs.Tests/coverage/run_tests.py --` | none |
| `dotnet` | static | `<python> DynaDocs.Tests/coverage/gate_adapter.py --stack dotnet --gate static` | `results/adapters/dotnet-static.json` |
| `dotnet` | coverage | `<python> DynaDocs.Tests/coverage/gate_adapter.py --stack dotnet --gate coverage` | `results/adapters/dotnet-coverage.json` |
| `python` | test | `<python> -m unittest discover -s DynaDocs.Tests/coverage/tests -p test_*.py` | none |
| `python` | static | `<python> DynaDocs.Tests/coverage/gate_adapter.py --stack python --gate static` | `results/adapters/python-static.json` |
| `python` | coverage | `<python> DynaDocs.Tests/coverage/gate_adapter.py --stack python --gate coverage` | `results/adapters/python-coverage.json` |
| `node` | test | `node DynaDocs.Tests/coverage/node_tests.cjs` | none |
| `node` | static | `<python> DynaDocs.Tests/coverage/gate_adapter.py --stack node --gate static` | `results/adapters/node-static.json` |
| `node` | coverage | `<python> DynaDocs.Tests/coverage/gate_adapter.py --stack node --gate coverage` | `results/adapters/node-coverage.json` |
| every stack | mutation | unavailable, reason `Pending DYD-103` | none |

Artifact paths are shown relative to `DynaDocs.Tests/coverage/`; the manifest declares them
repository-relative, and the artifact root is `DynaDocs.Tests/coverage/results`. The `dotnet` stack declares isolation
`git-worktree-copy-working-changes`, verified by the adapter `DynaDocs.Tests/coverage/run_tests.py`;
`python` and `node` declare verified `in-place` isolation.

Every G row therefore has a real mechanism: three test adapters, three static adapters and three
coverage adapters. Mutation is the only unavailable capability, and it belongs to DYD-103. Its
policy is already settled and does not wait on the adapter: DynaDocs requires no surviving or
uncovered changed-code mutants, as the [Testing Strategy](../guides/testing-strategy.md) states it.
Nothing here measures that today — every stack's mutation row is `unavailable` with the reason
`Pending DYD-103`, so the gate cannot run, and cannot pass, until that Issue lands a reviewed
mechanism. Whether a given candidate passes is what its result artifact says; no document stands in
for a run.

---

## Row states, exits and evidence

| State | Row exit | Cause |
|---|---|---|
| `passed` | 0 | the child exited 0 and every required artifact was created or refreshed |
| `failed` | 1 | a measured failure: a nonzero test exit, or a gate child exit other than 0, 2 or 130 |
| `unavailable` | 2 | the manifest declares the capability unavailable, with its reason |
| `invalid` | 2 | malformed row, unusable destination, an unrefreshed required artifact, or a gate child exit of 2 (evidence-incomplete) |
| `interrupted` | 130 | deadline or interrupt, after the adapter's own cleanup window |

Each row keeps the raw child exit in `childExit` beside its `resultExit`; the operation's aggregate
is the maximum of the row exits. A configured gate row must create or observably refresh every
required artifact during its child invocation: the facade compares metadata and content,
recursively for directory artifacts, and never deletes old evidence to manufacture freshness. That
comparison proves an observable change inside the child-operation interval, while the isolation
adapter owns protection from concurrent writers at the same evidence path.

An interrupt goes to the active adapter's process group. The facade grants up to 30 seconds for
cleanup before escalation, preserves the raw child exit when observed, stops the remaining rows and
records 130.

---

## The adapter summary

`gate_adapter.py` publishes one stable summary per row at
`DynaDocs.Tests/coverage/results/adapters/<stack>-<gate>.json`, written through a temporary file
under an exclusive `<summary>.lock` sibling; a surviving lock is a publication error worth exit 2,
never a silent overwrite. The identical bytes remain at
`results/assurance/run-<32 hex>/report.json`, whose directory also holds that run's
`inventory.json`, its command streams and the raw collector evidence.

| Field | Meaning |
|---|---|
| `schema` | `1` |
| `candidate` | `{commit, dirty, sourceFingerprint}`: Git HEAD, porcelain dirtiness, and the hash of the maintained-file inventory |
| `stack`, `gate` | the row this summary belongs to |
| `inventory` | `{path, sha256}` of the run's `inventory.json`, path relative to the artifact root |
| `tools` | both interpreter identities and every resolved tool pin |
| `commands` | ordered native command evidence |
| `collectors` | one uniform row per collector |
| `findings` | measured policy violations, each tagged with its collector |
| `gaps` | measurement errors: something the gate could not measure |
| `measurementComplete` | `false` exactly when `gaps` is nonempty |
| `exitCode` | 0 for a clean pass, 1 for findings, 2 for gaps |

### tools

```json
"tools": {
  "interpreters": {
    "caller": {"path": "<absolute>/python.exe", "sha256": "<hex>", "version": "3.12.14"},
    "dependencyBearing": {"path": "dydo/_system/.local/static-gates/python/Scripts/python.exe",
                          "sha256": "<hex>"}
  },
  "pins": {
    "python": {"path": "DynaDocs.Tests/coverage/requirements.lock", "sha256": "<hex>",
               "resolved": {"coverage": "7.16.0", "ruff": "0.16.6", "vulture": "2.16"}},
    "javascript": {"path": "DynaDocs.Tests/coverage/package.json", "sha256": "<hex>",
                   "resolved": {"c8": "12.0.0", "jscpd": "5.1.2"}},
    "dotnet": {"path": "DynaDocs.Tests/coverage/metrics/packages.lock.json", "sha256": "<hex>",
               "resolved": {"Microsoft.CodeAnalysis.CSharp": "<version>"}},
    "altcover": {"path": ".config/dotnet-tools.json", "sha256": "<hex>",
                 "resolved": {"altcover.global": "9.0.102"}}
  }
}
```

`caller` is the interpreter that ran the adapter; `dependencyBearing` is the pinned local
interpreter the dependency-carrying collectors launch. Each pin names its lock file, that file's
SHA-256, and the resolved name-to-version map it contains. An unreadable or malformed lock keeps
its `path` and records `"resolved": null` with a `reason` instead of dropping provenance silently.
The `resolved` maps above are abbreviated; the artifact carries every entry of each lock.

### commands

Ordered rows of
`{name, argv, cwd, environment, exit, elapsedSeconds, stdout, stdoutSha256, stderr, stderrSha256}`.
Stream paths are relative to the run directory. `environment` is only what this gate supplied to
that command — `APPDATA`, `NUGET_PACKAGES` and `DYDO_ROW_DEADLINE` for the adapter's own children,
and `DOTNET_CLI_USE_MSBUILD_SERVER`, `MSBUILDDISABLENODEREUSE` and
`DYNADOCS_GATE_METRICS_PREBUILT_DLL` inside the C# campaign — never the inherited process
environment, so ambient secrets stay out of the artifact.

The three coverage wrapper rows `python-coverage`, `javascript-coverage` and `csharp-campaign`
record `"stdout": null`, `"stderr": null` and `"streams": "inherited"`: those campaigns deliberately
inherit the child's streams so an operator can watch a long run. Every native command inside the C#
campaign is republished as its own row, with hashed streams, from the campaign's `commands.json`.

### collectors

`{name: {status, facts, findings, errors, artifacts}}` for every collector the row ran. `status` is
`pass`, `fail` or `error`: a `pass` may not hide findings or errors, a `fail` requires findings and
no errors, and an `error` requires at least one error identity. `artifacts` lists every raw file the
collector wrote as run-relative `{path, sha256}`. The summary's `findings` and `gaps` are these
rows' findings and errors, each carrying its collector name.

Before collecting, the adapter sets `APPDATA` to `dydo/_system/.local/appdata` and defaults
`NUGET_PACKAGES` to `~/.nuget/packages`, so tool caches stay off the developer profile.

---

## Static collectors

`projects`, `source-inventory` and `associations` run in every static row. `projects` restores each
`.csproj` and evaluates `Compile`, `PackageReference`, `IsTestProject` and `TargetPath` through
`dotnet msbuild -getItem/-getProperty`; `source-inventory` classifies every maintained file;
`associations` validates `DynaDocs.Tests/coverage/test-associations.json`.

| Stack | Collector | Mechanism |
|---|---|---|
| `dotnet` | `csharp-source` | `GateMetrics.dll --project <csproj> --root <root>`: a real Roslyn compilation per project, per-callable cognitive complexity and policy CC, parameter counts, namespace edges |
| `dotnet` | `csharp-analyzers` | two `dotnet build` passes per project, the second with `-p:RunAnalyzers=true -p:ErrorLog=<sarif>`, joined to the maintained and generated inventories |
| `dotnet` | `versions` | the resolved Python, JavaScript and .NET pins, plus `node --version` equal to `v22.13.0` and `dotnet --version` equal to `10.0.300` |
| `python` | `python-source` | `python_metrics.py` under the dependency-bearing interpreter: complexipy cognitive, radon cyclomatic, nested ternaries, module-level score |
| `python` | `python-dead-code` | `ruff check --isolated --select F` and `vulture` over every maintained Python source |
| `python` | `python-dependencies` | AST import graph and cycles; a dynamic `__import__`, `import_module` or `spec_from_file_location` call is a gap, not a finding |
| `node` | `javascript-source` | `js_metrics.cjs`: ESLint with `eslint-plugin-sonarjs`, `allowInlineConfig: false`, complexity and cognitive complexity reported at threshold 0 so every function yields its value |
| `node` | `javascript-dependencies` | `dependency-cruiser` over the maintained set: cycles and unresolved imports |
| `node` | `javascript-unused-exports` | `knip` across the `DynaDocs.Tests/coverage` and `npm` packages, its counters cross-checked against the detailed report |
| `node` | `clones` | `jscpd` over the complete maintained source set of every stack, plus a per-source eligibility proof that a skipped file really is under 15 lines or 100 tokens |

Three of those rows feed one judgment. `csharp-source` derives namespace edges with
`GateMetrics.NamespaceDependencies`, from every identifier outside a `using` directive whose symbol
is a type, method, property, field or event, linking the enclosing namespace to the symbol's
containing namespace and keeping only edges whose two namespaces both declare a type in that
project's maintained trees; `python-dependencies` derives module edges from the AST import graph;
and `javascript-dependencies` keeps dependency-cruiser's resolved edges between two maintained
files. Each stack hands its edges to the same `gate_inventory.dependency_cycles`, which returns the
strongly connected components of that graph — every component of more than one member, plus any
self-edge — rather than a bounded search, so no traversal depth can omit a cycle. The `dotnet`
edges are pooled across every project before the components are computed, so dependency cycles that
close through a second project are still found. Each one is a single finding carrying its whole
component as sorted `members`, named `namespace-cycle` on `dotnet` and `module-cycle` on `python`
and `node`.

Suppression is itself a finding: a suppressed C# analyzer diagnostic on maintained source is
reported as `maintained-diagnostic-suppression`, an `istanbul`, `c8` or `v8 ignore` comment in
JavaScript is reported as `coverage-suppression`, and inline ESLint configuration is disabled
outright.

`versions` runs in the `dotnet` static row and measures the *caller* process, so running the facade
with any interpreter other than `dydo/_system/.local/static-gates/python/Scripts/python.exe`
produces a gap such as `No package metadata was found for colorama` and fails that row closed with
exit 2.

---

## Coverage collectors

| Stack | Collector | Mechanism |
|---|---|---|
| `python` | `python-coverage` | coverage.py 7.16.0 with branch coverage, plus a flat per-process callable witness. `python_coverage.collect` writes `config.json` and a generated `startup/sitecustomize.py` onto `PYTHONPATH`, runs the ordinary unittest suite, then combines the `.coverage` data with every `counter-*.json` receipt |
| `node` | `javascript-coverage` | `node javascript_coverage.cjs` driving c8 12.0.0 with `--all --exclude-after-remap=false` over `node_tests.cjs`; Istanbul function and branch counters are joined to the ESLint callable inventory |
| `dotnet` | `csharp-coverage` | `run_tests.py --assurance-output <dir>`: one isolated Git worktree copy of the working candidate, one AltCover campaign inside a Windows Job object, evidence published back outside the snapshot |

A maintained JavaScript file with no filename extension is recorded as a gap naming DYD-105, so the
row fails closed rather than quietly measuring less than the inventory.

`gate_policy.evaluate_policy` judges the joined modules of every stack the same way: line coverage
at least 80% and branch coverage at least 60% per module, HCRAP
(`cc² × (1 − covered/total)³ + cognitive`) at most 20 and cognitive complexity at most 20 per
callable, and at most seven parameters outside constructors. Rates are exact fractions, never
rounded percentages, and a module that claims to be non-executable may carry no methods, lines or
branches.

---

## The C# campaign

Restore the pinned tool once per checkout:

```powershell
dotnet tool restore --tool-manifest .config/dotnet-tools.json
dotnet tool run altcover -- version    # AltCover version 9.0.102
```

`.config/dotnet-tools.json` pins `altcover.global` 9.0.102 with the command `altcover`. The campaign
builds `DynaDocs.sln` and `GateMetrics.csproj` in Debug and the identity producer in Release, then
runs exactly these two commands
(`csharp_coverage.altcover_commands`, pinned by
`DynaDocs.Tests/coverage/tests/test_csharp_coverage.py`):

```text
dotnet tool run altcover --
  --inputDirectory=<root>/bin/Debug/net10.0
  --inputDirectory=<root>/DynaDocs.Tests/bin/Debug/net10.0
  --inputDirectory=<root>/DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0
  --inplace
  --report=<results>/template.opencover.xml
  --reportFormat=OpenCover
  --eager --localSource --visibleBranches
  --assemblyFilter=^(?!(dydo|DynaDocs.Tests|GateMetrics)$).*

dotnet tool run altcover -- runner
  --recorderDirectory=<root>/DynaDocs.Tests/bin/Debug/net10.0
  --workingDirectory=<root>
  --executable=<python>
  --outputFile=<results>/coverage.opencover.xml
  --summary=N
  -- <root>/DynaDocs.Tests/coverage/csharp_coverage.py --_subject --root <root>
```

- `--inplace` instruments the three built output directories where they are. The originals are
  staged before the run and restored after it, and the report may name either the original path or
  AltCover's `__Saved` sibling.
- `--eager` writes runner-mode visits immediately instead of buffering an end-of-process table.
  AltCover 9.0.102's default aggregation keeps only the first spool per module, which silently
  loses every hit from a child process; the retained diagnosis is
  `dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence/child-diagnosis-research.md`.
- `--localSource` is assembly-level here: it drops nothing merely because a document is absent from
  disk.
- The assembly filter admits only `dydo`, `DynaDocs.Tests` and `GateMetrics`.
- The prepare command deliberately carries no `--showGenerated`: that flag marks unvisited
  generated code with a visit count of `-2` for the Visualizer, and `-2` is not a count the join may
  read.
- The subject the recorder runs is the ordinary unfiltered suite,
  `dotnet test DynaDocs.sln -c Debug --no-build ... RunConfiguration.TreatNoTestsAsError=true`
  followed by `tests/test_csharp_metrics.py`. Supplying selected-test arguments to a coverage row is
  exit 2.

Around those two commands the campaign proves identity: assembly and PDB SHA-256, MVID, the document
table and every method's token, signature and points are captured before instrumentation, compared
after instrumentation, and compared again after restoration. The template report is mapped to
original MethodDef tokens before execution, and the collected report must produce the identical map,
so instrumented renumbering can never rebind a method.

---

## OpenCover and MethodDef token schema

`csharp_join.coverage_methods` reads the collected report strictly. Each of these is a measurement
error rather than a coverage number:

| Element | Required | Rejected |
|---|---|---|
| `Modules/Module` | exactly one whose `ModuleName` is the assembly name | an unknown `ModulePath` alias, or a `hash` attribute that is not the original assembly's SHA-1 |
| `Files/File` | a unique `uid`, and a `fullPath` present in the assembly's PDB document table | a report source outside the PDB document inventory |
| `Classes/Class/Methods/Method` | a decimal `MetadataToken` and a `Name` equal to the original emitted signature | a duplicate token, an unknown token, or a signature mismatch |
| `SequencePoints/SequencePoint` | `vc`, `uspid`, `ordinal`, `offset`, `sl`, `sc`, `el`, `ec`, `fileid`, all nonnegative with `sl` at least 1 | a negative visit count, a `(file, line)` pair the PDB does not own, or a duplicate `(path, uspid, ordinal, offset, sl)` |
| `BranchPoints/BranchPoint` | `vc`, `uspid`, `fileid`, `sl`, `offset`, `offsetend`, `path`, `ordinal` | a `(file, line)` pair the PDB does not own, or a `uspid` already seen in the module |

Every eligible original token must appear in the report; a missing one fails with
`Missing physical token coverage`. Eligible means the method has at least one `maintained` point and
is not a `SourceBehavior` structural member — a semantic synthesized member with no authored
executable behaviour, which is accounted with its evidence instead of being counted.

---

## Roslyn policy CC

`GateMetrics.SourceMetrics.PolicyCc` is `1 +` the number of descendant syntax nodes, not entering a
nested callable, in this set: `if`, conditional expression, `for`, `foreach`, foreach-variable,
`while`, `do`, `case` label, `default` label, switch-expression arm, `&&`, `||`, `??`, `??=`,
conditional access, and `and`/`or` patterns. It is the `cc` term of HCRAP; the cognitive term comes
from SonarAnalyzer's own `CSharpCognitiveComplexityMetric`, so the build-time rule and the gate
agree on one number. Methods, accessors, expression-bodied members, local functions and lambdas are
each measured on their own control flow, and a top-level-statements file is measured as one
synthetic `Program::<Main>$` member.

The other stacks use their own producers for the same two numbers: radon plus complexipy for
Python, ESLint's `complexity` and `sonar/cognitive-complexity` rules for JavaScript.

---

## Portable-PDB document origins

Every document reached by a non-hidden sequence point is classified by reproducible origin, never by
name, suffix or on-disk existence:

| Origin | Rule | Identity |
|---|---|---|
| `maintained` | an absolute URL under the inventory root with no `obj` path segment | its root-relative path |
| `generated` | under the inventory root with an `obj` path segment | its root-relative path |
| `package` | outside the root, under a `packageFolders` entry of the owning project's locked `obj/project.assets.json`, its first two segments naming a `libraries` entry | `nuget:<package>/<version>/<path>` |

Anything else exits 2 with a message ending `outside inventory root: <exact URL>`. Only
`maintained` points create a denominator. A method whose points are all generated or package is
accounted as `excluded by origin`; a method mixing `maintained` with another origin is a
measurement error; and a maintained document whose bytes no longer match the PDB's SHA-256 or SHA-1
checksum fails the join. The measured per-assembly document facts are retained in
`dydo/agents/workspace/dyd96-portable-wip/pdb-document-classification-evidence.md`.

---

## Toolchain provenance

`dydo/_system/.local/` is git-ignored, so each machine provisions its own measurement environment
and no gate ever installs anything. Four declarations define what that environment must contain,
and every summary republishes each with its hash in `tools.pins`:

| Declaration | Supplies | Restored into |
|---|---|---|
| `.config/dotnet-tools.json` | AltCover 9.0.102 | the tool manifest, by `dotnet tool restore --tool-manifest .config/dotnet-tools.json` |
| `DynaDocs.Tests/coverage/requirements.lock` | coverage.py 7.16.0, ruff 0.16.6, vulture 2.16, complexipy 8.0.0, radon 6.0.1 | the local CPython 3.12.14 at `dydo/_system/.local/static-gates/python/Scripts/python.exe` |
| `DynaDocs.Tests/coverage/package.json`, resolved by `package-lock.json` | c8, dependency-cruiser, eslint, eslint-plugin-sonarjs, istanbul-lib-instrument, jscpd, knip | `node_modules` under `DynaDocs.Tests/coverage` |
| `DynaDocs.Tests/coverage/metrics/packages.lock.json` | the Roslyn, SonarAnalyzer and Cecil closure of `GateMetrics.csproj` | that project's `obj/project.assets.json`, by `dotnet restore` |

The `versions` collector fails the `dotnet` static row closed if an installed version differs from
its lock, if a declared JavaScript dependency and the lock disagree, if a locked package is missing
without being optional, if the resolved NuGet closure differs from `obj/project.assets.json`, or if
the runtime pins CPython 3.12.14, Node v22.13.0 and .NET SDK 10.0.300 are not the ones in use.

---

## Related

- [Testing Strategy](../guides/testing-strategy.md) — the adopted policy, the gate route and the
  recorded triage
- [DR 048](../project/decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md) —
  one-level static gates, no escape hatch
