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
be the suite's verdict rather than a broken measurement, one finding `{"gate": "functional",
"child_exit": N}` tagged with its collector (`gate_adapter.py:366-374` python, `:400-412` node,
`:430-439` dotnet; aggregation `:277-288`; publication `:171-185`). Collector names are
`python-coverage`, `javascript-coverage`, `csharp-coverage`.

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
    "failure": ["findings", {"collector": "csharp-coverage", "gate": "functional"}]
  }
}
```

`python` uses `python-coverage`, `node` uses `javascript-coverage`, in both fields. Exactly these
three rows in `gap_check.json` gain the key.

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
2. `C.state == "invalid"` → test row `invalid`, `childExit` null, resultExit 2. (`C.state ==
   "unavailable"` is unreachable here: prepare-time unavailability means no deferral.)
3. `C.state in ("passed","failed")` — and only then, because those two states are exactly the ones
   whose required artifact passed the freshness check (`gap_check.py:310-314`), so `R` is this
   child's own report and never a stale one. Read `R`, walk `suiteVerdict.exit` through object keys:
   - absent / unreadable / not JSON / key missing / value not an `int` (a `bool` is not an `int`
     here) → test row `invalid`, resultExit 2;
   - value `0` → test row `passed`, `childExit` 0;
   - value `N != 0` and `suiteVerdict.failure` matches (walk its leading keys to an array; some
     element is an object containing every declared key with the declared value) → test row
     `failed`, `childExit` N, resultExit 1;
   - value `N != 0` and no match → test row `invalid`, resultExit 2.

Applied to the real adapters, with the coverage row's own behaviour unchanged:

| Situation | coverage row | test row | aggregate |
|---|---|---|---|
| suite passes, policy passes | `passed` 0 | `passed`, childExit 0, exit 0 | 0 |
| suite passes, policy fails | `failed` 1 (findings, `child_exit` 0) | `passed`, childExit 0, exit 0 | 1 |
| suite fails | `failed` 1 (`functional` finding, `child_exit` N) | `failed`, childExit N, exit 1 | 1 |
| campaign could not measure — build/prepare/instrumentation/tool/join failure, or the adapter raised early so `collectors` falls back to `report["facts"]` (`gate_adapter.py:182`) and the coordinate resolves to nothing | `invalid` 2 | `invalid`, childExit null, exit 2 | 2 |
| adapter classified a nonzero child exit as a broken campaign (`child not in (0,1)`, `gate_adapter.py:401, :431`) | `invalid` 2 | `invalid`, childExit null, exit 2 | 2 |
| inventory errors with the suite's exit still recorded (`gate_adapter.py:477-480` keeps `facts`) | `invalid` 2 | derived normally from `child_exit` | 2 |
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

**New.**

1. *One instrumented execution carries both the coverage measurement and the test verdict* — a stack
   whose coverage row declares a suite verdict and publishes `child_exit` 0; `--force-run`; the test
   command's side-effect file is absent, the coverage command's is present, rows are test/static/
   coverage in manifest order, the test row is `passed` with `childExit` 0, empty `argv` and the
   derivation reason, aggregate 0. Probe `test_derived_test_row_passes_from_the_instrumented_run`.
2. *A derived test verdict fails closed* — Scenario Outline over the per-case table above, one
   example per row of it (`suite passes and policy passes`, `suite passes and policy fails`,
   `suite fails`, `the campaign could not measure`, `the report records no suite exit`,
   `the coverage row did not attribute the child exit to the suite`), each naming the test row's
   state, childExit and resultExit, the coverage row's state and resultExit, and the aggregate.
   One probe per example.
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
| 4 | `$P DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~TestingFacade"` | exit 0; every scenario of the feature binds and passes | iteration |
| 5 | `dotnet build DynaDocs.sln -c Release -warnaserror` | exit 0 | acceptance |
| 6 | `dotnet bin/Release/net10.0/dydo.dll check` | 0 errors, 0 warnings | acceptance |
| 7 | one full G on the exact clean candidate: `$P DynaDocs.Tests/coverage/gap_check.py --force-run` | the row evidence below | acceptance |
| 8 | the release workflow's non-publishing dry run on the candidate | the comparison below | acceptance |

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
   instrumented execution per stack — and its `exit` equals the derived test row's `childExit`.

Corroboration, recorded not asserted: the G's wall clock against the 2026-09-10 baseline of 46
minutes; the expected drop is roughly the 12 minutes measured there.

**Gate 8's comparison.** `workflow_dispatch` on the branch runs `build` and `validation` only;
`release`, `nuget` and `npm` are tag-guarded (`release.yml:111-112`), so the dispatch publishes
nothing. The job's own exit is compared **before and after on the same candidate**, not required to
be 0 — see flag F1. Pass condition: the same set of failing steps with the same reasons as the base,
the `Run isolated test adapter` step (`release.yml:95-96`) exiting 0 exactly once, and the
`Run coverage gate` step's `result.json` showing the `python` and `node` test rows derived and the
`dotnet` test row `invalid` with the not-established reason.

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

**DYD-103 spec gate 9 (line 630) — "`$P DynaDocs.Tests/coverage/gap_check.py --force-run` | identical
selected rows and aggregate before and after DYD-103's edits on the same base; mutation never
selected."** Unchanged, and DYD-103 needs no re-pin on DYD-164's account. That gate compares two legs
on the base DYD-103 pins; DYD-164 is not one of DYD-103's edits, so if its base includes DYD-164 both
legs derive identically and if it does not, neither does. Selected rows are unchanged; mutation is
still never selected by `--force-run`. DYD-103 consumes only the facade's freshness behaviour
(`artifact_error`, `gap_check.py:273-280`) and the 0/1/2/130 mapping (`EXITS` `:27`, `completed_row`
`:283-289`); **DYD-164 changes neither**, and nothing it adds applies to a `mutation` row, to
`gate mutation`, or to any row of a stack whose coverage capability carries no `suiteVerdict`.

