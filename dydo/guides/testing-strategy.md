---
area: guides
type: guide
---

# Testing Strategy

Every project exposes one project-local testing facade. It is a small Python runner beside a schema
1 JSON manifest. The facade selects declared adapters and executes their argv arrays directly: it
does not construct a shell command, infer an omitted gate, or turn missing assurance into success.

Run this repository's facade with the pinned local interpreter, because the `dotnet` static row
measures the caller's own package identities:

```powershell
$py = "dydo/_system/.local/static-gates/python/Scripts/python.exe"
& $py DynaDocs.Tests/coverage/gap_check.py all
& $py DynaDocs.Tests/coverage/gap_check.py gate static
& $py DynaDocs.Tests/coverage/gap_check.py gate coverage
& $py DynaDocs.Tests/coverage/gap_check.py --force-run
& $py DynaDocs.Tests/coverage/gap_check.py gate mutation --since BASE
```

## Stable grammar

`test --stack NAME -- ARGS` runs only one selected test adapter and forwards the arguments
after `--` as literal argv items. `all` runs every declared test adapter by default, or the
selected stacks in manifest order. `gate static`, `gate coverage`, and `gate mutation --since
BASE` run only that capability. `capabilities` checks the same stack, isolation, command and path
contracts without running children or creating result artifacts. Valid configuration, including
declared unavailable capabilities, returns 0; malformed entries are reported as invalid alongside
valid peers and return 2. Mutation's argv structure is checked without requiring `--since` for inspection.
`--force-run` selects every test, static, and coverage row; it never runs mutation.

Bare invocation prints help, creates no result, and exits 2. A recognized operation writes one
`result.json` under the manifest's repository-contained `artifactRoot`. It records schema,
candidate commit and dirty state, operation, selected stacks, ordered rows, and aggregate exit.
Each row records its stack, capability, state, argv, working directory, isolation requirement and
evidence, raw child exit, result exit, artifacts, and any reason.
The result destination is prepared before dispatch. An unusable destination starts no adapter;
filesystem failures are reported as exit 2, while an already interrupted operation retains exit 130.

Exit 0 means every selected configured row passed. Exit 1 means a measurement failed. Exit 2 means
invalid, missing, malformed, unsupported, or unavailable work. Exit 130 means an interrupted
adapter completed cleanup. The aggregate preserves that priority: interruption, then unavailable or
invalid work, then measured failure, then pass.

## Manifest and adaptation

The manifest has schema `1`, a repository-relative artifact root, and an ordered array of uniquely
named stacks. A stack declares `name`, `kind`, `cwd`, `isolation`, and all four capabilities:
`test`, `static`, `coverage`, and `mutation`. A configured capability owns an `argv` or
`current-python` command and artifact declarations; `current-python` prefixes argv with the
running interpreter. An unavailable capability has a reason and is a failed-closed result, not a
passing gate.

An `argv` executable path is resolved from the stack's declared working directory while the argv
record stays unchanged. Bare executable names use the platform search rules.

Manifest cwd, adapter and artifact paths are repository-relative and contained. A configured non-test
gate must declare required artifacts and create or observably refresh each one during its successful
child invocation. The facade compares file metadata and content, recursively for directory artifacts;
an unchanged old report cannot pass. It never deletes or modifies old evidence to manufacture freshness.
This comparison establishes an observable change inside the child-operation interval; the isolation
adapter remains responsible for preventing another process from writing the same evidence path.
Mutation has exactly one argv item equal to `{base}`; the
facade replaces that one item with `--since`'s value. Isolation is a project adapter claim:
in-place work has direct evidence, while worktree and per-run requirements name a verified adapter.
The facade does not invent isolation.

