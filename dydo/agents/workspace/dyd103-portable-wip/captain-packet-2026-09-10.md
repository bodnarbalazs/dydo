# DYD-103 captain packet — exclusive preparation, 2026-09-10

Written by the issue-captain of DYD-103 (requested model `claude-opus-5`, effective identity
unavailable). This packet is the resume point for the next captain. It is git-ignored; the Linear
record carries the same facts in compact form.

## Where the work is

| Thing | Value |
|---|---|
| Preparation branch | `DYD-103-mutation-prepare` |
| Base SHA | `e887cd078b4b14ad555534a8f248d33bc28af49f` (DYD-96 CODE/interface PASS checkpoint) |
| Worktree | `C:/Users/User/Desktop/Projects/DynaDocs/.worktrees/dyd103-prepare` |
| Spec branch (backup, reviewed) | `DYD-103-mutation-gate` @ `77157b1c`, pushed to `origin` |
| Retained historical branch | `DYD-103-mutation-assurance` @ `70cf3a5e`, untouched, worktree `.worktrees/dyd103` |

Ancestry verified with `git merge-base --is-ancestor`: the original interface-subset PASS
`fe3855f5`, the spec base `1bc93c5d` and the reviewed feature head `40c35967` are all ancestors of
the base `e887cd07`, which is itself an ancestor of DYD-96's current tip.

## Hops on the branch, in order