**The release workflow.** `release.yml:105` keeps calling `--force-run` and is not edited. It does
not simply get faster: the validation job runs on `ubuntu-latest` with Python 3.13, where the C#
coverage campaign cannot execute at all — `windows_job.validate` raises on `os.name != "nt"` or a
Python other than 3.12.14 (`windows_job.py:88-89`), so `_run_assurance_campaign` returns 2
(`run_tests.py:209-211`) and `collect_dotnet_coverage` records a broken campaign
(`gate_adapter.py:431-435`). The `dotnet` coverage row is therefore `invalid` 2 there and the derived
`dotnet` test row is `invalid` 2 with its reason — the .NET suite is not run by `--force-run` on that
host. What the job does get is the removal of the duplicate .NET execution: today the explicit
`run_tests.py` step **and** the `--force-run` dotnet test row each run the suite; after this Issue
the explicit step is the only .NET suite execution in the job, and the Python and Node suites drop
from two executions to one each. On a Windows host with the pinned interpreter, `--force-run` gets
faster by the full measured margin.

### The release.yml third execution — decided, no edit

Finding E asked whether to remove `release.yml:95-96`. **Keep it, and touch neither workflow.**
Removing it would leave the release validation with no .NET suite execution at all, because the
derived `dotnet` test row cannot be established on `ubuntu-latest` (above). Post-Issue the job runs
the .NET suite once, the Python suite once and the Node suite once, which is exactly the Outcome's
scope; the step is the single execution, not a duplicate. `ReleaseWorkflowTests.cs:24` stays correct
and is **not** needed — no ownership amendment for it. `.github/workflows/ci.yml` was read at
`112ec76c` and contains no facade call: `actions/checkout`, `setup-dotnet`, `dotnet restore`,
`dotnet build --no-restore --warnaserror`, `dotnet test --no-build --verbosity normal`
(`ci.yml:13-28`). Both workflow paths are declared **empty**.

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
`dydo/reference/coverage-tools.md`. Empty: `.github/workflows/release.yml`,
`.github/workflows/ci.yml`, `DynaDocs.Tests/Features/assurance-adoption.feature`,
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
   run is the stack's single suite execution under `--force-run`, and that the C# campaign is
   Windows-only so a Linux host derives nothing for `dotnet`. Checkable: gate 6.
7. **Acceptance.** Gates 5, 6, 2, 3, then 7 on the exact clean candidate, then 8. Post each with
   candidate, command, environment, exit and result location, per the workspace standard.
8. **Harden**, then fresh whole-change review, then Merge DYD-166 — after DYD-130 is Done, before
   DYD-131.

**Edge cases** —

| Input or state | Behaviour |
|---|---|
| `suiteVerdict` on a `test`, `static` or `mutation` row | that row is `invalid` 2, `invalid suite verdict declaration`; peers run |
| `suiteVerdict` on an `unavailable` coverage row | today's unavailable path is unchanged: `valid_unavailable` (`gap_check.py:125-130`) already rejects unknown keys, so the row is `invalid` 2 |
| coverage row declares zero or two required artifacts with `suiteVerdict` | `invalid` 2; the report has no unambiguous path |
| `exit` resolves to `true` | not an integer → not established → test row `invalid` 2 |
| `failure` walk reaches a non-array | no match → a nonzero exit is not attributed → `invalid` 2 |
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
5. **Workflow drift.** Confirm the Linux argument for keeping `release.yml:95-96`, and that
   `ReleaseWorkflowTests.cs:24` and `ci.yml` genuinely need nothing.
6. **DYD-103 interface drift.** Confirm `artifact_error`, `EXITS`, `completed_row`, `CAPS` and the
   mutation path are byte-unchanged.
7. **Portable-example honesty.** `gap_check.py` must gain no knowledge of DynaDocs' adapters; check
   that every adapter-shaped string lives in `gap_check.json` and none in the runner, and that
   `sync_testing_example.py --check` passes in the same hop as the runner edit.
8. **Print-order change.** Confirm no existing scenario, probe or doc pins the live order of
   `--force-run`'s rows.

**Plan review** — `recommended`. It changes the public facade's execution plan, which governs every G
in this repository and every downstream adopter of the byte-copied runner; it adds a manifest-schema
key; and it settles two things the parent criterion left open (the coverage row's true exit on a
suite failure, and the release workflow's third execution).

---

## Named items for the admiral

- **F1 — pre-existing, not this Issue's to fix.** The release `validation` job cannot pass on
  `ubuntu-latest` on this base: the C# coverage campaign requires Windows and CPython 3.12.14
  (`windows_job.py:88-89`) while the job pins `ubuntu-latest` and Python 3.13
  (`release.yml:71, :83`), and `gate mutation` at `release.yml:108` is `unavailable` pending DYD-103.
  Gate 8 is therefore specified as a before/after comparison on the same candidate, not as a green
  job. Someone owns making that job passable; DYD-164 does not.
- **F2 — description clarification.** The Outcome's "the coverage row is invalid 2 as today" for a
  failing test is inaccurate; today it is `failed` 1. The spec keeps today's behaviour and states the
  real table.
- **F3 — recorded residue.** `DynaDocs.Tests/coverage/tests/test_csharp_metrics.py` runs inside the
  .NET campaign and inside the `python` test row. Seconds of work, out of this Issue's scope,
  worth an Issue only if someone measures it as material.
- **No ownership amendment is requested.** Every path the route needs is inside the Issue's Owned
  paths, and `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs` is not needed because the release
  workflow is not edited.