DR 048 has one policy for every maintained module: warnings as errors and strict types, no dead code,
all tests passing and a test file for every non-trivial module,
line coverage of at least 80%, branch coverage of at least 60%, HCRAP at most 20 per method,
cognitive complexity at most 20, at most seven parameters outside constructors, no supported nested
ternary, no clone meeting both 15 lines and 100 tokens, and no namespace or module dependency cycles.
Only code not maintained here (generated, vendored, or minified) is excluded. There are no tiers,
classic CRAP thresholds, registry, annotations, or nesting-depth gate, and there are no
suppressions: a suppressed C# analyzer diagnostic on maintained source, an `istanbul`, `c8` or
`v8 ignore` comment, and an inline ESLint disable are a finding or ignored input, never an escape.
What DR 048 permits instead is correcting a gate that is wrong, with the triage recorded; this
repository's one recorded correction is below. Mutation is separate: DynaDocs requires
no surviving or uncovered changed-code mutants. A stack
without a reviewed mechanism reports that gate as unavailable until adoption.

`DynaDocs.Tests/coverage/gap_check.py` is the canonical runner and
`dydo/reference/gap-check.example.py` is its derived copy, not a second maintained source:
`DynaDocs.Tests/coverage/sync_testing_example.py` writes the example from the canonical bytes, and
`sync_testing_example.py --check` exits 2 when the two differ. Divergence is a defect to re-sync,
never a waiver, and the static gate refuses to exclude the derived copy from its source inventory
unless the canonical runner, the producer and the producer's test are all present and the bytes
match. Adopt it with its adjacent `dydo/reference/gap-check.example.json`: rename both together to
`gap_check.py` and `gap_check.json` at the chosen project location. The runner discovers the
enclosing Git root; paths in the manifest resolve from that root. Outside Git, they resolve from
the runner's folder.

Replace each project's cwd and artifact placeholders, supply the real isolation adapter, then enable
its capability with faithful argv. The ASP.NET example shows `dotnet test` but leaves execution
unavailable until a worktree adapter copies working changes. The React/Vite example shows Node
running `node_modules/vitest/vitest.mjs run`; its adapter must arrange per-run artifacts. The Python
example uses `uv run --locked --extra dev -m pytest` from the adapted Python project directory.
A fully adapted targeted test can run while other stacks remain unfinished. Default `all` still
reports every declared test row and returns 2 until all selected tests are available and valid.

Global JSON/schema/request errors start nothing. Row-local defects skip only that row; valid peers
run before aggregate failure is reported. Configured rows require command and artifacts and forbid a
reason. Unavailable rows require a reason, forbid executable commands/artifacts, and may carry
non-executable `exampleArgv` for adoption. Do not relabel an unwired available mechanism as a pass.
The example's own static and coverage rows stay unavailable until DYD-91's adoption pass gives each
applicable row a faithful command and evidence contract; mutation adoption is DYD-103.

An interrupt goes to the active adapter process group. The facade grants up to 30 seconds for adapter
cleanup before escalation, preserves raw child exit when observed, stops all remaining rows, and
records exit 130. Adapters
own cleanup; the router does not invent worktree or artifact isolation. DynaDocs' real cancellation
probe uses a safe filtered test and verifies its newly observed worktree directory and Git
registration have disappeared before the result is reported.

## What this repository measures

Nine rows are configured: a test, a static and a coverage adapter for each of `dotnet`, `python`
and `node`. Mutation is unavailable on all three with the reason `Pending DYD-103`, and an
unavailable capability is a failed-closed 2, never a passing gate. The `dotnet` stack runs inside
an isolated Git worktree copy of the working candidate; `python` and `node` run in place.
[Coverage Tools](../reference/coverage-tools.md) holds the exact commands, artifacts, exit meanings
and summary schema.

Static measurement covers all maintained source of a stack, test files included: complexity,
parameters, dead code, nested ternaries, dependency cycles, unused exports, clones and the native
analyzers all read test code as well as product code.