| SHA | Hop | What |
|---|---|---|
| `a763540b` | merge | the six reviewed specify hops of `DYD-103-mutation-gate` brought in unsquashed |
| `db0d0ead` | implement (spec step 2) | three engines restored; exclusive `mutation/` manifests, locks, templates, `.gitignore` |
| `007c38a7` | implement (spec step 3) | 24 real engine fixtures + `origin.json` provenance |
| `556000a9` | specify | first bounded amendment: the two unimplementable Cosmic Ray mechanisms |
| `4045bdb5` | specify | closes spec review 1's four findings plus the authorized denominator fix |
| `1d7f9f2c` | specify | binds the completion marker to its campaign (spec review 2's finding) |
| `89108401` | implement (step 3 reopened) | eleven Cosmic Ray sessions recaptured through the specified runner |
| `4c643168` | implement (step 4) | red tests and the Reqnroll bindings, with importable stubs |
| `087e1820` | implement (step 5) | `mutation_summary.py` green |
| `a216f8e8` | implement (step 6) | `mutation_adapter.py`, 883 lines |
| `b147cd90` | fix | four proved facade-harness defects; the adapter's special `widened` reading deleted |

Two independent spec review rounds ran on the amendment: round 1 FAILed `556000a9` with four
findings, round 2 FAILed `4045bdb5` with one. Both are folded in. Two rounds is this Project's
standing limit, so `1d7f9f2c` carries the second round's correction as the reviewer wrote it and
production resumed on it without a third round.

## Consumed DYD-96 pins, verified at the base

All seven checked with `git rev-parse HEAD:<path>` and unchanged from DYD-96's renewed checkpoint:

| Path | Blob |
|---|---|
| `DynaDocs.Tests/coverage/inventory.py` | `c5ab1c662475903445a435e6f641b042f1d24107` |
| `DynaDocs.Tests/coverage/gate_inventory.py` | `34c6e45c3db15d9d5a8047359a3b51e7f3784601` |
| `DynaDocs.Tests/coverage/run_tests.py` | `c023fea2ccc7ccbf3002c9b68560a671b96099b0` |
| `DynaDocs.Tests/coverage/windows_job.py` | `5b9ae1cbb978a27778c659c76590ccfcb5bdeb05` |
| `DynaDocs.Tests/coverage/gate_adapter.py` | `ef8c20a23ef7d53befa653c007088aebdfc67018` |
| `DynaDocs.Tests/coverage/gap_check.py` | `a65ccdef2bdd990056b7bfbc271e9fc59d1410e3` |
| `dydo/reference/gap-check.example.py` | `a65ccdef2bdd990056b7bfbc271e9fc59d1410e3` |

**No consumed pin departed during this commission.** The `execution_seconds_maximum` keyword the spec
consumes is present on both `windows_job.validate` (line 82) and `windows_job.run` (line 441) at the
base, so step 1's keyword condition holds here; the post-DYD-130 re-pin still owes its own check.

## Gate baseline — pre-code, and unchanged by every hop

`$P` is the bundled facade Python 3.12.14
(`C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`).
Run every gate with `C:/WINDOWS/system32` first on PATH (Windows `tar`, not the MSYS one) and the
Codex PowerShell bundle after it (`pwsh` 7 is not installed on this host).

| Gate | Pre-code at `a763540b` | Still true |
|---|---|---|
| 2 — `$P -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_*.py"` | 330 tests, 7 failures, 10 errors, 2 skips, exit 1 | yes, at every hop |
| 5 — `dotnet build DynaDocs.sln -c Release --warnaserror` | 0 warnings, 0 errors | yes |
| 6 — `dydo check` (installed 3.0.0-beta.3 == csproj) | 0 errors, 0 warnings, 821 files | yes |
| 7 — `$P DynaDocs.Tests/coverage/gap_check.py capabilities` | exit 0, mutation `unavailable: Pending DYD-103` on all three stacks | yes |

The 17 gate-2 failures are all in DYD-96-owned dependency-bearing collector test modules
(`test_python_metrics`, `test_csharp_coverage`, `test_csharp_metrics`, `test_javascript_metrics`,
`test_knip`, `test_source_tokens`, `test_diagnostics`, `test_python_coverage`). The base `e887cd07`
predates DYD-96's batch A, which its captain reports takes the suite to 433 tests exit 0. **This set
is DYD-96's to close; DYD-103's obligation is to add nothing to it.** Full list in
`evidence/precode-gate2.txt`; the reasoning in `evidence/precode-baseline-note.md`.

Do not re-run gate 2 under `dydo/_system/.local/static-gates/python`. That venv is DYD-96's
collector-dependency interpreter and is strictly worse here (48 errors, 19 failures): it fails the
whole `test_windows_job.py` native-boundary set and the Node execFile/spawn/fork set, because
`windows_job.preflight()` and those probes need the exact CPython the facade gates name.

## What this commission changed in the contract

Production stopped twice because the accepted spec could not be implemented, and both stops were
verified at primary source before anything moved. The corrections are recorded inside
`specification.md` under **Ruled — 2026-09-10**; the short version:

1. **The Cosmic Ray kill witness.** The accepted spec read a `Ran N tests in` line out of the
   engine's recorded `output`. `cosmic_ray/testing.py` discards stderr (line 70) and unittest writes
   that summary to stderr, so the line is never there and every kill became a `timeout` finding —
   the python stack could not pass. Replaced by an adapter-generated in-process suite runner that
   merges stderr into a UTF-8-reconfigured stdout and writes a completion marker as its last act,
   with the marker bound to its own campaign so the mutated suite cannot forge it.
2. **The Cosmic Ray command rendering.** `subprocess.list2cmdline` against the engine's POSIX
   `shlex.split` (line 63) destroyed Windows backslashes. Replaced by `shlex.join` with JSON
   escaping of the TOML basic string; both layers proven necessary.
3. **`incompetent` was fail-open.** The accepted mapping `incompetent → compileError, generated
   only, excluded from valid` dropped a mutant whose run never happened out of the denominator while
   the campaign stayed able to pass. On 8.7.0 under the `local` distributor `INCOMPETENT` only ever
   means `run_tests` itself raised, so it is now gap 2, `engine could not run mutant`.
4. Report validation is now an ordered list (zero-mutant before per-file presence), report paths are
   normalized from vendor absolute spellings to canonical inventory paths, and the unreachable
   worker outcomes are named as unreachable.

5. **The kill witness was forgeable.** The marker was identified positionally, as the last non-empty
   line — which separates the runner's line from the suite's own only when the runner survives to
   write last, exactly not the case the witness exists for. A test printing the literal and then
   calling `os._exit(3)` scored as a kill, and the literal is guaranteed present in the mutated
   python stack because `test_mutation_summary.py` carries it as fixture data and
   `mutation_summary.py` carries it as its match pattern. The marker now carries a per-invocation
   `uuid.uuid4().hex` nonce, and `mutation_summary` takes that nonce as an explicit argument.

Four of these were fail-open holes in a gate whose entire purpose is that invalid measurement
cannot pass. Each was found by running the engine rather than reading it, and each was caught by an
independent reviewer or by production, never by the author of the text that contained it.

**The nonce's honest bound, recorded in the spec and worth not forgetting.** The nonce defeats a
marker spelled by text committed in this repository, which is the real threat. It cannot defeat a
suite that actively reads the generated `suite_runner.py` it was launched by and lifts the nonce
from it — measured, and true of any value, because the runner must hold the nonce in cleartext in a
file the suite can read. The spec states this bound rather than glossing it. The nonce must be
freshly generated per invocation and never derived from the candidate, the base or any committed
value; a derived nonce could be spelled in advance by the very text it defends against.

## Human ruling on test selection — 2026-09-10, above the Issue contract in precedence

**Run only the relevant tests during iteration, and never run the same suite twice inside one gate
run.** Iteration hops and hop reviews run only the touched test modules, the fixtures the change
reaches, and the cheap static checks; the hop reviewer verifies that selection instead of rerunning
the full Python suite. Full suites run only at gates.

Two consequences for the work still owed:

1. **The post-DYD-130 full G runs `gate static` plus `gate coverage`, not `--force-run`**, so each
   suite executes once inside its coverage row.
2. **Spec gate 9 collides with that and must be reconciled at step 7.** Gate 9 is written as
   `$P DynaDocs.Tests/coverage/gap_check.py --force-run`, proving DYD-103's edits leave the selected
   rows and aggregate identical on the same base and that mutation is never selected. That is a
   claim about the *plan*, but the command executes the rows — the double run the ruling forbids.
   Satisfy its purpose by comparing the selected plan rather than executing it, or fold it into the
   `gate static` + `gate coverage` pair, and record which, because the spec text says `--force-run`.

DYD-164, blocked by DYD-130 and merging before DYD-131, changes `gap_check.py`'s execution plan
under `--force-run`. The post-DYD-130 re-pin absorbs that blob change in the same step. It touches
neither the required-artifact freshness rule nor the 0/1/2/130 mapping DYD-103 consumes, so it is
not a spec return — but it is a second reason to reconcile gate 9 deliberately rather than run it as
written.

## Still owed, in order

1. **Step 3 recapture** (spec review 1, finding 4, owner implementer). None of the seven Cosmic Ray
   fixtures at `007c38a7` meets the amended step 3: four carry the raw manifest argv,
   `cosmic-ray-killed.sqlite` came through a hand-written runner witnessing the refuted
   `Ran N tests in` line, `cosmic-ray-incompetent.sqlite` names a non-existent executable, and
   `cosmic-ray-survived.sqlite` is DYD-96's retained probe with no recorded configuration. Step 3
   now carries an explicit session table naming subject, expected native rows and the reading each
   carries. **The Stryker fixtures are unaffected and must not be recaptured.**
2. **Step 4** — red tests: `test_mutation_summary.py`, `test_mutation_adapter.py`,
   `test_mutation_facade.py`, and `Steps/MutationAssuranceSteps.cs` mapping every scenario title and
   outline argument to one named probe.
3. **Steps 5 and 6** — green `mutation_summary.py`, then green `mutation_adapter.py`, with every
   `windows_job.run` call carrying `execution_seconds_maximum=14400`.
4. **Independent CODE review** of the whole preparation, then release.

Steps 7-10 and every serial-after-DYD-130 path stay forbidden until DYD-130 is `Done`.

## Decisions a later captain should not have to rediscover

- **The dotnet engine cannot be restored through the manifest here.** `.config/dotnet-tools.json` is
  serial-after-DYD-130. `dotnet-stryker` 4.16.0 is installed to the git-ignored tool path
  `dydo/_system/.local/mutation/dotnet-tools` for fixture capture only; the restore-command text the
  adapter records stays `dotnet tool restore`, which step 7 makes true. The tool-presence check
  exercises the real command in a temporary directory outside the repository.
- **Two facade probes cannot be green before DYD-130.** The feature's first two scenarios bind the
  *real* project manifest (`capabilities` reporting `mutation: configured`, and each stack's row
  naming the adapter argv), which only step 7's `gap_check.json` edit satisfies. Author them in step
  4 with `skipUnless` on the real manifest actually carrying configured mutation rows, commented with
  DYD-130 and step 7, so they activate the moment those rows land. Gate 1 at this boundary is
  therefore exit 0 with exactly those two recorded skips; gate 4 stays production-only per the
  spec's own gate table.
- **Reviewer's recorded non-binding item, for the hardener:** the adapter could decode the baseline's
  `stdout.log` strictly as UTF-8, so a suite the engine would record `INCOMPETENT` fails once at the
  baseline instead of once per mutant. Fail-closed either way; deliberately not grown into the spec.

### Captain ruling — the Cosmic Ray session is read with stdlib `sqlite3`, not `cosmic_ray.work_db`

**This is a deliberate, recorded departure from the spec text**, ruled by the captain after the step-4
implementer surfaced the conflict, and handed to the CODE reviewer to judge rather than absorbed
quietly.

The spec pins `cosmic_ray.work_db.use_db(path, mode=WorkDB.Mode.open)`. The gate interpreter `$P` has
no `cosmic_ray`, so a `work_db` reading could not be unit-tested without either a third permanent
skip or a hard dependency on the restored mutation venv — and untested code inside a fail-closed
gate is a worse outcome than a vendor API forgone. The spec's own facade design shims only
`cosmic_ray/cli.py`, which cannot serve a `work_db` read-out either, so the pinned API and the
pinned test design were already in tension.

`read_cosmic_session` therefore reads `work_items`, `mutation_specs` and `work_results` with the
standard library, against sessions the engine really produced. The condition that makes this safe is
part of the ruling: **the reading validates the schema it expects and fails closed with gap 2 on any
deviation** — a missing table, an unexpected column, an unknown enum value. The engine is pinned to
exactly 8.7.0 by `mutation/requirements.lock`, so the schema is fixed; if a future version changes
it, that must surface as invalid measurement, never as a silent misreading. The
`--read-cosmic-session` subcommand, its `--marker-nonce` argument and its execution under the venv
interpreter are unchanged from the spec.

### Captain ruling — the facade probes may share one prepared project

Each facade probe builds a temporary repository whose inventory the real DYD-96 producer evaluates,
which means a `dotnet restore` and an MSBuild inside every snapshot, roughly sixty times. Red costs
nothing because the stub never runs it, but at green that makes gate 1 expensive enough to stop
being run. A session-scoped prepared project, built once and copied per probe, is authorized —
**provided every probe still exercises the real producer, the real `gap_check.py` and the real
adapter.** Sharing the build is allowed; sharing or faking the measurement is not.

### Recorded, not acted on

- The step-4 map has one many-to-one: the three rows of the DYD-130-bound outline share a single
  probe, forced by the two-skip limit, since per-row probes would have meant four skips.
- Gate 4's `RunProbe` asserts exit 0, `Ran 1 test` and no `skipped`, so the two DYD-130 scenarios
  *fail* gate 4 until step 7 lands the manifest rows rather than passing on a skip. Gate 4 is
  production-only, so nothing at this boundary depends on it.

## Coordination

DYD-96 is live under another captain on `codex/DYD-96-assurance-adoption` (`.worktrees/dyd96-adoption`),
with one test-only fix hop, a full G, a hardener, final review and a PR still to run. Never write in
`.worktrees/dyd96-adoption`, `.worktrees/dyd96`, `.worktrees/dyd103`, `.worktrees/dyd103-spec` or the
main checkout. Before anything expected to hold the CPU past about five minutes, check
`.worktrees/dyd96-adoption/DynaDocs.Tests/coverage/results/g-final-*/pid.txt` and whether that
process is alive; DYD-96's authoritative G campaigns run about 45 minutes.

## Evidence

Everything raw is git-ignored under `dydo/agents/workspace/dyd103-portable-wip/evidence/` in the
preparation worktree: `before-first-edit.txt`, `precode-gate2.txt`, `precode-gate2-venv.txt`,
`precode-baseline-note.md`, `precode-gate6-7.txt`, `step2-*`, `step3-*`, `hop1-gate2.txt`,
`hop2-gate2.txt`, `hop2-build.txt`, `spec-amend-cosmic-ray-*`, `spec-review-close-cosmic-ray-*`.

## Final state of this commission — 2026-09-11

Released at **`9f6d7591`** on `DYD-103-mutation-prepare`, pushed to origin, worktree clean.
Spec Plan steps 1-6 are done. Steps 7-10 and every serial-after-DYD-130 path remain untouched.

Later hops, after those listed above:

| SHA | Hop | What |
|---|---|---|
| `052c9573` | fix | the four round-one CODE findings; 16/16 planted mutants killed |
| `1d3f0fd6` | specify (captain) | corrected the refuted Cosmic Ray path-spelling sentence |
| `5a6f58d3` | fix | the four round-two CODE findings, with real-engine measurement |
| `9f6d7591` | specify (captain) | `module_path` equality is path identity, not bytes |

Two independent CODE review rounds ran. Round one planted 12 mutants at the fail-closed seams and
**11 survived**; round two, after those were closed, found that the fix for its own finding 3 had
**broken the real python path** — a byte comparison of `module_path` against an engine that stores
`str(Path(...))`, so every nested python target would have gone `invalid`/2. It survived because the
test shim spelled a path unlike the engine it stands for.

**The lesson worth carrying to step 7 and beyond: a shim that does not imitate its engine faithfully
cannot witness the bugs that matter.** Most of what both rounds found hid behind exactly that kind of
gap. The shim now stores `str(Path(...))` as the engine does, but nothing machine-checks that it
keeps doing so; the durable guard is the reader example in `test_mutation_summary.py`, which fails
whether or not the shim is faithful.

Final gates at the released head: gate 1 163 tests exit 0 with the two DYD-130 skips, run three
times; gate 4 61 passed / 4 failed, the four being exactly the DYD-130-gated rows; gate 5 build 0
warnings 0 errors; gate 2 not rerun at the last two hops under the admiral's selection ruling, with
the reasoning measured rather than assumed — `mutation_summary.py`'s only importer is
`mutation_adapter.py`, and of the 30 test modules only DYD-103's three import either.

**The last hop is unreviewed.** Two review rounds is this Project's standing limit and both ran, so
`5a6f58d3` and the two captain spec corrections were folded in and released rather than sent to a
third round. `5a6f58d3` is proved by its own real-engine measurement — a full `init`+`exec` campaign
over a nested `src/mod.py` reading exit 0, score 100, with `src/MOD.py`, `src/other.py`, `mod.py`
and `other/src/mod.py` all still refused — but no independent reviewer has judged it. A reviewer
picking this up should start there.

Open, recorded, nobody's defect: report validation items 2-4's new examples run at the recorded
`windows_job.run` boundary like their neighbours, so they prove the guard and the published gap
rather than the native behaviour of a real held handle.
