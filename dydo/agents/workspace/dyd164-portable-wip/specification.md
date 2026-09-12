# DYD-164 specification — one instrumented execution per stack carries the test verdict

Authored on `DYD-164-single-execution` in
`C:/Users/User/Desktop/Projects/DynaDocs/.worktrees/dyd164`, base
`c4b2f1d16f82bc28a7c342f7cfef6f8093c5bb4d` (= `origin/feature/dydo-3-consolidation`), 2026-09-11.
Production base is the feature head **after DYD-130** (DYD-96's merge). Every file:line below was
read at `origin/codex/DYD-96-assurance-adoption` = `112ec76c067d226793b3838b9b8069ca5321d054`, the
post-DYD-130 shape; re-pin, do not re-specify, unless a cited line moved.

Authority in order: Issue DYD-164 (Outcome, Owned paths, Exact gates, Base branch); the human's
ruling of 2026-09-10 ("Do not run the same tests twice … run the relevant tests only during
iteration and the full suite at the gates"); the admiral's DYD-170 ruling of 2026-09-11 (every Merge
owes the merge-scale gate once on the exact merge commit; `gate static` plus `gate coverage` is the
interim route until this Issue lands); `dydo/guides/working-tree-contract.md:78` ("Within one such
gate run, each suite executes once"); DR 048 (`dydo/project/decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md`)
§1–§3 and its no-suppression stance; `dydo/reference/linear-workspace-standard.md`
"Communication and evidence"; the coding standards.

Ownership table: `owned-paths.json` beside this file.

---

## Spec

### Outcome

Under an invocation that selects both the `test` and the `coverage` row of the same stack — today
only `--force-run` — a stack whose coverage row declares where its instrumented run records the
suite's own exit launches **no** test process. Its coverage adapter's single instrumented run is the
one execution, and the facade derives that stack's test row from what the adapter already published.
Every other invocation, every other stack and the whole result schema are unchanged.

Measured cost today (session `g-final-e3fb4e72-20260910T163527`): .NET 8 + 13 min, Python 4.5 + 16
min of a 46-minute G; about 12 minutes of every G is the second execution.

### Seam

```text
gap_check.py --force-run
  execute_rows: for each stack, for capability in ("test", "static", "coverage")
    test      -> DEFER when the stack's coverage row is configured and declares suiteVerdict
                 and the test row itself is configured; append a placeholder, launch nothing
    static    -> unchanged
    coverage  -> unchanged: gate_adapter.py --stack S --gate coverage runs the suite under
                 instrumentation and publishes results/adapters/S-coverage.json
              -> then RESOLVE the placeholder from that published report and print it
```

Nothing else moves. `request()` (`gap_check.py:346-364`), `CAPS`/`EXITS`
(`gap_check.py:26-27`), `completed_row()` (`:283-289`), `artifact_error()` (`:273-280`),
`run_row()` (`:292-321`), `write_result()` (`:431-442`) and `HELP` (`:31-49`) are untouched.

### Why derivation is sound — the instrumented run is the same suite

| Stack | Plain test row (`gap_check.json`) | Instrumented run | Verdict |
|---|---|---|---|
| `python` | `<py> -m unittest discover -s DynaDocs.Tests/coverage/tests -p test_*.py` (`gap_check.json:23`) | byte-identical argv built at `gate_adapter.py:361-362`, run by `python_coverage.collect` (`python_coverage.py:155-156`), whose return value **is** `subprocess.run(argv).returncode` (`:166`) | identical selection, identical exit |
| `node` | `node DynaDocs.Tests/coverage/node_tests.cjs` (`gap_check.json:35`) | same script passed as c8's command (`gate_adapter.py:390`), c8's child status returned unchanged (`javascript_coverage.cjs:162-164`) | identical selection, identical exit |
| `dotnet` | `run_tests.py --` → `test_command(None)` = `dotnet test DynaDocs.sln -- RunConfiguration.TreatNoTestsAsError=true` (`run_tests.py:35-44, 239-246`) | `run_tests.py --assurance-output` (`gate_adapter.py:423`) bypasses `test_command` and reaches `csharp_coverage._subject_commands` (`csharp_coverage.py:59-66`): `dotnet test DynaDocs.sln -c Debug --no-build -p:RunAnalyzers=false -p:UseSharedCompilation=false -- RunConfiguration.TreatNoTestsAsError=true`, **plus** `<py> DynaDocs.Tests/coverage/tests/test_csharp_metrics.py` | **superset**, same solution, same `TreatNoTestsAsError=true` guard |

Both .NET legs run inside the same isolated worktree copy of the working candidate
(`run_tests.py:214-250`), and the campaign builds that copy itself before instrumenting
(`csharp_coverage.py:300-311`), so `--no-build` never runs stale binaries. The .NET derivation is
therefore **strictly at least as strict** as the plain row: it can never report green where the
plain row would report red. Two recorded departures, neither a gap in this Issue:

- The campaign builds with analyzers off. Analyzer enforcement belongs to the `static` row and to
  `dotnet build -warnaserror`, never to the test row.
- `test_csharp_metrics.py` executes inside the .NET campaign **and** inside the `python` test row's
  discovery (`DynaDocs.Tests/coverage/tests/test_*.py`). That module therefore still runs twice per
  G, once per stack. It is seconds of work, it is not "a stack's full test suite run twice", and
  this Issue does not fix it. Recorded, not owned.

The instrumented run's environment differs from the plain row's: `gate_adapter.main` redirects
`APPDATA` to `dydo/_system/.local/appdata` and defaults `NUGET_PACKAGES` (`gate_adapter.py:468-470`),
and the Python leg prepends a generated `sitecustomize.py` to `PYTHONPATH`
(`python_coverage.py:146-154`). The derived verdict is, by design, the verdict of the instrumented
run.

### Where the verdict is read from — no adapter change

The adapters already record everything needed. Per stack, `gate_adapter` writes
`collectors.<collector>.facts.child_exit` — the suite's own exit — and, when it judges that exit to
be the suite's verdict rather than a broken measurement, one finding, verbatim
`{"gate": "functional", "child_exit": N}` (`gate_adapter.py:368-369` python, `:412` node,
`:437-438` dotnet). Collector names are `python-coverage`, `javascript-coverage`,
`csharp-coverage`.

**The finding is untagged, and it is published at two coordinates.** `_aggregate` (`:277-288`) —
which would prefix every finding with its `{"collector": name}` — is reached from `collect_static`
alone (`:337`); no coverage path calls it. The coverage path goes through `_coverage_report`
(`:165-168`), which hands the *same* finding list both to the collector row and to the outer
`result()` (`gate_run.py:64-72`), and `_publication_payload` publishes that one list unchanged at
the report's top-level `findings` (`:175, :183`) **and** at `collectors.<collector>.findings`
(`:182`). Neither copy carries a `collector` key. A declaration that expects one can never match a
real report; the spec's coordinates below are the ones that do.

**Verified against the real published reports**, not only against the code:
`.worktrees/dyd96-adoption/DynaDocs.Tests/coverage/results/adapters/{python,node,dotnet}-coverage.json`
(green campaigns, 2026-09-11). Each has top-level keys `candidate, collectors, commands, exitCode,
findings, gaps, inventory, measurementComplete, schema, stack, tools`. Each `collectors` object
holds **exactly one** key — `python-coverage`, `javascript-coverage`, `csharp-coverage` — whose
value has `artifacts, errors, facts, findings, status`. In all three, `facts.child_exit` is `0`,
`status` is `"pass"`, and both `findings` arrays are `[]`. So
`["collectors", "<collector>", "facts", "child_exit"]` and
`["collectors", "<collector>", "findings", …]` are both live coordinates in the shipped shape.

**One collector per coverage report.** `_coverage_report:168` constructs `{"collectors": {name:
row}}` from a single name, so a coverage report structurally cannot publish two collectors; all
three real reports confirm it (`len(collectors) == 1`). The top-level `findings` array is therefore
unambiguous *today* — but the declaration below scopes `failure` under the collector anyway, so a
future report that did grow a second collector could not feed another collector's `functional`
finding into this stack's verdict. The scoping is in the declared path, not in a rule about adapter
names, which is what keeps `gap_check.py` free of adapter knowledge.

**The coverage row's process exit cannot carry the verdict.** `_publication_payload` maps
`{"pass":0,"fail":1,"error":2}[status]` (`gate_adapter.py:185`), so a coverage **policy** failure and
a **functional** suite failure both leave the row at exit 1, and a nonzero `child_exit` that the
adapter classified as a broken campaign leaves it at 2. Reading the exit alone would hide a test
failure inside a coverage exit, which the Outcome forbids.

**Decision.** The facade reads the published report at declared coordinates. No coverage adapter,
`python_coverage.py`, `javascript_coverage.cjs`, `csharp_coverage.py` or `run_tests.py` changes; the
Owned paths' adapter clause is not exercised.

### The manifest declaration

`gap_check.py` is copied byte-for-byte to `dydo/reference/gap-check.example.py`
(`sync_testing_example.py:24-29`) and is meant to be adopted by other projects. It therefore learns
no adapter shape. The coordinates live in the project's manifest, on the **coverage** capability
only, as one optional key:

```json
"coverage": {
  "state": "configured",
  "command": {"kind": "current-python", "argv": ["DynaDocs.Tests/coverage/gate_adapter.py", "--stack", "dotnet", "--gate", "coverage"]},
  "artifacts": [{"path": "DynaDocs.Tests/coverage/results/adapters/dotnet-coverage.json", "required": true}],
  "suiteVerdict": {
    "exit": ["collectors", "csharp-coverage", "facts", "child_exit"],
    "failure": ["collectors", "csharp-coverage", "findings", {"gate": "functional"}]
  }
}
```

`python` uses `python-coverage`, `node` uses `javascript-coverage`, in both fields. Exactly these
three rows in `gap_check.json` gain the key.

Both coordinates were verified against real reports above. The matcher object is
`{"gate": "functional"}` and nothing more: the published finding carries no `collector` key, and
`child_exit` is deliberately *not* matched on, because its value is the very number `exit` supplies.
`failure` is scoped under the same `["collectors", "<collector>"]` prefix as `exit` so that the two
coordinates always describe the same collector; the equally valid untagged shape
`["findings", {"gate": "functional"}]` is rejected only because it reads a report-wide bag.

**Validation**, inside `configured_command` (`gap_check.py:143-164`), fails the row closed exactly as
every other manifest defect does — `invalid`, resultExit 2, valid peers still run:

1. The permitted key set is `{"state","command","artifacts"}`, plus `"suiteVerdict"` **only when
   `capability == "coverage"`**. Any other extra key, and `reason` on a configured row, keep today's
   diagnostic and today's behaviour.
2. `suiteVerdict` must be an object with exactly `{"exit","failure"}`.
3. `exit` is a nonempty array of nonempty strings.
4. `failure` is an array whose first *n*−1 items are nonempty strings and whose last item is a
   nonempty object mapping nonempty strings to `str`, `int` or `bool`.
5. The row must declare **exactly one** artifact with `"required": true`. That single artifact is the
   report; there is no second path to keep in sync.
6. `failure` must name at least one container key before its matcher object — `len(failure) >= 2`.
   A bare `[{…}]` would ask the runner to match the report root itself; the runner never *searches*
   a report, it only walks a declared path, so an unscoped matcher is a declaration defect.

**The walk is never widened.** Derivation follows `failure`'s leading string keys from the report
root through object keys only and matches inside the single array it lands on — it never scans
siblings, never recurses, and never learns a collector name. That is what makes the collector-scoped
declaration an actual guard rather than a convention: a second collector's findings would live at a
path this declaration does not name, so they cannot reach this stack's verdict. Gate 7's condition 4
pins the one-collector fact against the real reports on every acceptance run.

Diagnostic on any defect: `invalid suite verdict declaration`. `capabilities`
(`gap_check.py:374-384`) reports the same state names as today and gains only this diagnostic
string.

The key is optional, so `dydo/reference/gap-check.example.json` (whose three coverage rows are
`unavailable`), every unadopted manifest and every fixture manifest in the test suite stay valid and
byte-unchanged.

### Derivation applies to the invocation, not to the coverage row

A stack's test row is **deferred** iff all four hold:

1. this invocation's capability set contains both `test` and `coverage` — today only `--force-run`
   (`gap_check.py:347-348`); and
2. the stack is selected; and
3. `prepare_row(stack, "coverage", root)` returns state `configured` **and** that coverage
   capability carries `suiteVerdict`; and
4. `prepare_row(stack, "test", root)` returns state `configured`.

Therefore `all`, `test --stack NAME`, `gate static`, `gate coverage`, `gate mutation` and
`capabilities` are bit-for-bit unchanged: `all` and `test` never select a coverage row, and every
`gate` selects exactly one capability. `--force-run` still forwards no native arguments
(`gap_check.py:348`), so no forwarded argument can reach a derived row.

Condition 3 covers "a host without the venv" and any unadopted stack: a coverage row that is
`unavailable` or `invalid` **at prepare time** produces no deferral, so that stack's test row runs
plainly exactly as today. That is DR 048 §2's rule — a stack without a mechanism skips that gate and
keeps the rest — not an escape hatch: nothing is skipped and nothing passes.

### Execution and reporting order

For a deferred stack, `execute_rows` (`gap_check.py:414-428`) appends a placeholder at the test row's
index, launches nothing, runs `static` and `coverage` unchanged, then resolves and prints the test
row immediately after the coverage row completes.

- **Rows in `result.json` keep manifest capability order**: test, static, coverage. Unchanged.
- **Live print order changes for a deferred stack** to `static`, `coverage`, `test`, because each
  line is printed when that row's outcome becomes known. `execute_rows`' print uses the resolved
  row's own `capability`, not the loop variable. This is the only observable change outside the
  derived row itself, and it affects no existing assertion (`test_aggregation`
  `test_testing_facade.py:445-457` pins stdout order for `all`, not `--force-run`).
- **Interruption.** `execute_rows` still breaks out of the capability loop on an interrupted row and
  out of the stack loop when the last executed row is interrupted. A placeholder is resolved at
  exactly two points, which are exhaustive: (a) when its stack's coverage row completes; (b) when
  the capability loop ends with the row still deferred — reachable only through the interruption
  break, because deferral requires `coverage` to be in the capability set. `rows` never leaves
  `execute_rows` holding a placeholder.

### The derived row

`argv: []`, `artifacts: []`, `environment: {}`; `stack`, `cwd`, `isolation` from `result()`
(`gap_check.py:87-92`) as for any row of that stack. **No new result key.** Empty `argv` on a
`passed` or `failed` test row is the structural marker that no process was launched; `reason` names
the evidence. `assert_payload`'s exact key set (`test_testing_facade.py:521-523`) and the schema
scenario line `testing-facade.feature:184` are satisfied unchanged.

`reason` strings, verbatim, no implementer choice:

| Case | reason |
|---|---|
| derived pass or fail | `test verdict derived from the coverage row: suite exit {n} at {artifact}` |
| coverage row not `passed`/`failed` | `suite verdict not established: the coverage row is {state}` |
| coverage row never ran | `suite verdict not established: the run was interrupted before the coverage row` |
| report unreadable or coordinate missing | `suite verdict not established: {artifact} does not record an integer at {"/".join(exit)}` |
| nonzero exit the adapter did not attribute to the suite | `suite verdict not established: the coverage row did not attribute child exit {n} to the suite` |

### Fail-closed derivation — the exact per-case table

Let `C` be the stack's completed coverage row and `R` its single required artifact.

1. `C.state == "interrupted"` → test row `interrupted`, `childExit` null, resultExit 130.
2. `C.state == "invalid"` → test row `invalid`, `childExit` null, resultExit 2 — **always, with no
   exception, and `R` is not opened at all.** (`C.state == "unavailable"` is unreachable here:
   prepare-time unavailability means no deferral.)

   The reason is not freshness. `invalid` has three producers in `run_row` and only one of them
   says anything about `R`: the freshness failure at `gap_check.py:310-313` (`R` was not produced or
   not refreshed — absent or stale); `completed_row`'s child-exit-2 branch at `:284-285`, reached
   through the call at `:314` and therefore *after* the freshness check passed, so `R` is this
   child's own; and the `except (OSError, ContractError)` branch at `:320-321`, where the child may
   never have launched at all and `childExit` may be null. The
   row alone does not distinguish them — the only in-band discriminator is the presence and wording
   of `reason`, and a verdict must not hang on a diagnostic string. So the rule is the blunt one:
   `invalid` means the measurement is broken and the suite verdict is not established, whatever the
   report happens to contain. This is the same fail-closed stance as the crash case recorded below,
   and it costs nothing an operator needs: the aggregate is already 2, no row reports `passed`, and
   the `reason` names what was not established.
3. `C.state in ("passed","failed")` — and only then, because those two states are exactly the ones
   in which this child both refreshed its required artifact (`artifact_error` at
   `gap_check.py:310-313` returned no error) **and** exited 0 or 1 (`completed_row:284-287`), so `R`
   is provably this child's own report and never a stale one. That is the whole discriminator, and
   it is one sentence: **a coverage row is `passed` or `failed` only after this child refreshed `R`
   and exited 0 or 1.** Read `R`, walk `suiteVerdict.exit` through object keys:
   - absent / unreadable / not JSON / key missing / value not an `int` (a `bool` is not an `int`
     here) → test row `invalid`, resultExit 2;
   - value `0` → test row `passed`, `childExit` 0;
   - value `N != 0` and `suiteVerdict.failure` matches — walk its leading string keys through
     object keys to an array, then some element of *that* array is an object containing every
     declared key with the declared value → test row `failed`, `childExit` N, resultExit 1. For the
     three DynaDocs rows this is one untagged `{"gate": "functional", "child_exit": N}` inside
     `collectors.<collector>.findings`;
   - value `N != 0` and no match → test row `invalid`, resultExit 2.

Applied to the real adapters, with the coverage row's own behaviour unchanged:

| Situation | coverage row | test row | aggregate |
|---|---|---|---|
| suite passes, policy passes | `passed` 0 | `passed`, childExit 0, exit 0 | 0 |
| suite passes, policy fails | `failed` 1 (findings, `child_exit` 0) | `passed`, childExit 0, exit 0 | 1 |
| suite fails | `failed` 1 (untagged `{"gate": "functional", "child_exit": N}` at `collectors.<c>.findings`, and the identical object at the report's top-level `findings`) | `failed`, childExit N, exit 1 | 1 |
| campaign could not measure — build/prepare/instrumentation/tool/join failure, or the adapter raised early so `collectors` falls back to `report["facts"]` (`gate_adapter.py:182`) and the coordinate resolves to nothing | `invalid` 2 | `invalid`, childExit null, exit 2 | 2 |
| adapter classified a nonzero child exit as a broken campaign (`child not in (0,1)`, `gate_adapter.py:401, :431`) | `invalid` 2 | `invalid`, childExit null, exit 2 | 2 |
| inventory errors, so the campaign is `error` even though `facts` and `findings` survive and `R` still records the suite's own exit (`gate_adapter.py:477-480`) | `invalid` 2 | `invalid`, childExit null, exit 2 — **`R` is not read**, per rule 2 | 2 |
| the coverage tool itself is missing, so the collector's child fails without launching a test — Node on a runner with no `node_modules`: `javascript_coverage.cjs:157` resolves a c8 that is not there, `spawnSync` at `:162` starts node, node exits 1 on the missing module, `:164` returns 1, and `gate_adapter.py:412` records that 1 as a `functional` finding | `failed` 1 | `failed`, childExit 1, exit 1 — **a failure attributed to a suite that never ran**; see F4 | 1 |
| coverage row interrupted | `interrupted` 130 | `interrupted`, exit 130 | 130 |
| an earlier row interrupted, coverage never ran | absent | `interrupted`, exit 130 | 130 |
| coverage row unavailable or invalid **at prepare time** | 2 | runs plainly, exactly as today | per rows |

**Chosen against a fallback plain execution, for every runtime case.** A second, uninstrumented run
of the same suite inside the same gate run is exactly the alternate satisfying path DR 048 §1
forbids: it would let a test row go green by a cheaper route the G had already denied, and it would
re-import the 12 minutes this Issue exists to remove, at the worst moment — after a failed
13-minute campaign. Nothing is hidden by refusing it: the aggregate is already 2 or 130, no row
reports `passed`, and each refusal carries a `reason` naming what was not established. An operator
who wants the plain verdict runs `gap_check.py test --stack NAME`, a separate invocation, not the
same gate run. The prepare-time case is the single exception, and it is not a second execution: the
coverage row never ran the suite at all.

**One consequence to record.** A suite that dies without recording its exit (a Python suite crashing
past `atexit`, so `combine_counters` raises `Missing Python counter files`,
`python_coverage.py:78-79`) is reported as test `invalid` 2 with its reason, not `failed` 1. Nothing
passes and nothing is silent; the record says the verdict was not established rather than inventing
one.

### Contract inaccuracy to carry to the admiral

The Issue's Outcome says "a failing test still yields the test row's exit 1 with its native output,
and **the coverage row is invalid 2 as today**". Today a functional suite failure under the coverage
row yields a `functional` finding → status `fail` → `exitCode` 1 → coverage row **`failed` 1**, not
`invalid` 2 (`gate_adapter.py:368-370, :412, :437-439, :185`, and `completed_row`
`gap_check.py:283-289`). Only a campaign that could not measure yields 2. The table above is the
actual behaviour and this Issue leaves it exactly as it is; the parenthetical is a description
inaccuracy the captain carries to the admiral, not a change to make.

### Scenarios

`DynaDocs.Tests/Features/testing-facade.feature`. This specify hop is **spec-only** by the captain's
brief: production is blocked until DYD-130 is Done, so the scenario text below is authored here and
written into the feature file as the first act of the implement hop, red before green. Each scenario
binds one-to-one to a named probe in `DynaDocs.Tests/coverage/tests/test_testing_facade.py` through
`TestingFacadeSteps.ProbeName()` (`TestingFacadeSteps.cs:214`), following the existing pattern.
Fixture stacks come from `test_testing_facade.py:31-42`; the fixture coverage command writes a JSON
report at the row's declared required artifact.

**Every fixture report must mirror the real published shape.** A fixture that authors a finding as
`{"collector": "…", "gate": "functional"}` would make a wrong declaration look correct — that is the
defect this hop corrects. So each fixture report a scenario writes has, at minimum, a `collectors`
object with **exactly one** key, that collector's `facts.child_exit`, and — where a suite failure is
being exercised — that collector's `findings` holding the untagged object `{"gate": "functional",
"child_exit": N}` and nothing else in it. Confirmed against
`.worktrees/dyd96-adoption/DynaDocs.Tests/coverage/results/adapters/*-coverage.json`.

**New.**

1. *One instrumented execution carries both the coverage measurement and the test verdict* — a stack
   whose coverage row declares a suite verdict and publishes `child_exit` 0; `--force-run`; the test
   command's side-effect file is absent, the coverage command's is present, rows are test/static/
   coverage in manifest order, the test row is `passed` with `childExit` 0, empty `argv` and the
   derivation reason, aggregate 0. Probe `test_derived_test_row_passes_from_the_instrumented_run`.
2. *A derived test verdict fails closed* — Scenario Outline over the per-case table above, one
   example per distinct facade outcome in it: `suite passes and policy passes`,
   `suite passes and policy fails`,
   `suite fails`, `the campaign could not measure`,
   `the campaign is invalid but its report records the suite exit`,
   `the report records no suite exit`, and
   `the coverage row did not attribute the child exit to the suite` — each naming the test row's
   state, childExit and resultExit, the coverage row's state and resultExit, and the aggregate.
   One probe per example. Two of these examples are the discriminating ones the implementer may
   not resolve by taste:
   - `suite fails` writes the **untagged** finding shape above; a probe that passes only against a
     `collector`-tagged fixture is the defect this correction removes.
   - `the campaign is invalid but its report records the suite exit` writes a report whose
     `collectors.<c>.facts.child_exit` is a usable integer *and* whose coverage row is `invalid` 2
     (fixture coverage command exits 2 after writing the report, so the freshness check passes and
     `completed_row` maps 2 to `invalid`). The test row must be `invalid`, `childExit` null,
     resultExit 2 — pinning rule 2's "`R` is not opened" against the one case where opening it
     would have produced a different answer.

   Two table rows get no example of their own, deliberately. The "adapter classified a nonzero child
   exit as a broken campaign" row is byte-identical at the facade to `the campaign could not
   measure`. The missing-tool row is byte-identical at the facade to `suite fails` — the facade
   cannot tell them apart, and that is precisely why the misattribution has to be fixed in the
   adapter (F4) rather than papered over with a facade probe.
3. *An interruption before the coverage row leaves no unresolved test verdict* — the static row is
   interrupted after its cleanup; the coverage command does not run; the test row is `interrupted`
   with resultExit 130 and the deferral reason; aggregate 130. Probe
   `test_deferred_test_row_is_interrupted_when_coverage_never_runs`.
4. *Derivation belongs to the invocation, not to the coverage row* — Scenario Outline over `all`,
   `test --stack dotnet`, `gate coverage --stack dotnet` and `--force-run` against the same
   declaring manifest, naming for each whether the test command and the coverage command run. Probe
   `test_derivation_only_when_both_rows_are_selected`.
5. *A stack without a declared suite verdict keeps its own test execution* — one stack with a
   configured coverage row and no declaration, one with an unavailable coverage row; `--force-run`;
   both test commands run and both test rows record their own child exits. Probe
   `test_undeclared_or_unavailable_coverage_keeps_the_plain_test_row`.
6. *A malformed suite verdict declaration skips only that row* — one new example row
   `an invalid suite verdict declaration` on the existing outline *A row-local defect skips only
   that row* (`testing-facade.feature:187-202`); the coverage row is invalid, the test row runs
   plainly, valid peers run, exit 2. Probe `test_row_suite_verdict`.

**Amended, by name.**

7. *Full-G compatibility never degrades to tests only* (`testing-facade.feature:91-98`) — line 95's
   "every valid configured selected command runs" gains "except a test row whose stack declares a
   coverage suite verdict: it runs no command of its own and its verdict comes from that one
   instrumented execution". Its probe `test_full_g` (`test_testing_facade.py:436-443`) gains a third
   stack that declares a suite verdict, and asserts its three rows and the absence of its
   `third-test.txt`.
8. *Schema 1 has one small concrete manifest and result shape* (`testing-facade.feature:147-185`) —
   the docstring's `dotnet` stack must be updated with the new `suiteVerdict` block, because
   `AssertConcreteManifestShape` deep-equals it against `gap_check.json`'s first stack
   (`TestingFacadeSteps.cs:192-204`). Line 177 gains "a configured coverage row may additionally
   declare `suiteVerdict` with exactly `exit` and `failure` and exactly one required artifact"; a
   new line states "a derived test row records empty argv, the suite's own child exit and a reason
   naming the coverage report". Lines 182-185 (result shape, row keys, state and exit domains) stay
   verbatim. Probe `test_schema`; `schema_rows` (`test_testing_facade.py:599-631`) gains the
   `suiteVerdict` defect cases from the validation list above.
9. *DynaDocs uses its real adapters and admits missing assurance* (`testing-facade.feature:241-249`)
   — gains "And each stack's coverage row declares the suite verdict its test row is derived from".
   Probe `test_active_manifest` (`test_testing_facade.py:704-719`) asserts the three declarations.

**Not amended, verified.** `DynaDocs.Tests/Features/assurance-adoption.feature` and
`DynaDocs.Tests/Steps/AssuranceAdoptionSteps.cs` need no change: every scenario there drives
`gate <capability>` or `test --stack`, never `--force-run` (`test_assurance_adoption.py:51-52,
:122`), so no invocation there selects both rows of one stack.
`DynaDocs.Tests/coverage/tests/testing_facade.test.mjs` drives `test --stack node` only
(`testing_facade.test.mjs:38`) — unchanged.

### Gates — verbatim

`$P` = `dydo/_system/.local/static-gates/python/Scripts/python.exe` (the pinned local interpreter;
`coverage-tools.md:18-21`).

| # | Command | Pass condition | When |
|---|---|---|---|
| 1 | `$P -m unittest test_testing_facade.TestingFacadeTests.<probe> test_testing_facade.PortableTestingFacadeTests.<probe>` for each touched probe, with `PYTHONPATH=DynaDocs.Tests/coverage/tests` | exit 0; red before green on the specify-committed scenarios | iteration |
| 2 | `$P DynaDocs.Tests/coverage/sync_testing_example.py --check` | exit 0 | iteration and acceptance |
| 3 | `$P DynaDocs.Tests/coverage/gap_check.py capabilities` | exit 0; every stack reports `test: configured`, `static: configured`, `coverage: configured`, `mutation: unavailable` | iteration and acceptance |
| 4 | `$P DynaDocs.Tests/coverage/run_tests.py` with the two-name filter below | exit 0; every scenario of the feature and all three `ReleaseWorkflowTests` facts pass | iteration |
| 5 | `dotnet build DynaDocs.sln -c Release -warnaserror` | exit 0 | acceptance |
| 6 | `dotnet bin/Release/net10.0/dydo.dll check` | 0 errors, 0 warnings | acceptance |
| 7 | one full G on the exact clean candidate: `$P DynaDocs.Tests/coverage/gap_check.py --force-run` | the row evidence below | acceptance |
| 8 | the release workflow's non-publishing dry run on the candidate | the comparison below | acceptance |

**Gate 4's command**, verbatim — the `|` is a literal character of the filter, which is why the
command sits here and not in the table cell:

```text
$P DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~OneProject_LocalInterfaceRunsTestsAndAssuranceHonestlyFeature|FullyQualifiedName~ReleaseWorkflowTests"
```

**Gate 7's row evidence — the mechanical reviewer check.** From the G's `result.json`:

```text
$P -c "import json,sys;d=json.load(open(sys.argv[1]));[print(r['stack'],r['capability'],r['state'],r['resultExit'],r['childExit'],len(r['argv']),r.get('reason','')) for r in d['results']];print('aggregate',d['aggregateExit'])" <result.json>
```

Pass condition, all four:

1. Nine rows, three per stack, in manifest and capability order.
2. Every `test` row prints `0` for the argv length and a reason beginning
   `test verdict derived from the coverage row` — i.e. no test row launched a process.
3. Every `coverage` row's `argv` is that stack's `gate_adapter.py --stack <s> --gate coverage`
   vector.
4. In each `DynaDocs.Tests/coverage/results/adapters/<stack>-coverage.json`, `commands[]` holds
   exactly one row named `python-coverage`, `javascript-coverage` or `csharp-campaign` — one
   instrumented execution per stack — and its `exit` equals the derived test row's `childExit`;
   and `collectors` holds **exactly one** key, the collector the row's `suiteVerdict` names in both
   its `exit` and its `failure` path. The second half of that condition is the standing pin against
   a future two-collector report reaching a declaration written for one.

Corroboration, recorded not asserted: the G's wall clock against the 2026-09-10 baseline of 46
minutes; the expected drop is roughly the 12 minutes measured there.

**Gate 8's comparison.** `workflow_dispatch` on the branch runs `build` and `validation` only;
`release`, `nuget` and `npm` are tag-guarded (`release.yml:111-112`), so the dispatch publishes
nothing. Every `release.yml` line number in this spec is the file **before** this Issue's two added
steps; after the edit each one below `:93` shifts down by 6. The job's own exit is compared **before and after on the same candidate**, not required to
be 0 — see flag F1.

The pass condition is written from *measured* runner behaviour, not from expectation: it is the
"after" column of the release-workflow table below, and every claim in that table that could not be
traced to a line is marked there as measured-by-this-gate. Gate 8 passes when, in the
`Run coverage gate` step's `result.json` and in the step logs:

1. **Three suite executions, one per stack, across the whole job.** (i) the `dotnet test` summary
   line (`Passed!` / `Failed!  - Failed: …, Total: … - DynaDocs.Tests.dll`) appears exactly once in
   the job, in `Run isolated test adapter`; (ii) inside `Run coverage gate`, `python test:` and
   `node test:` each print exactly once, each after its stack's `coverage:` line, with a reason
   beginning `test verdict derived from the coverage row`; (iii) `python-coverage.json` and
   `node-coverage.json` `commands[]` each hold exactly one row named `python-coverage` /
   `javascript-coverage` whose `argv` is the suite argv (`gate_adapter.py:361-362` / `:390`) and
   whose `exit` equals the derived row's `childExit`.
2. **No derived row reports a verdict for a suite that did not run.** For `python` and `node`, the
   published `<stack>-coverage.json` `commands[]` row carries the collector's `exit`, and the
   derived test row's `childExit` equals it. If either derived row is `failed` while its stack's
   suite produced no test output in the log, gate 8 **fails** — that is the F4 misattribution and it
   must not be signed off as expected.
3. **`dotnet` is `invalid`, not `failed`.** The `dotnet` coverage row is `invalid` 2 (Windows-only
   campaign, `windows_job.py:88-89`) and the derived `dotnet` test row is `invalid` with the
   not-established reason. The .NET suite's real verdict in this job comes from
   `Run isolated test adapter`, which must still exit 0.
4. **No new failing step relative to the base**, and no step that failed on the base now silently
   passing for a reason other than this Issue's change.

If the two new install steps do not make the Python and Node coverage rows launch their suites, the
implement hop reports a mismatch and stops at the choice — it does not accept a green-looking gate 8
whose Python and Node suites never ran.

### Reconciliations — adoptable verbatim

**DYD-96 adoption spec line 21 — "`all` means tests; `--force-run` means test/static/coverage, never
mutation."** Unchanged and still exact. DYD-164 does not touch `request()`, `CAPS` or the capability
sets any operation selects (`gap_check.py:26, :346-364`); `--force-run` still selects the same nine
rows in the same order and still never selects mutation. What changes is only how a test row obtains
its verdict when the same invocation also selects that stack's declaring coverage row.

**DYD-96 adoption spec line 152 (G-final) — "the exact candidate's `--force-run` returns 0 with zero
valid gaps/violations."** Unchanged as the acceptance condition, and not weakened. Aggregate 0 still
requires every row `passed`; a derived test row is `passed` only when the coverage adapter published
`child_exit == 0`, which for all three stacks is reachable only after the suite's own process exited
0, and the coverage row is `passed` only with no findings and no gaps. On the pinned Windows
interpreter all three coverage rows can complete, so 0 stays reachable on the same host G-final
already targets.

**DYD-103 spec gate 9 (line 496 of
`.worktrees/dyd103-spec/dydo/agents/workspace/dyd103-portable-wip/specification.md`) —
"`$P DynaDocs.Tests/coverage/gap_check.py --force-run` | identical
selected rows and aggregate before and after DYD-103's edits on the same base; mutation never
selected."** Unchanged, and DYD-103 needs no re-pin on DYD-164's account. That gate compares two legs
on the base DYD-103 pins; DYD-164 is not one of DYD-103's edits, so if its base includes DYD-164 both
legs derive identically and if it does not, neither does. Selected rows are unchanged; mutation is
still never selected by `--force-run`. DYD-103 consumes only the facade's freshness behaviour
(`artifact_error`, `gap_check.py:273-280`) and the 0/1/2/130 mapping (`EXITS` `:27`, `completed_row`
`:283-289`); **DYD-164 changes neither**, and nothing it adds applies to a `mutation` row, to
`gate mutation`, or to any row of a stack whose coverage capability carries no `suiteVerdict`.

### The release workflow — what runs on `ubuntu-latest`, before and after

The earlier version of this section claimed "the Python and Node suites drop from two executions to
one each". That is false, and correcting it changes this Issue's route. Here is the traced record.

**The validation job installs nothing.** `.github/workflows/release.yml:69-108` read in full at
`112ec76c`: `checkout`, `setup-python` 3.13 (`:80-83`), `setup-dotnet` (`:85-88`), `setup-node` 24
(`:90-93`), then five `run:` steps. There is no `pip install`, no `npm ci`, and no vendored
`node_modules` — only `DynaDocs.Tests/coverage/requirements.lock` (8 pins, `coverage==7.16.0`) and
`DynaDocs.Tests/coverage/package.json` + `package-lock.json` (`c8@12.0.0`) are tracked. The file's
only `apt-get` (`:48-49`) is in the `build` job's Linux-ARM64 leg, a different job.

**Measured today, on the real runner.** GitHub Actions run `34557358149` (2026-09-11, push of the
merge commit `c4b2f1d1` — this branch's own base): validation steps 1-8 all succeeded, including
`Run isolated test adapter`; step 9 `Run coverage gate` failed with `Aggregate: 2`; step 10
`Run mutation gate` was skipped because step 9 failed. Its `--force-run` log shows
`dotnet test: PASSED (child exit 0)` (a 2377-test `dotnet test DynaDocs.sln`), `Ran 138 tests …
OK` then `python test: PASSED (child exit 0)`, and the `node --test` spec-reporter summary
(`ℹ tests 1`) then `node test: PASSED (child exit 0)`; all six static and coverage rows printed
`UNAVAILABLE: Pending DYD-96`. So the job does reach `--force-run` and all three suites do execute
there — this is not a hypothetical path.

**Per stack, on that runner.** "Today" = the base above. "After DYD-130" = the same job with
`gap_check.json`'s coverage rows `configured` (`gap_check.json:13, :25, :37` at `112ec76c`), which is
DYD-164's production base. "After DYD-164" = with derivation and no workflow edit.

| Stack | Today | After DYD-130, before DYD-164 | After DYD-164 with **no** workflow edit |
|---|---|---|---|
| `dotnet` | suite runs **twice**: step `:95-96` and the `--force-run` test row | still twice; coverage row `invalid` 2 — `windows_job.validate` refuses non-Windows / non-3.12.14 (`windows_job.py:88-89`) → `_run_assurance_campaign` returns 2 (`run_tests.py:209-211`) → `child not in (0,1)` → broken campaign (`gate_adapter.py:431-435`) | suite runs **once** (step `:95-96`); derived test row `invalid` 2 with its reason. Correct, and the intended saving. |
| `python` | suite runs **once**, in the `--force-run` test row (`gap_check.json:23`) — the job's only Python suite execution | still once, in the test row; the coverage row dies before it can launch anything: `python_coverage.collect` does `import coverage` at `python_coverage.py:132`, **before** the suite launch at `:156`. `coverage` is a third-party pin, so `ModuleNotFoundError` — an `ImportError`, absent from `REPORT_DEFECTS` (`gate_adapter.py:25-26`) — escapes `main`'s handler at `:481`, the adapter dies without publishing, and `gap_check`'s freshness check (`:310-314`) makes the row `invalid` 2 | **the Python suite runs zero times.** Test row deferred, coverage row `invalid` → derived test row `invalid` 2 |
| `node` | suite runs **once**, in the `--force-run` test row (`gap_check.json:35`) — the job's only Node suite execution | still once, in the test row; the coverage row resolves c8 at `DynaDocs.Tests/coverage/node_modules/c8/bin/c8.js` (`javascript_coverage.cjs:157`), which does not exist. `spawnSync` at `:162` succeeds (node exists), the child exits 1 on the missing module, `:164` returns 1, `child in (0,1)` so `gate_adapter.py:412` records `{"gate":"functional","child_exit":1}` → coverage row `failed` 1 | **the Node suite runs zero times, and the derived test row says `failed` 1** — a red test verdict for a suite that never started |

So with no workflow edit this Issue would delete the release validation's only Python and Node suite
executions (138 Python tests and the whole Node suite) and replace one of them with a false failure.
That is not the Outcome; it is the Outcome's inverse.

### Decision — the validation job installs the coverage toolchain

`.github/workflows/release.yml` is an Owned path "where they invoke the facade", and making the
invocation at `:105` measure anything on that runner is inside it. **Edit the validation job; keep
`Run isolated test adapter` at `:95-96`.**

Two steps, inserted immediately after `Setup Node.js` (`:90-93`) and therefore before every `run:`
step, so `run_tests.py` and both gate invocations see the same environment:

```yaml
      - name: Install Python assurance toolchain
        run: python -m pip install -r DynaDocs.Tests/coverage/requirements.lock

      - name: Install Node assurance toolchain
        run: npm ci
        working-directory: DynaDocs.Tests/coverage
```

What pins them: `requirements.lock` is the fully pinned transitive list (8 `==` pins, including
`coverage==7.16.0`) that `gate_tools` already publishes as this gate's Python pin
(`gate_adapter.py:108`); `npm ci` installs strictly from
`DynaDocs.Tests/coverage/package-lock.json`, and `package.json` is the same file `gate_tools`
publishes as the JavaScript pin (`:109`). No version is named in the workflow, so no pin can drift
out of sync with the gate's own tool record. `package.json` declares `engines.node 22.13.0` while the
job pins node 24; the repository has no `.npmrc`, so npm's engine check is advisory and `npm ci`
proceeds — recorded, not relied on: gate 8 sees the install step's exit.

**`Run isolated test adapter` stays.** The `dotnet` coverage row still cannot run on Linux
(`windows_job.py:88-89` is not something an install fixes), so removing `:95-96` would leave the job
with no .NET suite execution. Post-Issue the job runs each of the three suites exactly once —
.NET at `:95-96`, Python and Node inside their coverage rows — which is the Outcome, job-wide.

**`ReleaseWorkflowTests.cs` needs a two-line amendment, requested here.** Every validation assertion
is `Assert.Contains` on the job's text (`ReleaseWorkflowTests.cs:19-28`); two added steps satisfy all
of them unchanged. The one prohibition, `Assert.DoesNotContain("continue-on-error:", validation)` at
`:29`, is respected — neither new step is allowed to carry it, and neither does. Line 24's pin of
`python DynaDocs.Tests/coverage/run_tests.py` stays true. But that only tests whether the existing
assertions survive, while this spec makes the two install steps a standing invariant (*The one
interval that must not open*, below) guarded only by a one-time DYD-166 check — although
`ReleaseWorkflowTests.cs:19-29` is the repository's existing pin for this job's steps, and F1 records
the job as permanently red on `ubuntu-latest`, so a later removal of either install step would not
surface in job status. A consequential regression guard would be missing for a coupling this route
depends on. This spec therefore **requests an ownership amendment for
`DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`, limited to two lines inserted after `:28`**:

```csharp
        Assert.Contains("python -m pip install -r DynaDocs.Tests/coverage/requirements.lock", validation);
        Assert.Contains("run: npm ci\n        working-directory: DynaDocs.Tests/coverage", validation);
```

The amendment travels to the admiral through the captain; this spec states the request, it does not
assume the grant. If the admiral declines, the residual is recorded under F5.

**`.github/workflows/ci.yml` stays empty.** Read at `112ec76c`: `actions/checkout`, `setup-dotnet`,
`dotnet restore`, `dotnet build --no-restore --warnaserror`, `dotnet test --no-build --verbosity
normal` (`ci.yml:13-28`). No facade call anywhere.

**The one interval that must not open.** Between DYD-130's merge and DYD-166's, nothing is removed:
the test rows still execute all three suites. But **the workflow edit and the deferral must land in
the same merge — if the implement hop cannot land the two install steps, DYD-164 must not merge,
because merging derivation alone deletes release validation's only Python and Node suite
executions.** Merge DYD-166 checks this explicitly.

**What is measured, not asserted.** That `pip install` and `npm ci` succeed on the runner, and that
the Python and Node coverage campaigns then complete on Linux, is not traceable to a line — no
Linux campaign has ever run. Gate 8 measures it, with the pass condition above. If the Python join
still fails on Linux after the install (`python_coverage.py:157-165` runs `coverage.combine` and the
counter join after the suite), the Python suite will have *executed* but its verdict will read
`invalid` — a strictly better state than today's route and than the no-edit route, and one the
implement hop must report rather than absorb.

### Interim rule until this lands

Captains run G as `gate static` plus `gate coverage`, not `--force-run` — the admiral's DYD-170
ruling of 2026-09-11 and the Issue's own note. That route already runs each suite once, because the
coverage rows carry the instrumented executions and no test row is selected. The rule retires when
DYD-166 merges.

---

## Plan

**Approach** — defer the test row and read its verdict from the coverage adapter's already-published
report at manifest-declared coordinates, adding one optional manifest key and no result key.
Rejected: reading the coverage row's process exit (conflates policy failure with suite failure,
`gate_adapter.py:185`); teaching `gap_check.py` the adapter report's field names (the runner is
copied byte-for-byte into other projects and must know no adapter shape); deriving whenever both
rows are selected without a declaration (would silently derive a test verdict from a coverage row
that never ran tests — fail-open); a fallback plain run when the campaign breaks (DR 048 §1's
alternate satisfying path, and the 12 minutes back); a new result key such as `derivedFrom` (the
Outcome freezes the result schema and `argv == []` plus `reason` already carry the fact).

**Pattern to copy** —
- `gap_check.py:143-164` (`configured_command`'s exact key-set and shape validation, one diagnostic
  string per defect, row-local failure) — the `suiteVerdict` validation mirrors it exactly.
- `gap_check.py:292-321` (`run_row`'s use of `result()` with `reason=` for a row that launched
  nothing) — the derived row is built the same way; departure: it carries a `reason` even when
  `passed`.
- `gap_check.py:414-428` (`execute_rows`' append-then-print-then-break loop) — the deferral adds a
  placeholder index and one resolution call; departure: one row is printed out of loop order.
- `test_testing_facade.py:31-42, 436-443` (fixture stacks, `--force-run` probe with side-effect
  files as the "did it run" witness) — every new probe copies this shape.
- `TestingFacadeSteps.cs:214-232` (scenario title → probe name) — every new scenario gets one entry.

**Files** — see `owned-paths.json`. Edited: `DynaDocs.Tests/coverage/gap_check.py`,
`dydo/reference/gap-check.example.py` (regenerated), `DynaDocs.Tests/coverage/gap_check.json`,
`DynaDocs.Tests/Features/testing-facade.feature`, `DynaDocs.Tests/Steps/TestingFacadeSteps.cs`,
`DynaDocs.Tests/coverage/tests/test_testing_facade.py`, `dydo/guides/testing-strategy.md`,
`dydo/reference/coverage-tools.md`, and `.github/workflows/release.yml` (the two install steps after
`:93`, nothing else). Empty: `.github/workflows/ci.yml`,
`DynaDocs.Tests/Features/assurance-adoption.feature`,
`DynaDocs.Tests/Steps/AssuranceAdoptionSteps.cs`, `dydo/reference/gap-check.example.json`, all four
coverage adapters.

**Steps** —

1. **Specify (this hop).** Commit this file and `owned-paths.json` only. Production is blocked by
   DYD-130, so no feature file is touched here; the scenarios above enter `testing-facade.feature`
   at step 3. Checkable: `git status --porcelain` shows only the two workspace files, and
   `git diff --cached --name-only` lists exactly those two paths.
2. **Re-pin.** At the post-DYD-130 head, diff every cited line of `gap_check.py`, `gap_check.json`,
   `gate_adapter.py`, `run_tests.py`, `csharp_coverage.py`, `python_coverage.py`,
   `javascript_coverage.cjs`, `testing-facade.feature`, `test_testing_facade.py`,
   `TestingFacadeSteps.cs`, `release.yml`, `ci.yml`. Record "re-pinned to <SHA>" on the Issue, or
   stop with a spec return naming any moved contract. Checkable: the recorded SHA and an unchanged
   list.
3. **Red.** Add the six new probes and amend `test_full_g`, `schema_rows`, `test_active_manifest`;
   add the `ProbeName()` entries and the new `Then`/`When` attributes. Checkable: gate 1 fails on
   assertions, not collection errors; gate 4 fails on assertions, not unbound steps.
4. **Green the facade.** `gap_check.py`: the `suiteVerdict` validation inside `configured_command`;
   a `deferral_applies(stack, capabilities, root)` predicate calling `prepare_row` for coverage then
   test; a `suite_verdict(root, stack, coverage_row)` returning the derived row; the placeholder and
   two resolution points in `execute_rows`. Budget: under 60 added lines in a 470-line file; no new
   module, no new import beyond what is there. Then run `sync_testing_example.py` (no `--check`) in
   the **same hop** to regenerate `dydo/reference/gap-check.example.py`. Checkable: gates 1, 2, 3.
5. **Green the manifest and the docstring together.** Add the three `suiteVerdict` blocks to
   `gap_check.json`; copy the `dotnet` stack verbatim into `testing-facade.feature`'s docstring,
   because `AssertConcreteManifestShape` deep-equals them (`TestingFacadeSteps.cs:201-202`).
   Checkable: gate 4 green, including *Schema 1 has one small concrete manifest and result shape*.
6. **Docs.** `dydo/guides/testing-strategy.md`: the `--force-run` sentence at line 33; the
   configured-row sentence at 105-107; the "Nine rows are configured" paragraph at 120-125 gains one
   sentence that a declaring stack's test verdict comes from its coverage row's single instrumented
   execution. `dydo/reference/coverage-tools.md`: the grammar paragraph at 32-34; the declared-rows
   table at 63-74 gains the declaration; the row-states table at 94-100 gains the derived row and
   its `reason`; the *Coverage collectors* section at 240-246 states that each row's instrumented
   run is the stack's single suite execution under `--force-run`, that the C# campaign is
   Windows-only so a Linux host derives nothing for `dotnet`, and that the Python and JavaScript
   collectors need `requirements.lock` and `DynaDocs.Tests/coverage`'s npm lock installed or they
   cannot launch a suite at all. Checkable: gate 6.
7. **The workflow edit.** Insert the two install steps into `.github/workflows/release.yml`
   immediately after `Setup Node.js` (`:90-93`), verbatim as specified above, with no
   `continue-on-error`. Nothing else in either workflow moves. Then, if the requested ownership
   amendment is granted, insert the two `Assert.Contains` lines specified above after
   `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs:28` — nothing else in that file moves; if it is
   declined, skip this and the residual stands as recorded under F5. Checkable: gate 4 still green
   (`ReleaseWorkflowTests` runs inside the .NET suite), and the diff of `release.yml` is exactly
   those two steps.
8. **Acceptance.** Gates 5, 6, 2, 3, then 7 on the exact clean candidate, then 8. Post each with
   candidate, command, environment, exit and result location, per the workspace standard. Gate 8 is
   the only place the workflow edit can be proved; if it shows a derived row's verdict for a suite
   with no output in the log, stop and report the mismatch.
9. **Harden**, then fresh whole-change review, then Merge DYD-166 — after DYD-130 is Done, before
   DYD-131. Merge DYD-166 refuses a tree that carries the deferral without the workflow edit.

**Edge cases** —

| Input or state | Behaviour |
|---|---|
| `suiteVerdict` on a `test`, `static` or `mutation` row | that row is `invalid` 2, `invalid suite verdict declaration`; peers run |
| `suiteVerdict` on an `unavailable` coverage row | today's unavailable path is unchanged: `valid_unavailable` (`gap_check.py:125-130`) already rejects unknown keys, so the row is `invalid` 2 |
| coverage row declares zero or two required artifacts with `suiteVerdict` | `invalid` 2; the report has no unambiguous path |
| `exit` resolves to `true` | not an integer → not established → test row `invalid` 2 |
| `failure` walk reaches a non-array | no match → a nonzero exit is not attributed → `invalid` 2 |
| `failure` declared with fewer than two items (matcher against the report root) | declaration defect at prepare time: coverage row `invalid` 2, no deferral, test row runs plainly |
| a report grows a second collector | a `failure` scoped under `["collectors", "<c>"]` cannot see the other collector's findings; gate 7 condition 4 fails first and the change is caught, not absorbed |
| the coverage row is `invalid` 2 yet its report records a usable `child_exit` (inventory errors, `gate_adapter.py:477-480`) | rule 2 wins with no exception: test row `invalid`, `childExit` null, exit 2; `R` is never opened |
| the coverage tool is absent so the collector's child fails at launch (Node without `node_modules`) | derived test row `failed` 1 for a suite that never ran — the adapter's own misclassification, F4 |
| report parses but is an array or a scalar | key walk fails → `invalid` 2 |
| deferred stack, test row itself `unavailable` or `invalid` at prepare time | no deferral; reported as today without running, coverage still runs |
| `--stack` narrowing under `--force-run` | `--force-run` accepts no options (`gap_check.py:347`); unreachable |
| a stack selected twice | impossible: duplicate names are globally invalid (`gap_check.py:82-83`) |
| the coverage row publishes a report but the freshness check fails | coverage row `invalid` 2 → test row `invalid` 2; the stale report is never read |
| a project adopts the runner with no coverage rows | no declaration, no deferral, today's behaviour |

**Risks a spec reviewer must probe** —

1. **Double counting.** Does any path let one suite execution produce two `passed` rows that a
   reader could take as two runs? The derived row's empty `argv` and its `reason` are the answer;
   check that gate 7's condition 2 is unfalsifiable by a row that did launch a process.
2. **A masked test failure.** Walk each branch of the per-case table against `gate_adapter.py`:
   is there any reachable state where the suite failed and the derived row is `passed`? The only
   `passed` branch requires `child_exit == 0`, which for all three stacks requires the suite's own
   process to have exited 0.
3. **Result-schema drift.** No new key; confirm against `test_testing_facade.py:521-523` and
   `testing-facade.feature:182-185`, and that a `reason` on a `passed` row breaks nothing.
4. **Manifest-schema drift.** The key is optional and coverage-only; confirm
   `dydo/reference/gap-check.example.json`, every fixture manifest and `test_portable`
   (`test_testing_facade.py:670-691`) are untouched, and that the `gap_check.json` change is
   mirrored byte-for-byte in the feature docstring.
5. **Workflow drift.** Confirm the Linux argument for keeping `release.yml:95-96`; confirm the two
   install steps are the whole `release.yml` diff, carry no `continue-on-error`
   (`ReleaseWorkflowTests.cs:29`), and satisfy every `Assert.Contains` at `:19-28` unchanged; confirm
   `ci.yml` genuinely needs nothing. Then probe the substance: after the edit, does each of the three
   suites execute exactly once in the validation job, and can any derived row report a verdict for a
   suite that produced no output?
6. **DYD-103 interface drift.** Confirm `artifact_error`, `EXITS`, `completed_row`, `CAPS` and the
   mutation path are byte-unchanged.
7. **Portable-example honesty.** `gap_check.py` must gain no knowledge of DynaDocs' adapters; check
   that every adapter-shaped string lives in `gap_check.json` and none in the runner, and that
   `sync_testing_example.py --check` passes in the same hop as the runner edit.
8. **Print-order change.** Confirm no existing scenario, probe or doc pins the live order of
   `--force-run`'s rows.

**Plan review** — `recommended`. It changes the public facade's execution plan, which governs every G
in this repository and every downstream adopter of the byte-copied runner; it adds a manifest-schema
key; and it settles three things the parent criterion left open (the coverage row's true exit on a
suite failure, the release workflow's third execution, and whether the validation job must install
the coverage toolchain for `--force-run` to measure anything on `ubuntu-latest`).

---

## Named items for the admiral

- **F1 — pre-existing, not this Issue's to fix.** The release `validation` job cannot pass on
  `ubuntu-latest` on this base: the C# coverage campaign requires Windows and CPython 3.12.14
  (`windows_job.py:88-89`) while the job pins `ubuntu-latest` and Python 3.13
  (`release.yml:71, :83`), and `gate mutation` at `release.yml:108` is `unavailable` pending DYD-103.
  Measured, not assumed: Actions run `34557358149` on `c4b2f1d1` failed at `Run coverage gate` with
  `Aggregate: 2` and skipped the mutation step. Gate 8 is therefore specified as a before/after
  comparison on the same candidate, not as a green job. Someone owns making that job passable;
  DYD-164 does not.
- **F5 — decided inside the Owned path, recorded because it changes a workflow.** Post-DYD-130, the
  validation job's coverage rows cannot measure anything on `ubuntu-latest` because the job installs
  neither `requirements.lock` nor the `DynaDocs.Tests/coverage` npm lock. **Left alone, DYD-164 would
  delete release validation's only Python (138 tests on the measured base; larger after DYD-130) and
  Node suite executions and publish a false `failed` 1 for the Node suite that never ran.** This
  spec therefore adds two install steps to `.github/workflows/release.yml` after `:93` — inside the
  Owned path, at the point where the facade is invoked — so the derived rows are real. It also
  **requests one ownership amendment**, for `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`,
  limited to the two `Assert.Contains` lines inserted after `:28` that pin those two install steps;
  residual if the admiral declines: the two steps become a standing invariant guarded only by
  DYD-166's one-time merge check, and because F1 records the validation job as permanently red on
  `ubuntu-latest`, a later removal of either step would not surface in job status. If the admiral
  would rather the workflow stay untouched, the honest alternative is to
  **remove the `python` and `node` `suiteVerdict` declarations from `gap_check.json` until a Linux
  route exists**, keeping their plain test rows; the cost is that a Windows G still runs those two
  suites twice, which is most of what this Issue exists to remove. Recommendation: take the install
  steps.
- **F4 — a follow-up Issue, not DYD-164's and not DYD-96's blocker.** `javascript_coverage.cjs:164`
  returns c8's status without distinguishing "the suite failed" from "c8 itself failed to start", and
  `gate_adapter.py:412` turns any child `1` into a `functional` finding. The coverage row has
  misattributed this since DYD-96; DYD-164 only makes it visible on a test row. It is not fixable
  inside DYD-164's Owned paths: the adapter clause permits an adapter change **only if the verdict
  cannot be derived from what the adapters already record**, and here it can — what is wrong is the
  adapter's classification of its own launch failure, not the recording. The fix belongs where the
  fact is produced (`javascript_coverage.cjs` should raise rather than return 1 when the c8 entry
  point is missing, so `gate_adapter` records an error and the row is `invalid` 2). Worth an Issue;
  gate 8's condition 2 keeps it from being signed off as expected behaviour in the meantime.
- **F2 — description clarification.** The Outcome's "the coverage row is invalid 2 as today" for a
  failing test is inaccurate; today it is `failed` 1. The spec keeps today's behaviour and states the
  real table.
- **F3 — recorded residue.** `DynaDocs.Tests/coverage/tests/test_csharp_metrics.py` runs inside the
  .NET campaign and inside the `python` test row. Seconds of work, out of this Issue's scope,
  worth an Issue only if someone measures it as material.
- **Exactly one ownership amendment is requested, and nothing more.**
  `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`, limited to two lines inserted after `:28` — the
  two `Assert.Contains` lines that pin the install steps, given verbatim in *Decision — the
  validation job installs the coverage toolchain*. Every other path the route needs is already
  inside the Issue's Owned paths, including `.github/workflows/release.yml`, which the Owned paths
  grant "where they invoke the facade". The amendment travels to the admiral through the captain;
  this spec states the request, it does not assume the grant, and F5 records the residual if it is
  declined.
- **Released after two review rounds, under the human's standing rule.** Round 1 reviewed candidate
  `192dbf07` and FAILed on three findings, corrected in candidate `8595fa46`. Round 2 reviewed
  `8595fa46` and FAILed on three findings — gate 4's filter selecting nothing, gate 8's condition 1
  naming witnesses that do not exist, and the missing ownership amendment for
  `ReleaseWorkflowTests.cs`. Those three are folded into this file verbatim in substance, nothing
  else was changed, and the spec is released without a third review.