Coverage measures only target modules. The role comes from the build, not from a naming
convention: for C# from each project's evaluated `IsTestProject`, for Python and JavaScript from
native test discovery. Test bodies, fixtures and assertion helpers supply the evidence; they are
not coverage targets and are never required to cover themselves. A maintained gate producer or
runner is a target even under a test directory — `GateMetrics` and the `DynaDocs.Tests/coverage`
runners are measured, while the `DynaDocs.Tests` assembly is instrumented for identity only.
`DynaDocs.Tests/coverage/test-associations.json` carries the file-level intent DR 048's test rule
needs: every executable target module must name at least one associated test file, and one that
names none is the finding `test-association`.

Two gaps are recorded rather than dropped or weakened: mutation on every stack, which is DYD-103,
and a maintained JavaScript file with no filename extension, which the JavaScript coverage row
reports as a gap naming DYD-105 instead of measuring less than the inventory.

## Recorded gate correction: three dynamic Vulture uses

DR 048 admits a gate only where a violation is certainly wrong at a threshold where no exception
would be accepted, with no per-file suppression, and it makes the transition the validation: when
the gates first run on existing code, a failure over code that is right as it stands means the gate
is wrong and is corrected, and the triage is recorded. Dead code is such a gate, run for Python as
`ruff check --isolated --select F` plus Vulture. Vulture is a heuristic — it reports a name with no
static reader — and three of its reports here name symbols that a real caller reads at run time
through a mechanism no static analysis can see.

`DynaDocs.Tests/coverage/gate_collect.py` holds those three as exact `(path, message)` pairs in
`_DYNAMIC_VULTURE_USES`. `classify_vulture` moves a matching row out of the findings and into the
collector's `semantic_uses`, tagged with its witness.

| Path | Vulture message | Witness | Why the finding is wrong |
|---|---|---|---|
| `DynaDocs.Tests/coverage/python_coverage.py` | `unused function 'startup_from_environment'` | `python-coverage-startup` | `python_coverage.collect` generates `startup/sitecustomize.py`, whose two lines import and call the function, and puts that directory on the child's `PYTHONPATH`. CPython runs it at interpreter startup in every process of the campaign, so the only caller is a file written at run time and no static caller can exist. |
| `DynaDocs.Tests/coverage/windows_job.py` | `unused attribute 'cb'` | `windows-native-abi` | `Startup.cb` is a `_fields_` member of the ctypes mirror of Win32 `STARTUPINFOW`. `native_run` sets it to `sizeof(StartupEx)`, and the kernel reads it when `CreateProcessW` receives `byref(startup)`. |
| `DynaDocs.Tests/coverage/windows_job.py` | `unused attribute 'flags'` | `windows-native-abi` | The same ABI: `Startup.flags` carries `STARTF_USESTDHANDLES` for `CreateProcessW`, and `BasicLimits.flags` carries `KILL_ON_JOB_CLOSE` for `SetInformationJobObject`. Python writes them; only the kernel reads them. |

This is a correction, not a waiver:

- Nothing in the measured source changes. There is no pragma, ignore file, allowlist or per-file
  exemption, and no other module can inherit the three pairs.
- The pairs are exact, so drift re-raises the finding. Rename the function, rename an attribute, or
  let a Vulture upgrade reword its message, and the row no longer matches and becomes an ordinary
  dead-code finding again. Because Vulture's message does not name the owning structure, the
  `flags` pair covers every `unused attribute 'flags'` row in `windows_job.py` — the
  `STARTUPINFOEX` one and the two job-limit ones — and would cover a new one added to that file;
  any other name or file is outside it.
- The measurement keeps emitting the evidence beside the record. Every reclassified row is
  published in the `python-dead-code` collector's `facts.semantic_uses` in
  `DynaDocs.Tests/coverage/results/adapters/python-static.json` with its path, line, message,
  confidence and witness, so a reader of the artifact sees the raw Vulture output and the triage
  together.

## Related

- [Coverage Tools](../reference/coverage-tools.md) — adapter commands, schemas and provenance
- [DR 048](../project/decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md) —
  one-level static gates, no escape hatch
