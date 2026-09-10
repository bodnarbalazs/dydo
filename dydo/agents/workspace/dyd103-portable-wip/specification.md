# DYD-103 specification — isolated mutation assurance for C#, Python and JavaScript

Authored at `1bc93c5d2122305ad5e3a3fc997d7a6609ae77e4` (= `feature/dydo-3-consolidation` tip) on
`DYD-103-mutation-gate` in `C:/Users/User/Desktop/Projects/DynaDocs/.worktrees/dyd103-spec`, 2026-09-09.
Authority in order: DR 048 §4; `dydo/project/plans/dydo-3-completion.md` §3, §4 "Later bearings" 3,
"Exact gates", §5, §6; Issue DYD-103 sections "Current contract — 2026-09-09" and "Admiral integration
ruling — gate adapter exits, 2026-09-06" and the admiral's DECIDED 9c72177b (2026-09-09: campaign
cap and association rows); DYD-96's reviewed adoption specification at
`cc6705b04a3d289892437cb09a37cc71e6b80537` (SPEC PASS aa8d2e13 on DYD-96; DECIDED 543bcf14 there
mirrors the association transfer); the coding, testing and coverage standards.

The production base of DYD-103 is the feature head after DYD-130 (DYD-96's merge). This spec is
authored at `1bc93c5d` and is **re-pinned, not re-specified**, to that head unless a consumed
interface field named below changed or the `execution_seconds_maximum` keyword is absent from
`windows_job.validate`/`run` there. `$P` = `C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`
(Python 3.12.14).

### Scheduling amendment — reviewed interface preparation

The post-DYD-130 production prerequisite remains required for the integrated feature and all
serial work. Disjoint DYD-103 preparation may begin only after this amendment has a fresh SPEC PASS
and an independently reviewed CODE PASS exists for the exact consumed DYD-96 interface subset at a
named SHA. The subset is behavior and files, not DYD-96's whole tree: `inventory.py` and
`gate_inventory.py` producing and fail-closed validating schema 1; `run_tests.py` snapshot functions;
`windows_job.py` preflight/request/run with default 1800 and caller override plus cleanup; and
`gate_adapter.py`/`gap_check.py` freshness and 0/1/2/130 mapping. Association semantics are needed
later only. JS G coverage outputs, unrelated DYD-96 findings and remediation are outside the bounded
handoff.

Preparation may touch only the existing `exclusive` paths: `mutation_adapter.py`,
`mutation_summary.py`; `mutation/**` configs/manifests/probe; `test_mutation_*.py` and fixtures;
`mutation-assurance.feature`; and `MutationAssuranceSteps.cs`. The serial-after-DYD-130
`gap_check.json`, `.config/dotnet-tools.json`, `test-associations.json`, `testing-strategy.md`, and
`coverage-tools.md` remain forbidden until DYD-130. Branch preparation from the exact subset-PASS
SHA to preserve source ancestry, accepting only the named interface blobs and behavior; inherited
unrelated DYD-96 paths are neither reviewed by DYD-103 nor owned. After verifying that the subset-PASS
SHA is an ancestor, merge the exact reviewed post-DYD-130 feature head into the preparation branch,
then re-pin and compare every consumed signature, field and blob; non-ancestry, conflict or departure
is a spec return, never a silent transplant. Reuse retained `70cf3a5e`
engine pins/provenance and existing native qualifications where bytes and environment allow; do not
duplicate qualification solely because of scheduling, while still running missing current-adapter
proofs and gates.

## Spec

### Outcome

The DYD-113 facade's `gate mutation --since BASE [--stack csv]` runs a real Stryker.NET 4.16.0,
StrykerJS 9.6.1 or Cosmic Ray 8.7.0 campaign per stack over an isolated snapshot of the caller's tree
(committed, dirty and untracked content), selected from DYD-96's `inventory.json` schema 1 and the
Git diff against `BASE`, and publishes one schema-1 adapter summary per stack. Policy: **every valid
generated mutant of the selected files must be Killed**; any surviving, uncovered, timed-out,
runtime-error, ignored, unrun or unknown mutant is a measured failure (1). Tool failure, missing or
malformed or stale evidence, a substantive zero-mutant campaign, an unselectable applicable target,
or an unverified cleanup is 2. Interruption is 130 only after the adapter's owned cleanup. Wider
selection is always allowed; narrower than the changed file set never. No fixture is ever presented
as M.

### Seam

Each stack's `mutation` capability in `DynaDocs.Tests/coverage/gap_check.json` becomes:

```json
"mutation": {"state": "configured",
             "command": {"kind": "current-python",
                         "argv": ["DynaDocs.Tests/coverage/mutation_adapter.py", "--stack", "<name>", "--since", "{base}"]},
             "artifacts": [{"path": "DynaDocs.Tests/coverage/results/adapters/<name>-mutation.json", "required": true}]}
```

for `<name>` in `dotnet`, `python`, `node`. `{base}` is the single argv element the facade replaces
with `--since`'s value (facade contract, `gap_check.py:167-173`). The adapter is the exclusive new
module `DynaDocs.Tests/coverage/mutation_adapter.py` with its normalizer
`DynaDocs.Tests/coverage/mutation_summary.py`; it does **not** extend DYD-96's `gate_adapter.py`.
Justification: the shared module is not required (its `main` dispatches only `static|coverage`, and
its `publish` acquires the summary lock at publication time whereas a mutation campaign must hold
the slot for its whole duration); an exclusive module keeps DYD-103's ownership disjoint from a file
DYD-96 may still amend, and the summary schema is a data shape, not code to share. The manifest's
stack-level `isolation` rows are unchanged (`python`/`node` stay `in-place` for tests); the mutation
adapter is its own isolation adapter and always snapshots, which the guide records.

`gap_check.py` public behaviour is unchanged. **Facade edit: none.** Every facade behaviour this
spec relies on is already implemented and proven: `{base}` substitution and `--since` requirement
(`testing-facade.feature` lines 56-82), required-artifact freshness (`gap_check.py:233-246` at
`1bc93c5d`, extended by DYD-96 to every non-test exit at `0d3f0995` lines 242-246),
`--force-run` never selecting mutation (`gap_check.py:266`, feature line 91-98), independent rows
continuing after a peer's 1 or 2 and 130 stopping later rows (`gap_check.py:367-374`,
DYD-96's `assurance-adoption.feature` lines 44-71), and the 30-second interrupt window
(`gap_check.py:248-260`). Those are cited, not respecified.

### Adapter exit protocol

| Adapter exit | Meaning | Summary `exitCode` |
|---|---|---|
| 0 | complete measurement, no policy finding | 0 |
| 1 | complete measurement with at least one finding (surviving, NoCoverage, Timeout, RuntimeError, Ignored-in-selected-file, Pending/NotRun, unknown status, Cosmic Ray survived, timed out or incomplete) | 1 |
| 2 | invalid, unavailable, missing, malformed, stale, tool-missing, lock collision, unsupported host, unresolvable or non-ancestor base, inventory schema/errors, unselectable applicable target, substantive zero-mutant or all-invalid campaign, campaign limit exceeded, unverified snapshot removal | 2 |
| 130 | interrupted, returned only after engine job teardown, raw evidence retention, verified snapshot removal and the publication below; the lock is released last | 130 — the summary **is** published on 130 (`gaps: [{"reason": "interrupted"}]`, `findings: []`, `measurementComplete: false`) |

2 outranks 1; 130 outranks both. Raw vendor exits are retained per command in `commands[].exit`.
The facade maps adapter 0/1/2/130 to passed/failed/invalid/interrupted per the admiral ruling, but
only after its required-artifact check: `gap_check.py` `run_row` at `0d3f0995` (= `fb34c2bc`) marks
every non-test row whose required artifact was not refreshed `invalid`/2 with the raw childExit
(lines 242-246) before the 2/130 mapping (247-249), so an adapter-side 130 that left
`<stack>-mutation.json` unrefreshed would be `invalid`, the aggregate 2, and later rows would keep
dispatching — which the ruling forbids. Chosen correction: the summary is published on 130 after
owned cleanup, exactly as DYD-96's 130 fixture does (`tests/test_assurance_adoption.py:104` at
`0d3f0995` writes `{"schema": 1, "exitCode": 130}` before `SystemExit(130)`). The alternative —
pinning the freshness rule as a consumed interface and proving adapter-side 130 → interrupted without
a refresh — cannot be proven: the facade code makes an unrefreshed artifact `invalid` whatever the
exit, and a facade edit is outside this Issue. An interruption whose snapshot removal is unverified
is not a 130: cleanup failure after interruption is 2 (adoption specification, "Cleanup failure is 2,
including after interruption"), published with both gaps.

### Consumed interfaces — pinned

**Inventory artifact.** `inventory.json` schema 1 as defined in DYD-96's adoption specification
section "Inventory, roles and the mutation handoff" (lines 29-37) at commit
`cc6705b04a3d289892437cb09a37cc71e6b80537` (file SHA256
`2a46e02cc502e2567f1e2a06522356d1fb32319f62aa7e1ac33fada395250406`; that section is byte-identical
to the `fd7bd3d3` pin this spec first carried, so the field table below is unchanged). Produced inside the
snapshot by DYD-96's own producer as `gate_adapter.py` uses it for its `run/inventory.json`
(`gate_adapter._candidate(root)` and `gate_adapter._inventory_artifact(root, run, candidate)` at
`0d3f0995`; the re-pin substitutes the public name if DYD-96 exposes one).

| Field | DYD-103 reads it as | Departure = |
|---|---|---|
| `schema` | must be integer 1, else 2 | spec return |
| `candidate.commit`, `candidate.dirty` | must equal the snapshot's `git rev-parse HEAD` and porcelain state at inventory time, else 2 | spec return |
| `candidate.sourceFingerprint` | recorded at snapshot creation; before acceptance every `files[]` row is rehashed from the snapshot and `inventory.source_fingerprint(rows)` must equal it, else 2 | spec return |
| `files[].path`, `files[].sha256`, `files[].deleted` | staleness rehash; changed-path classification (`deleted` rows are absent paths) | spec return |
| `sources[].path`, `.language`, `.role`, `.testFiles`, `.projects`, `.executable` | selection: language routes to a stack, role `target|test`, `testFiles` joins a changed test to its targets, `projects` decides C# selectability, `executable` decides whether zero mutants is substantive | spec return |
| `excluded[].path` | a changed excluded path widens its language's stack | spec return |
| `errors` | nonempty → 2, copied into `gaps` | spec return |
| `files[]` order, `sources[].sha256`, `excluded[].reason/origin`, `projects[]` (top level), any added property | validated as part of the schema-1 handoff; any mismatch → 2 before selection | spec return |

The consumer validates the complete schema-1 envelope before it computes a changed set or selects a
stack. The root has exactly `schema`, `candidate`, `files`, `sources`, `excluded`, `projects` and
`errors`; `candidate` has exactly `commit`, `dirty` and `sourceFingerprint`. Unknown properties,
missing properties, wrong JSON types, duplicate keys, duplicate paths, and case-folding aliases are
invalid. `files` is a list of exact canonical paths in strictly sorted order: an existing row is
exactly `{path,sha256}`, while a tracked deletion is exactly
`{path,sha256:null,deleted:true}`. No other row shape is accepted. `sources` is a list of the exact
rows `{path,sha256,language,role,projects,executable,testFiles}`, sorted by exact path; language,
role and booleans use the DYD-96 domains, project paths and `testFiles` are sorted exact canonical
paths, and every source path occurs once in `files` with the same `sha256` and is not deleted.

`excluded` is validated even when no changed path would use it. Each row is exactly
`{path,reason,origin}`, with a canonical path occurring once and an origin object whose exact keys
and positive evidence match its reason (`derived-copy` carries `source`, `canonicalSourceSha256`,
`producer`, `producerTest`; `native-evidence-fixture` carries `manifest`, `manifestSha256`).
`projects` is likewise always validated even when no C# target is selected: every row is exactly
`{path,compile,testProject,assembly}`, with canonical sorted `compile` paths, a boolean
`testProject`, and a root-relative `assembly` path or null. Duplicate or case-alias paths across
the applicable inventory collections, unsorted `files`/`sources`/`projects`/nested path lists,
or any added property are rejected with adapter exit/result 2. These checks occur before selection,
so malformed or ambiguous inventory data cannot become an empty or widened campaign.

For every `sources[]` row, the adapter first matches its path to the unique `files[]` row and then
rehashes the matching current snapshot bytes; either mismatch, including a source hash that is
correct in `sources[]` but stale in `files[]`, is exit/result 2. It also validates the complete
`excluded` and `projects` structures and the envelope's `sourceFingerprint` before reading any
selection field. The boundary proofs are named exactly: an unknown root, candidate, file, source,
excluded-origin or project property returns adapter exit 2 and summary `exitCode: 2` before selection;
a duplicate or case-alias path, invalid ordering, or invalid row shape does the same; and a duplicate
inventory entry never yields a `none` or `widened` selection. The stable 0/1/2/130 protocol,
caller-supplied `execution_seconds_maximum`, no `windows_job` ownership transfer, and production
dependency on DYD-96's final reviewed source remain unchanged.

DYD-96's open bounded return (Linear document "DYD-96 bounded implementation and SDK PDB return —
0d3f0995": the external `Microsoft.NET.Test.Sdk.Program.cs` PDB document) touches how an external
generated document is represented. It is a DYD-103 spec return only if it adds or changes a row in
`files[]` or `sources[]` with a path that `git diff`/`git status` can report; a representation in
`excluded[]` reason/origin, `projects[]` or a new property is ignored here and is a re-pin.

**Modules** (imported from the caller root's `DynaDocs.Tests/coverage/`; signature change = spec return):
`inventory.source_fingerprint(rows)`, `inventory.build_file_rows(root, paths, deleted)`,
`inventory.language_of(path)` (DYD-96 deliveries present at `0d3f0995` only — `inventory.py` does not
exist at `1bc93c5d`; DYD-130 must land them before production); `run_tests.create_worktree(path)`,
`run_tests.copy_dirty_files(worktree)`, `run_tests.remove_worktree(worktree)`,
`run_tests.is_registered_worktree(worktree)`, `run_tests.defer_interruption()` (present at `1bc93c5d`
and at `0d3f0995`);
`windows_job.preflight()`, `windows_job.request(argv, cwd, output, execution_seconds, teardown_seconds)`,
`windows_job.run(value, environment, execution_seconds_maximum)` returning `complete`, `cleanup_confirmed`,
`subject_status`, `stdout_path`, `stderr_path`, `elapsed_seconds` (at `0d3f0995`, without the keyword).
**Campaign cap — consumed interface, no DYD-103 edit** (admiral DECIDED 9c72177b on DYD-103,
2026-09-09; DYD-96's reviewed amendment at `cc6705b0` lines 111, 150 and 161, SPEC PASS aa8d2e13):
`windows_job.EXECUTION_SECONDS_MAXIMUM = 1800` is the one module constant and stays DYD-96's tested
invariant for its own campaigns; `validate(value, execution_seconds_maximum=EXECUTION_SECONDS_MAXIMUM)`
uses the parameter in place of the literal 1800 in the deadline tuple; `run(value, environment=None,
execution_seconds_maximum=EXECUTION_SECONDS_MAXIMUM)` passes the keyword to `validate` verbatim and
does nothing else with it; `request()`, the request schema, `config_sha256`, the result schema and
`main()` are unchanged, so a stdin request is always bound by the default and `run_tests.py` keeps
`execution_seconds=1800` — the maximum is an in-process caller argument, never a request field. An
explicit maximum is the finite ceiling of one caller, never a global raise: DYD-103 passes
`execution_seconds_maximum=14400` on every `run` call, and every 14400 s figure in this spec is that
caller-supplied bound. Edge (line 161): an explicit maximum below the requested value, or non-numeric,
rejects before launch with no output directory exactly as an over-limit request does. DYD-96's
`tests/test_windows_job.py` proves the keyword (`test_explicit_execution_seconds_maximum_is_validated_at_the_boundary`);
DYD-103 adds nothing to either file. At `cc6705b0` the production `windows_job.py` is still unchanged
(`validate(value)` hard-codes 1800 at line 96, `run(value, environment=None)` at line 438): the keyword
is reviewed intent that DYD-96's production hop implements and DYD-130 lands, so step 1's re-pin must
verify it exists on both functions at the production base; a missing keyword is a spec return, exactly
like a changed consumed inventory field.

**Manifest rows** read from the snapshot's `gap_check.json`: `stacks[].capabilities.test.command`
(`kind`, `argv`) for `python` and `node` (the vendor test command), nothing else.

**Shared files** DYD-96 creates that DYD-103 edits after DYD-130: `.config/dotnet-tools.json` (add
`dotnet-stryker` 4.16.0), `gap_check.json` (three `mutation` rows), `dydo/guides/testing-strategy.md`
and `dydo/reference/coverage-tools.md` (mutation sections), and `test-associations.json` (admiral
DECIDED 9c72177b; recorded on DYD-96 as a transfer, DECIDED 543bcf14: DYD-96 does not add them) —
exactly two schema-1 rows (`{"module": "exact/source", "tests": ["exact/test"]}`, DYD-96 spec line 37:
no wildcards, no same-stem inference, no empty or duplicate edges, sorted exact paths):
`DynaDocs.Tests/coverage/mutation_adapter.py` → `DynaDocs.Tests/coverage/tests/test_mutation_adapter.py`,
`DynaDocs.Tests/coverage/tests/test_mutation_facade.py`; `DynaDocs.Tests/coverage/mutation_summary.py` →
`DynaDocs.Tests/coverage/tests/test_mutation_summary.py`; no edit before DYD-130 is Done. `windows_job.py`
and `tests/test_windows_job.py` are DYD-96's and are never edited by DYD-103 (the cap paragraph above).
`DynaDocs.Tests/coverage/.gitignore` is not touched: DYD-103 adds an exclusive `mutation/.gitignore`.

### Candidate, base and changed set

1. **Snapshot.** `<tempdir>/dydo-mutation-<uuid8>` allocated under `run_tests.defer_interruption()`
   exactly as `run_tests.run_tests` does (`run_tests.py:152-169`): `create_worktree` (detached at HEAD),
   `copy_dirty_files` (modified, added, untracked, deleted). Every engine, baseline run and inventory
   producer runs with this path as `cwd`; the caller's tree is never written. All engine outputs go
   to the run directory, never into the snapshot.
2. **Inventory** is produced in the snapshot immediately after creation; identity checks per the
   table above.
3. **Base.** `git -c safe.directory=<snapshot> rev-parse --verify --end-of-options <BASE>^{commit}`
   inside the snapshot; failure → 2 (`unresolvable base`). Then `git merge-base --is-ancestor <base> HEAD`;
   nonzero → 2 (`base is not an ancestor of the candidate`). Decision: a non-ancestor base makes
   "changed since base" describe a divergent history, so the recorded base would misdescribe what was
   measured; DR 048 turns an ambiguous request into a refusal, not a wider guess.
4. **Changed set** = rows of `git diff --name-status -z <base> HEAD` (statuses A, M, D, R, C, T; an
   R/C row contributes its old path as D and its new path as A) ∪ rows of `git status --porcelain=v1 -z`
   in the snapshot taken before any engine starts (dirty/untracked; a deleted dirty path is D).
5. **Language of a changed path**: an existing path → `inventory.language_of`; a D path → extension
   `.cs|.py|.js|.cjs|.mjs`, else the first line of `git cat-file blob <base>:<path>` matched against
   the same two shebangs `language_of` accepts, else none.

### Selection width

Per stack the adapter computes `selection = {mode, reason, changedTargets, selected, witness}`:

- `none` — no obligation for this stack: `changedTargets == []` and no widening trigger.
- `changed` — `selected` = the changed targets of this language exactly (file globs / file list / one
  session per file).
- `widened` — `selected` = every `sources[]` row of this language with role `target` (for dotnet:
  those whose `projects == ["DynaDocs.csproj"]`, i.e. no `mutate` restriction). This repository has
  no distinct project level between file and stack for any of its three stacks, so DYD-103's only
  widening step is the whole stack; `reason` names the trigger.

Classification of each changed path, in this order; the first rule that applies decides:

| Changed path | Rule |
|---|---|
| not in `files[]` and not a D row | inventory stale → 2 |
| D row of a source language | widen that language's stack (`deleted or renamed source`) |
| in `excluded[]` and of a source language | widen that language's stack (`excluded source changed`) |
| in `sources[]`, role `target` | add to `changedTargets[language]` |
| in `sources[]`, role `test` | add every target whose `testFiles` contains it; none → widen that language's stack (`test change with no associated target`) |
| of a source language but in neither `sources[]` nor `excluded[]` | unclassified → 2 |
| in the build-configuration table below | widen the named stacks (`configuration changed`) |
| anything else (docs, data, JSON) | no obligation; recorded in `witness` |

Build-configuration table (`CONFIG_WIDENING` in `mutation_adapter.py`, verbatim): dotnet ←
`DynaDocs.sln`, `Directory.Build.props`, `Directory.Build.targets`, `global.json`, `.editorconfig`,
`.config/dotnet-tools.json`, every `sources[].projects` path, `DynaDocs.Tests/coverage/mutation/stryker-net.json`;
python ← `DynaDocs.Tests/coverage/requirements.txt`, `DynaDocs.Tests/coverage/requirements.lock`,
`DynaDocs.Tests/coverage/mutation/requirements.txt`, `DynaDocs.Tests/coverage/mutation/requirements.lock`,
`DynaDocs.Tests/coverage/mutation/cosmic-ray.toml`; node ← any path whose basename is `package.json`
or `package-lock.json`, `DynaDocs.Tests/coverage/mutation/stryker-js.json`; all three ←
`DynaDocs.Tests/coverage/gap_check.json`, `DynaDocs.Tests/coverage/test-associations.json`.

Selectability gaps (2 whatever the mode): a changed C# target whose `projects` is not exactly
`["DynaDocs.csproj"]` (today `DynaDocs.Tests/coverage/metrics/GateMetrics.csproj`, a separate
executable outside `DynaDocs.sln` with no .NET test project — Stryker.NET has no route; recorded as
the C# gap in the testing guide); a selected JavaScript target without a `.js|.cjs|.mjs` extension
(StrykerJS 9.6.1 cannot parse it; gone after DYD-105's merge). A widened dotnet campaign never
includes GateMetrics (it is not in the project under test) and is not a gap unless GateMetrics changed.

Zero-mutant rule: `substantive` = at least one selected target has `executable: true`. A substantive
campaign with zero generated mutants → 2; generated > 0 but valid == 0 → 2; a non-substantive
campaign with zero mutants → 0 with the witness. Mode `none` runs no engine and passes with
`measurementComplete: true`, `changedTargets: []`, counts all zero and a `witness` listing every
changed path with its classification; justification: AC 6 forbids a *substantive* zero-mutant pass,
and a docs-only change has no maintained source to mutate — the summary says so explicitly and
distinguishes it (`selection.mode: "none"`) from any campaign.

### Engines and pins

| Stack | Engine | Pin and location | Restore (never done by the adapter; reported in the gap) |
|---|---|---|---|
| dotnet | Stryker.NET | `dotnet-stryker` 4.16.0 in `.config/dotnet-tools.json` | `dotnet tool restore` |
| node | StrykerJS | `@stryker-mutator/core` 9.6.1 in `DynaDocs.Tests/coverage/mutation/package.json` + `package-lock.json`, installed to `DynaDocs.Tests/coverage/mutation/node_modules` | `npm --prefix DynaDocs.Tests/coverage/mutation ci --ignore-scripts` |
| python | Cosmic Ray | `cosmic-ray==8.7.0` in `DynaDocs.Tests/coverage/mutation/requirements.txt` + complete `requirements.lock`, venv at `dydo/_system/.local/mutation/python` | `$P -m venv dydo/_system/.local/mutation/python` then `dydo/_system/.local/mutation/python/Scripts/python.exe -m pip install --no-deps -r DynaDocs.Tests/coverage/mutation/requirements.lock` |

Pins verified against the retained inputs (`.config/dotnet-tools.json` 4.16.0, `tools/package.json`
9.6.1, `requirements.txt` 8.7.0 at `70cf3a5e`; the Issue's historical probe evidence records the same
three). No global installs. Manifest placement is exclusive (`mutation/`), not DYD-96's shared
`coverage/package.json` / `requirements.*`, so no serial transfer and no G/M dependency coupling.

Tool presence checks, each failing to 2 with the exact restore command above: `dotnet tool list --local`
in the snapshot lists `dotnet-stryker` `4.16.0`; `mutation/node_modules/@stryker-mutator/core/package.json`
has `version == "9.6.1"`; the venv interpreter exists and `importlib.metadata.version("cosmic-ray") == "8.7.0"`.
Recorded in `tools`.

Campaign settings, validated in the three templates before any launch (mismatch → 2 `invalid mutation
configuration`) and completed per run into the run directory:

- `mutation/stryker-net.json` (the retained root `stryker-config.json`, moved): `project` `DynaDocs.csproj`,
  `test-projects` `["DynaDocs.Tests/DynaDocs.Tests.csproj"]`, `concurrency` 1, `thresholds` 100/100/100,
  `reporters` `["json","html"]`, `break-on-initial-test-failure` true; forbidden keys (`since`,
  `with-baseline`, `ignore-mutations`, `ignore-methods`, `mutate`, `dashboard-*`) absent. Generated:
  `+ "mutate": [<glob-literal file paths>]` in `changed` mode only. Argv:
  `<dotnet> stryker --config-file <run>/dotnet/stryker-config.json --concurrency 1 --output <run>/dotnet/native --skip-version-check`.
  DYD-103 does not use Stryker.NET's `--since`: one selection algorithm from the inventory serves all
  three engines, dirty/untracked files are provably included, and the conservative bound is checkable
  in the generated config; whole-project mode omits `mutate`.
- `mutation/stryker-js.json` (retained, adapted): `testRunner` `command`, `coverageAnalysis` `off`,
  `disableBail` true, `concurrency` 1, `thresholds` 100/100/100, `reporters` `["json","html"]`,
  `plugins` `[]`, `inPlace` true (the snapshot is disposable; no sandbox copy of the repository),
  `allowConsoleColors` false, `cleanTempDir` false. Generated: `mutate` (exact file list),
  `commandRunner.command` (the node stack's manifest test argv rendered with `subprocess.list2cmdline`),
  `tempDirName`, `jsonReporter.fileName`, `htmlReporter.fileName` under `<run>/node/`. Argv:
  `<node> <root>/DynaDocs.Tests/coverage/mutation/node_modules/@stryker-mutator/core/bin/stryker.js run <run>/node/stryker.json`.
- `mutation/cosmic-ray.toml` (retained, adapted): `module-path ""`, `excluded-modules []`,
  `test-command ""`, `timeout 0.0`, `[cosmic-ray.distributor] name = "local"`. Generated per selected
  file: `module-path`, `test-command` (the three paragraphs below),
  `timeout = max(60, 5 × baseline seconds)`.
  Argv: `<venv python> -m cosmic_ray.cli init <toml> <run>/python/sessions/<n>.sqlite` then
  `... exec <toml> <that sqlite>`; read-out `<venv python> <root>/DynaDocs.Tests/coverage/mutation_summary.py --read-cosmic-session <sqlite> --output <run>/python/sessions/<n>.json`
  using `cosmic_ray.work_db.use_db(path, mode=WorkDB.Mode.open)`, `work_items` and `results`
  (`job_id`, `mutations[0].module_path/operator_name/occurrence/start_pos/end_pos`,
  `worker_outcome.value`, `test_outcome.value`, `output`). Cosmic Ray's process exit is never read.

**Cosmic Ray suite runner** (Ruled 3 below). `<run>/python/suite_runner.py` is generated once per
campaign from a constant the adapter owns, is listed and hashed with the generated configs, and lives
in the run directory, never in the snapshot, so no campaign ever mutates its own harness. It runs the
python stack's manifest test argv **in its own process** and reports completion: it keeps a reference
to `sys.stdout`, reconfigures that stream to `encoding="utf-8", errors="backslashreplace"`, binds
`sys.stderr` to it, sets `sys.path[0] = ""` (the `python -m` search order), sets `sys.argv` to the
manifest argv without its leading `-m`, executes it with
`runpy.run_module(<module>, run_name="__main__", alter_sys=True)` for a `-m <module>` argv or
`runpy.run_path(<script>, run_name="__main__")` for a script argv, catches `SystemExit`, writes
`\n##DYDO-SUITE-COMPLETE exit=<code>##` to the kept stream as its last act, and exits with `<code>`
(0 when nothing raised or `SystemExit(None)`, 1 for a non-integer code). In-process
is the mechanism, not a detail: a wrapper that spawned the suite would leave the real test process
orphaned on every engine timeout, because `cosmic_ray.testing._kill_process_group` degrades to
`proc.kill()` on Windows and the following `communicate()` then blocks on the orphan's pipe (measured:
21.1 s against a 2 s timeout, versus 3.0 s in-process). One process also means a suite that dies
mid-run — the case the completion witness exists for — writes no marker. A manifest test command the
runner cannot dispatch in-process (kind not `current-python`, or a first argument that is neither
`-m <module>` nor a script path) → 2 (`unsupported python test command`).

**Cosmic Ray `test-command` rendering** (Ruled 4 below).
`shlex.join([sys.executable, <run>/python/suite_runner.py, *manifest argv])`, written into the TOML
as a basic string with JSON escaping. Both layers are load-bearing: `cosmic_ray.testing.run_tests`
splits the command with POSIX `shlex.split`, which eats the backslashes of an unquoted Windows path,
and a raw backslash in a TOML basic string is an escape sequence (`\U` in `C:\Users\…` is a
`TOMLDecodeError: Invalid hex value` before the engine ever starts). `subprocess.list2cmdline` is
correct only for StrykerJS's `commandRunner.command`, which Node runs through a shell.

**Cosmic Ray baseline.** The python baseline runs that exact rendered command; its seconds feed the
timeout above. Nonzero exit → 2 (`baseline test run failed (exit N)`); exit 0 whose last non-empty
stdout line is not the completion marker → 2 (`baseline did not report suite completion`). A campaign
therefore starts only after the completion witness is proven live for it, so a missing marker on a
mutant can only mean that mutant's run was cut short.

Effective concurrency: configured 1 is validated in every generated config and passed on the
Stryker.NET argv; the raw stdout of each engine is retained. The replay gate (below) additionally
asserts the vendor log witnesses `Stryker will use a max of 1 parallel testsessions.` and
`ConcurrencyTokenProvider Creating 1 test runner process(es).` as evidence; the adapter parses no logs.

### Isolation, containment and cleanup

- One run directory per adapter invocation: `DynaDocs.Tests/coverage/results/assurance/run-<uuid>/`
  holding `inventory.json`, `inventory-commands/`, `report.json`, `<stack>/` (generated configs,
  `job-<name>/` windows_job output with `stdout.log`, `stderr.log`, `result.json`, `native/` or
  `reports/` or `sessions/` raw engine output, `baseline/`).
- Exclusive slot: `os.open(<summary>.lock, O_WRONLY|O_CREAT|O_EXCL)` is the adapter's first act after
  argument parsing, **before** the snapshot; collision → 2 (`mutation slot busy: <lock>`) recorded in
  `run/report.json` only — the foreign lock and the foreign summary stay byte-identical, so the facade
  row is `invalid`/2 with childExit 2 through its own freshness rule; released in `finally` only if
  this invocation created it. No stale-lock stealing.
- Every engine launch, baseline run and Cosmic Ray init/exec/read runs under
  `windows_job.run(windows_job.request(argv, cwd=snapshot, output=<run>/<stack>/job-<name>, execution_seconds=14400, teardown_seconds=10), env, execution_seconds_maximum=14400)`
  with `env` = the ambient environment minus `DYDO_*`. `windows_job.preflight()` failure (not Windows,
  not CPython 3.12.14) → 2 `unsupported host`. A result with `complete == False` or
  `cleanup_confirmed == False` → 2 (`campaign limit exceeded` when `elapsed_seconds >= 14400`, the
  caller-supplied `execution_seconds_maximum`, else `engine did not complete`). KILL_ON_JOB_CLOSE contains every descendant (vstest hosts, node test
  children, Cosmic Ray test commands); the adapter kills no foreign process.
- Raw reports are hashed and listed in the summary before the snapshot is removed. Removal =
  `run_tests.remove_worktree` then verification `not path.exists() and not is_registered_worktree(path)`;
  unverified → 2 with the retained path named (fail closed; never a pass over a leftover).
- Interruption (SIGINT/SIGBREAK → `KeyboardInterrupt`): the active job is torn down by closing the
  job (KILL_ON_JOB_CLOSE), raw evidence gathered so far is retained, the snapshot removed and
  verified, then the summary is published (`run/report.json` and `<stack>-mutation.json`, schema 1,
  `exitCode: 130`, `gaps: [{"reason": "interrupted"}]`, `findings: []`, `measurementComplete: false`,
  counts all zero, `score: null`, `rawReports` = the retained evidence; fields not yet settled are
  `null` — `mutation.base`, `mutation.selection`, `mutation.baseline`, and before the snapshot
  inventory exists `candidate.sourceFingerprint` and `inventory`, with `candidate.commit`/`dirty` then
  read from the caller root), the lock released last, exit 130. Unverified removal turns this into 2
  with gaps `interrupted` and `snapshot removal unverified: <path>`. An interrupt before the lock is
  held owns nothing, publishes nothing and exits 130 (the facade's own interrupt path still reports
  `interrupted`; an adapter-only interrupt in that window is `invalid`/2 — never a pass).

### Report normalization

Summary at `DynaDocs.Tests/coverage/results/adapters/<stack>-mutation.json`, written by temp-file +
`replace`, also written as `run/report.json`. Schema 1, DYD-96-compatible:

```
schema: 1
candidate: {commit, dirty, sourceFingerprint}          # from the snapshot inventory
stack: dotnet|python|node
gate: "mutation"
inventory: {path (relative to results/), sha256}
tools: {"stryker-net": "4.16.0"} | {"stryker-js": "9.6.1"} | {"cosmic-ray": "8.7.0"} plus interpreter/node identities
commands: [{name, argv, cwd, exit, elapsedSeconds, stdout, stderr, sha256s}]   # in order, raw vendor exits
collectors: {"mutation": {"status", "raw": [{path, sha256}]}}
findings: [{gate: "mutation", path, span: {startLine, startColumn, endLine, endColumn}, mutator, status, raw: {report, id}}]  # one per non-killed valid mutant of a selected file
gaps: [{reason, path?, raw?}]
measurementComplete: gaps == []
exitCode: 130 if interrupted after owned cleanup else 2 if gaps else 1 if findings else 0
mutation: {base, selection: {mode: none|changed|widened, reason, changedTargets, selected, witness},
           counts: {generated, valid, killed, survived, noCoverage, timeout, compileError, ignored, runtimeError, unrun, unknown},
           score: 100*killed/valid or null,
           rawReports: [{path (relative to the run directory), sha256}],
           baseline: {argv, exit, seconds} | null}
```

Status mapping (`mutation_summary.py`; both Strykers emit the mutation-testing-report schema):

| Native | Normalized | Counts toward | Policy |
|---|---|---|---|
| Stryker `Killed` | killed | valid | pass |
| Stryker `Survived` | survived | valid | finding |
| Stryker.NET `NoCoverage` | noCoverage | valid | finding |
| Stryker `Timeout` | timeout | valid | finding |
| Stryker `RuntimeError` | runtimeError | valid | finding |
| Stryker `CompileError` | compileError | generated only | excluded from valid |
| Stryker `Ignored` in a selected file | ignored | valid | finding |
| Stryker.NET `Ignored` in a file outside `selected` | not counted | — | recorded in `witness` (foreign-file rule below) |
| Stryker `Pending`/`NotRun` | unrun | valid | finding |
| any other Stryker status | unknown | valid | finding |
| Cosmic Ray `survived` | survived | valid | finding |
| Cosmic Ray `killed` whose `output` ends with the completion marker | killed | valid | pass |
| Cosmic Ray `killed` whose `output` is exactly `timeout` | timeout | valid | finding |
| Cosmic Ray `killed` with neither | runtimeError | valid | finding (`test command did not complete`) |
| Cosmic Ray `incompetent` | compileError | generated only | excluded from valid |
| Cosmic Ray worker outcome `skipped`/`no-test` | unrun | valid | finding |
| Cosmic Ray worker outcome `exception`/`abnormal` | — | — | gap 2 (`engine could not run mutant`) |

Uncovered rule: under StrykerJS `coverageAnalysis: off` an unexercised mutant surfaces as `Survived`;
Stryker.NET reports it natively as `NoCoverage`; Cosmic Ray as `survived`. All three are findings,
so "no surviving or uncovered changed-code mutant" is met exactly when every valid generated mutant
is Killed. Cosmic Ray needs its own witness: it records only the stdout of its test command and calls
every nonzero exit `killed`, which is equally what a crashed, cut-short or never-started suite does.
The witness is the marker the generated suite runner writes as its last act — `killed` carrying it is
a real kill; `killed` whose whole output is `timeout` is Cosmic Ray's own timeout string
(`testing.py:74` returns `(KILLED, "timeout")` and never reads the pipe); `killed` with neither means
the suite was cut short, which is a `runtimeError` finding, the same reading Stryker.NET gives a test
host that died. The marker must be the **last non-empty line** of `output` after trailing whitespace
is stripped, so the literal occurring inside the suite's own text — the normalizer's own tests carry
it — is never mistaken for the runner's. A `survived` row needs no marker: it is a finding whatever
produced it, and the baseline above already proves the marker live for the campaign.

Reachability of the Cosmic Ray worker outcomes, so the table is read for what it is (Ruled 7 below):
under the `local` distributor `mutate_and_test` returns only `normal`, `no-test` or `exception`
(`cosmic_ray/mutating.py:68, 86, 91`). `abnormal` has exactly one producer in 8.7.0,
`cosmic_ray/distribution/http.py:68`, which the `local` distributor never reaches, and `skipped` is
written only by the `cr-filter-*` tools, which this adapter never runs. Both stay specified and are
proved by normalization examples over a fixture; only `exception` and `no-test` can be produced by a
DYD-103 campaign, so only they are provable end to end here. `origin.json` records how each such
fixture was obtained — `cosmic-ray-skipped.sqlite` needed a hand-run `cr-filter-pragma` — so a
reader can tell a campaign-reachable status from one only a fixture can carry.

Report validation runs in this order and each failure is 2; the order is part of the contract
(Ruled 5 below). (1) The report file exists and parses; each mutant has `id`, `mutatorName`,
`status`, `location.start/end.line/column`; a Cosmic Ray session has exactly one mutation per work
item, every work item has a result, and every `module_path` equals the session's file. (2) Every
report path is normalized to a canonical repository-relative path (the normalization paragraph
after the foreign-file rule). (3) The
zero-mutant rule of "Selection width" is applied to the campaign as a whole. (4) Only when the
campaign generated at least one mutant: a file in `selected` that is absent from the report → 2
(`partial report`). (3) before (4) because a zero-mutant StrykerJS report carries an empty `files`
map with no entry for the mutated file at all, so the reverse order would report every zero-mutant
node campaign as `partial report` instead of `zero-mutant campaign`; Stryker.NET keeps the file with
an empty `mutants` list, and both must reach the same verdict. (5) The foreign-file rule, one per
engine (`selected` is the changed targets in `changed` mode and every target of the stack in
`widened` mode):

- Stryker.NET: `mutate` is a mutant filter, not a file filter — mutants in files outside the globs
  stay in the report as `Ignored` with `statusReason` `Removed by mutate filter` (pinned 4.16.0
  `Stryker.Core.dll` on disk: `FilePatternMutantFilter`, strings `Removed by ` + `mutate filter`;
  retained `run_mutation.py:491-497` at `70cf3a5e` required exactly that of every unselected row).
  A file outside `selected` is accepted only when every mutant in it is `Ignored`: those mutants are
  not counted and the file is listed in `witness`; any other status there → 2 (`foreign mutant: <path>`).
- StrykerJS and Cosmic Ray: any file outside `selected` → 2 (`foreign file: <path>`).

Report path normalization (Ruled 6 below): no engine reports a repository-relative path on its own.
Stryker.NET's `files` keys are absolute OS paths and its `projectRoot` is the mutated project's
directory; StrykerJS's `files` keys are relative to its own absolute `projectRoot`; Cosmic Ray stores
`module_path` exactly as the adapter generated `module-path`. `mutation_summary` therefore takes the
snapshot root as an explicit argument — never `cwd`, so a captured fixture whose paths point into the
tree that produced it replays anywhere — and maps every report key the same way: join a relative key
onto that report's `projectRoot`, resolve with `os.path.realpath`, require containment in the resolved
snapshot root compared under `os.path.normcase`, and take the remainder with `/` separators. The
remainder must equal exactly one inventory `files[]` path, byte for byte and case for case. A key
outside the snapshot root, a remainder no `files[]` row spells, or a match that only holds after case
folding → 2 (`unmappable report path: <key>`), before any status is counted. Every path the summary
publishes — `selected`, `changedTargets`, `witness`, findings and gaps — is the canonical inventory
spelling; a vendor spelling never reaches the summary.

### Failure semantics (each is a scenario or a unit gate)

| Situation | Exit | Reason text names |
|---|---|---|
| engine not restored | 2 | the exact restore command |
| `windows_job.preflight` fails | 2 | `unsupported host` |
| python/node baseline test run nonzero | 2 | `baseline test run failed (exit N)`; mutation on a red baseline measures nothing (invalid), not a policy failure |
| python baseline exits 0 without the completion marker | 2 | `baseline did not report suite completion` |
| a python manifest test command the suite runner cannot dispatch in-process | 2 | `unsupported python test command` |
| Stryker.NET exits nonzero with no report (initial test run or build failure) | 2 | `no mutation report produced (Stryker.NET exit N)` |
| report missing / malformed / partial / foreign file / foreign mutant (Stryker.NET) | 2 | the path |
| a report path that maps to no inventory `files[]` row | 2 | `unmappable report path: <key>` |
| inventory `schema` ≠ 1, `errors` nonempty, identity mismatch at production or acceptance | 2 | the field |
| a `files[]` row rehash differs after the campaign | 2 | `candidate changed during the campaign: <path>` |
| substantive zero generated mutants; generated > 0 and valid == 0 | 2 | `zero-mutant campaign` / `all mutants invalid` |
| campaign limit exceeded / engine did not complete | 2 | `campaign limit exceeded (14400 s)` (14400 is the caller-supplied `execution_seconds_maximum`, not a `windows_job` constant) |
| lock collision | 2 | the lock path, in `run/report.json` only; the foreign summary is not refreshed |
| unresolvable / non-ancestor base | 2 | the base |
| unselectable applicable target | 2 | the path and project |
| snapshot removal unverified | 2 | the retained path |
| survivor, NoCoverage, Timeout, RuntimeError, Ignored-in-selected, unrun, unknown | 1 | one finding per mutant |
| interruption | 130 | after owned cleanup, with the summary published (`gaps: [{"reason": "interrupted"}]`); unverified removal → 2 |
| an applicable stack unavailable while it has changed sources | 2 | its row cannot be part of a whole-M pass (facade aggregation) |

### Scenarios

`DynaDocs.Tests/Features/mutation-assurance.feature` (`@DYD-103`, committed in this hop). Bound by
`DynaDocs.Tests/Steps/MutationAssuranceSteps.cs` (pattern `Steps/TestingFacadeSteps.cs:185-230`,
`Steps/AssuranceAdoptionSteps.cs:48-79`): each scenario title (and outline argument) maps to one
named `unittest` probe, run with the `PYTHON` interpreter, in
`DynaDocs.Tests/coverage/tests/test_mutation_facade.py` (facade-boundary probes: a temporary Git
repository mirroring the coverage layout, the real `gap_check.py`, the real adapter and normalizer,
the real DYD-96 inventory producer, and engine shims at the process boundary — a `dotnet` shim
prepended to `PATH`, a shim `stryker.js` under the temp repository's `mutation/node_modules`, and a
throwaway venv whose `cosmic_ray/cli.py` is a shim — each emitting a real captured report fixture
from `tests/fixtures/mutation/`) or in `test_mutation_summary.py` (normalization examples from the
same fixtures). No unit test launches a real engine.

Fixtures under `DynaDocs.Tests/coverage/tests/fixtures/mutation/` are real engine outputs captured
from the replay subjects (`origin.json` names the subject, engine version and command per file);
statuses no subject reproduces (RuntimeError, Pending/NotRun) are real reports with that one field
edited and marked `derived` in `origin.json`. The fixture tree contains only `.json`, `.sqlite` and
`.txt` files (no source-language files), so DYD-96's inventory never classifies fixtures as maintained
sources; the temporary repositories used by tests are written from data strings.

### Gates — verbatim

| # | Command | Pass condition | Policy | When |
|---|---|---|---|---|
| 1 | `$P -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_mutation*.py"` | exit 0, every probe passes | testing-strategy: tests are contracts; no engine launched | production-only |
| 2 | `$P -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_*.py"` | exit 0 | DYD-96 unit gate plus DYD-103's tests | pre-code (existing suite) and production |
| 3 | `$P DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal` | exit 0 | isolated .NET suite green | production (pre-code it is red by the new feature's missing steps, which is the intended red-before-green) |
| 4 | `$P DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal --filter "FullyQualifiedName~MutationAssurance"` | exit 0; every scenario of the feature passes | Reqnroll binding of every scenario | production-only |
| 5 | `dotnet build DynaDocs.sln -c Release --warnaserror` | exit 0 | build invariants | pre-code and production |
| 6 | `dydo check` (`dotnet bin/Release/net10.0/dydo.dll check` when the installed dydo lags source) | 0 errors, 0 warnings | documentation graph | pre-code and production |
| 7 | `$P DynaDocs.Tests/coverage/gap_check.py capabilities` | exit 0; `mutation: configured` for dotnet, python and node | facade inspection | pre-code (rows unavailable, exit 0) and production (configured) |
| 8 | `$P DynaDocs.Tests/coverage/gap_check.py gate mutation --since <production base SHA>` | three rows reported; aggregate 0, or 1 where no finding's `path` is in any stack's `mutation.selection.changedTargets`; never 2; result bound to the exact candidate SHA and raw reports retained. A finding in a changed target is a DYD-103 defect that blocks CODE review | DR 048 §4, AC 6/7 | production-only (final M-measure) |
| 9 | `$P DynaDocs.Tests/coverage/gap_check.py --force-run` | identical selected rows and aggregate before and after DYD-103's edits on the same base; mutation never selected | facade compatibility unchanged | production-only |
| 10 | `$P -m unittest discover -s DynaDocs.Tests/coverage/mutation/probes -p "test_replay.py"` | exit 0: per engine the exact-assertion subject yields killed 1 / score 100 / adapter exit 0 and the weak-assertion subject yields survived 1 / exit 1; the concurrency witnesses appear in the raw stdout | the retained native strong/weak probes replayed through the completed adapter's engine seam, outside the unit gate | production-only |

Restore commands (setup, not gates) are the three in the engine table; they run once in the
production worktree before gates 8 and 10.

## Plan

**Approach** — one exclusive adapter (`mutation_adapter.py`) and normalizer (`mutation_summary.py`)
behind the existing facade row shape, consuming DYD-96's inventory and containment as reviewed
patterns; rejected: extending `gate_adapter.py` (lock timing differs, shared ownership), Stryker.NET
`--since` (second selection algorithm), the retained receipt/semantic-selector architecture (retired
by the plan), and an uncontained engine launch (orphaned vstest hosts would lock the snapshot).

```text
gap_check.py gate mutation --since BASE
  └─ mutation_adapter.py --stack S --since BASE            (current-python, one row per stack)
       lock <summary>.lock ─ preflight ─ snapshot (run_tests.*) ─ inventory (DYD-96 producer)
       base ─ changed set ─ selection ─ tool checks ─ [baseline] ─ engine under windows_job
       ─ mutation_summary.normalize ─ identity recheck ─ raw hashes ─ remove snapshot (verified)
       ─ publish summary ─ unlock ─ exit 0|1|2 (130 on interrupt after cleanup)
```

**Pattern to copy** —
- `DynaDocs.Tests/coverage/run_tests.py:149-188` (snapshot lifecycle, `finally` cleanup) — reused as
  functions; departure: removal is verified and unverified removal is 2.
- DYD-96 `gate_adapter.py:39-57` (exclusive lock, temp-file publish), `:60-86` (candidate identity,
  inventory artifact), `:233-267` (run directory, summary path, `--root/--output`) — mirrored;
  departure: the lock is held for the whole campaign.
- DYD-96 `windows_job.py:70-76, 438-463` (request/run) — reused through the reviewed
  `execution_seconds_maximum=14400` keyword on `run` (`cc6705b0` line 111); departure: none, DYD-96's
  files are not edited.
- DYD-96 `gate_run.py:15-46` (`CommandLog` rows) — the `commands[]` row shape is mirrored, not imported.
- retained `run_mutation.py:30-56` (template validation), `:248-267` (Cosmic Ray session read),
  `:374-375` (`glob_literal`), `:575-601` (tool presence checks) — adapted; `mutation_results.py:157-197`
  (Stryker row extraction) — adapted without the UTF-16 span machinery; everything else in the retained
  branch is discarded per `retained-disposition.json`.
- `DynaDocs.Tests/Steps/TestingFacadeSteps.cs:185-230, 292-315` and `coverage/tests/test_testing_facade.py`
  (probe-per-scenario, temp-repository facade drives, CTRL_BREAK delivery) — copied for
  `MutationAssuranceSteps.cs` and `test_mutation_facade.py`.

**Files** — see `owned-paths.json` beside this file (one edit each). Exclusive new:
`mutation_adapter.py`, `mutation_summary.py`, `mutation/{stryker-net.json, stryker-js.json, cosmic-ray.toml, package.json, package-lock.json, requirements.txt, requirements.lock, .gitignore}`,
`mutation/probes/test_replay.py`, `tests/test_mutation_adapter.py`, `tests/test_mutation_summary.py`,
`tests/test_mutation_facade.py`, `tests/fixtures/mutation/**`, `Features/mutation-assurance.feature`,
`Steps/MutationAssuranceSteps.cs`. Serial after DYD-130: `gap_check.json`, `.config/dotnet-tools.json`,
`test-associations.json` (the two rows named under Shared files), `dydo/guides/testing-strategy.md`,
`dydo/reference/coverage-tools.md`. Not owned: `gap_check.py`, `dydo/reference/gap-check.example.*`
(DYD-113/DYD-91), `inventory.py`, `gate_*.py`, `run_tests.py`, `windows_job.py`, `tests/test_windows_job.py`
(DYD-96; the `execution_seconds_maximum` keyword is consumed, never edited),
`coverage/package*.json`, `coverage/requirements.*`, `coverage/.gitignore` (DYD-96), `npm/**` (DYD-105),
`DynaDocs.Tests/DynaDocs.Tests.csproj`, `reqnroll.json` (DYD-99/DYD-96), root `.gitignore`.

**Steps** —
1. Before any exclusive preparation, record the fresh SPEC PASS and independently reviewed CODE
   PASS for the exact consumed DYD-96 subset at its named SHA; branch from that SHA and check that
   only the exclusive paths are touched. At final reconciliation, re-pin at the exact reviewed
   post-DYD-130 feature head: diff every consumed interface above, including that `windows_job.validate`
   and `windows_job.run` carry the `execution_seconds_maximum` keyword there; record "re-pinned to
   <SHA>" on the Issue, or stop with a spec return naming any changed signature, field, blob or
   missing keyword. Checkable: both PASS records, ancestry, the recorded final SHA, unchanged field
   list and keyword presence.
2. Restore the three engines with the restore commands; author only the three templates and the
   exclusive manifests/locks/`.gitignore`. Checkable: tool presence checks pass in a scratch run.
3. Capture real fixtures: run the retained-style strong/weak/no-coverage/timeout/all-invalid subjects
   (data strings in `test_replay.py`) once through each engine by hand, copy the raw reports and one
   Cosmic Ray session into `tests/fixtures/mutation/` with `origin.json`; derive the two
   non-reproducible statuses and mark them. Every Cosmic Ray fixture is captured through the
   generated suite runner and the `shlex.join` rendering exactly as the adapter emits them, and the
   set covers all three kill readings: `killed` with the marker, `killed` whose output is exactly
   `timeout`, and `killed` with neither. Checkable: `origin.json` names every fixture, and its
   Cosmic Ray `test-command` strings are the ones the adapter would render.
4. Red: commit the failing `test_mutation_summary.py`, `test_mutation_adapter.py`,
   `test_mutation_facade.py` and the Reqnroll step file mapping every scenario to a probe.
   Checkable: gate 1 fails on assertions, not on collection errors; gate 4 fails on assertions.
5. Green `mutation_summary.py`: Stryker and Cosmic Ray readers, status table, counts, findings,
   gaps, summary payload, `--read-cosmic-session`. Checkable: `test_mutation_summary.py` green.
6. Green `mutation_adapter.py`: lock, preflight, snapshot, inventory, base, changed set, selection
   (`CONFIG_WIDENING` verbatim), tool checks, baseline, generated configs, launches under
   `windows_job`, identity recheck, raw hashes, verified removal, publication, interruption.
   Checkable: gates 1, 2 green; every `windows_job.run` call passes `execution_seconds_maximum=14400`
   (the campaign-limit case of `test_mutation_adapter.py` asserts it at the launch seam).
7. Serial edits, only after DYD-130 is Done and the final reconciliation in step 1 passes: add the
   `dotnet-stryker` entry, configure the three `gap_check.json` mutation rows, and
   add the two `test-associations.json` rows named under Shared files (in module-path order — at
   `cc6705b0` between `DynaDocs.Tests/coverage/metrics/StructuralMethod.cs` and
   `DynaDocs.Tests/coverage/node_tests.cjs` — tests sorted, no other row touched) before gate 9's
   integrated `--force-run`; gates 5, 6, 7, 9 green; gate 3/4 green. Checkable: `associations.py`
   reports no `non-trivial target has no associated test file` for either module and no
   `unknown or duplicate associated module`.
8. Replay gate 10 with real engines; retain its evidence under the run directory.
9. Final M-measure: gate 8 at the exact candidate SHA with `--since <production base>`; retain the
   three summaries and raw reports as Issue evidence. DYD-103's own changes touch `gap_check.json`,
   `test-associations.json`, `.config/dotnet-tools.json` and an unassociated C# step file, so this run
   is `widened` for all three stacks by the rules above while each summary still records its
   `changedTargets`: it is the
   repository's first complete M measurement. Pass = aggregate 0, or 1 where no finding's `path` is in
   any stack's `changedTargets`. A finding in a changed target (`mutation_adapter.py`,
   `mutation_summary.py` or any other target DYD-103 changed) is a DYD-103 defect that blocks CODE
   review; a 1 whose findings all lie outside every stack's `changedTargets` is reported like
   DYD-96's G-measure — evidence for remediation Issues under AC 7 — and is not a DYD-103 defect; a 2
   is a DYD-103 defect.
10. Docs hop (not empty): the mutation sections of `testing-strategy.md` (what runs, selection
    rules, exits, artifacts, restore commands, the GateMetrics C# gap, Windows-only containment,
    extensionless JS until DYD-105) and `coverage-tools.md` (adapter commands, summary schema, raw
    locations, pins); gate 6 green. Then hardener (attack the fail-closed seams: stale identity,
    partial reports, lock, interruption inside each engine phase), CODE review, DYD-131.

**Edge cases** —
- `--since` value with leading dash or refspec syntax: passed after `--end-of-options`; unresolvable → 2.
- Base equal to HEAD with a clean tree: changed set empty → every stack `none` → 0 with witness.
- Rename with modification (`R0xx`): old path D (widen if source), new path A (changed target).
- A changed test file listed by targets of two languages (impossible by construction; `testFiles`
  are per target) — each target's own language stack is affected.
- Dirty deletion of a target: `files[]` row `deleted: true`; D → widen.
- Untracked new target: in `files[]` and `sources[]`; A → changed target; the snapshot contains it.
- Engine writes into the snapshot (`.stryker-tmp`, `StrykerOutput`): outputs are redirected to the
  run directory; any residue is irrelevant because only `files[]` rows are rehashed, never porcelain.
- StrykerJS `inPlace` leaves a mutated file after a crash: rehash mismatch → 2.
- Cosmic Ray session where a work item has no result (interrupted engine): partial → 2.
- Two facade invocations at once for the same stack: second sees the lock → 2, first unaffected.
- Adapter interrupted before the snapshot exists: publish the 130 summary (unsettled fields `null`),
  release the lock, exit 130; nothing to remove.
- Adapter interrupted during snapshot removal: `defer_interruption` semantics as `run_tests.py:122-146`;
  removal completes, then 130.
- Windows-only: any other host → 2 at preflight, before locking anything but the slot.
- `PYTHONPATH`/`NODE_TEST_CONTEXT` inherited by engines: the ambient environment minus `DYDO_*` is
  passed unchanged; the replay gate proves nested `node --test` children still observe
  `__STRYKER_ACTIVE_MUTANT__`.
- Facade `--stack` subset: rows not selected run nothing; the summary for a selected stack never
  claims other stacks.

**Plan review** — `recommended` (a consumed cross-Issue interface changed in the fold-in hop): (1) the
consumed inventory producer entry is a private name at
`0d3f0995` and the facade-boundary tests need its minimal repository inputs (a restorable csproj and
`test-associations.json`); (2) the consumed `execution_seconds_maximum` keyword of DYD-96's reviewed
`windows_job.py` interface, unimplemented at `cc6705b0` and verified only at the re-pin; (3) explicit
`mutate` globs instead of Stryker.NET `--since`; (4) StrykerJS `inPlace` with the command runner and
environment propagation to nested Node test processes; (5) the Cosmic Ray kill classification through
the generated in-process suite runner and its marker, and the baseline that proves the marker live;
(6) GateMetrics C# is unselectable (2), which binds any Project-level M
whose base predates DYD-96; (7) campaign cost of a widened dotnet run against the caller-supplied
14400 s bound;
(8) the M-measure reading of DYD-103's own final gate; (9) the docs-only `none` pass reading of AC 6.

**Ruled — 2026-09-09** (spec review 1 at `459ec3eb`, findings 1 and 2; admiral DECIDED 9c72177b on
DYD-103, 03:51Z; DYD-96 mirror DECIDED 543bcf14 and SPEC PASS aa8d2e13 on its amendment chain
`fd7bd3d3` → `fb34c2bc` → `08a6cd82` → `cc6705b0`).
1. Campaign cap: no transfer of `windows_job.py` / `tests/test_windows_job.py`; the 1800 s cap stays
   DYD-96's tested invariant for its own campaigns, and DYD-96's reviewed amendment adds the
   caller-supplied `execution_seconds_maximum` keyword to `windows_job.validate`/`run` (default 1800
   unchanged), which DYD-103 consumes with 14400; the ruling's fallback (routing around the cap) does
   not apply because DYD-96's spec review accepted the override.
2. Association rows: the envelope extends to `test-associations.json` as a serial edit after DYD-130
   with exactly the three edges named under Shared files; recorded on DYD-96 as a transfer; no edit
   before DYD-130 is Done.
This packet carries both rulings (Authority, Modules, Shared files, Isolation, Failure semantics,
Pattern to copy, Files, steps 1, 6, 7 and 9, Plan review, `owned-paths.json`).

**Ruled — 2026-09-10** (production's step-3 return on `DYD-103-mutation-prepare` at `007c38a7`; every
fact below re-verified at primary source in the installed engine under
`dydo/_system/.local/mutation/python/Lib/site-packages/cosmic_ray/` and proved against real Cosmic
Ray 8.7.0 campaigns on throwaway subjects outside the repository. Raw evidence:
`dydo/agents/workspace/dyd103-portable-wip/evidence/spec-amend-cosmic-ray-evidence.txt`, produced by
`spec-amend-cosmic-ray-drive.py` beside it; both git-ignored.)

3. **Cosmic Ray kill completion.** *Was specified:* `killed` with a `Ran N tests in` line in `output`
   is a kill and `killed` without it a timeout, the line being "unittest's own summary written by the
   manifest test command". *Refuted:* `cosmic_ray/testing.py` `run_tests` opens the command with
   `stdout=PIPE, stderr=PIPE` (65-66), keeps only stdout at `stdout, _ = proc.communicate(...)` (70)
   and returns it as the recorded `output` (77, 79), while `unittest` writes its summary to stderr.
   With the manifest argv the recorded output is therefore always empty, every kill normalizes to
   `timeout`, and the python stack can never reach exit 0. Measured: a campaign whose `test-command`
   is the manifest argv gave 12 × `NORMAL/KILLED` with **0 characters** of output (and one survivor,
   also empty); DYD-96's retained `pycase/killed.sqlite` says the same, both of its rows
   `NORMAL/KILLED` with `output ''`. *Corrected:* the target does not move — a kill
   whose suite did not complete is not a kill — but the witness is now the adapter's own, not a
   framework's prose. The generated in-process suite runner writes
   `##DYDO-SUITE-COMPLETE exit=<code>##` as its last line; the python baseline must show it before
   any campaign starts; a `killed` row is a kill only when that marker is the last non-empty line of
   `output`; `output == "timeout"` is Cosmic Ray's own timeout string; anything else is a
   `runtimeError` finding. *Proved:* killed rows carry the marker and the merged `Ran 1 test in …`
   line (12 of 13 mutants, the thirteenth surviving with `exit=0`); the same subject run from a
   runner outside the campaign directory — the production layout — behaves identically; an engine
   timeout yields exactly `timeout` and never a marker; and a suite that dies mid-run (`os._exit(1)`
   inside a test) yields `KILLED` with no output at all and is refused, which is the case the witness
   exists for. In-process beat a wrapper process on evidence: Cosmic Ray's `_kill_process_group`
   degrades to `proc.kill()` on Windows, so a wrapper leaves the real suite orphaned and the
   following `communicate()` blocks on its pipe — 21.1 s for a 2 s timeout against 3.0 s in-process,
   same subject — and a wrapper survives a suite that dies, so it would report a marker for a run
   that never completed. The UTF-8 reconfiguration is part of the mechanism, not tidiness: with the
   host code page (cp1250) a failing assertion whose message holds one non-ASCII character made
   Cosmic Ray's `stdout.decode("utf-8")` raise, and the row landed as `NORMAL/INCOMPETENT`
   (12 of 13) — `incompetent` is excluded from `valid`, so a real kill would silently leave the
   denominator.
4. **Cosmic Ray command rendering.** *Was specified:* `test-command` rendered with
   `subprocess.list2cmdline`. *Refuted:* `testing.py:63` splits the command with POSIX `shlex.split`,
   which eats the backslashes of an unquoted Windows path, and `list2cmdline` does not quote a path
   that has no space: `C:\…\python.exe` arrives as `C:…python.exe`. The retained probe only worked
   because it used forward slashes, which still breaks on the first path containing a space.
   *Corrected:* `shlex.join`, which `shlex.split` reverses exactly, plus JSON escaping of the value
   written into the TOML basic string. *Proved:* the `list2cmdline` rendering gave 13 rows of
   `NORMAL/INCOMPETENT` with `FileNotFoundError: [WinError 2]`; `shlex.join` on the same subject ran
   and killed; and an unescaped value never reaches the engine at all — `tomllib` rejects `\U` in
   `C:\Users` with `Invalid hex value`.
5. **Zero mutants before partial report.** *Was specified:* both rules, with no order between them.
   *Refuted:* a StrykerJS zero-mutant report carries an empty `files` map with no entry for the
   mutated file (captured `stryker-js-zero-mutants.json`), so the partial-report rule fires first and
   the feature's `node | zero generated mutants | zero-mutant campaign` row would report
   `partial report`; Stryker.NET instead keeps the file with an empty `mutants` list
   (`stryker-net-zero-mutants.json`), and both must reach the same verdict. *Corrected:* the numbered
   order in Report validation, zero-mutant at (3) and per-file presence at (4).
6. **Report paths are vendor paths.** *Was specified:* nothing; report keys were compared to
   `selected` directly. *Refuted:* Stryker.NET writes absolute `files` keys and a `projectRoot` that
   is the mutated project's directory, StrykerJS writes keys relative to its own absolute
   `projectRoot`, and the captured fixtures accordingly point into the DYD-96 probe tree and the
   capture directory. *Corrected:* the normalization paragraph after the foreign-file rule — the
   snapshot root passed in explicitly, `realpath` + `normcase` containment, `/` separators, an exact
   match against one inventory `files[]` row, `unmappable report path: <key>` otherwise, and only
   canonical spellings in the summary.
7. **Unreachable worker outcomes.** *Was specified:* `exception`/`abnormal` as one row, as though
   both were testable here. *Refuted:* `abnormal` has one producer in 8.7.0,
   `cosmic_ray/distribution/http.py:68`, and `skipped` is written only by the `cr-filter-*` tools;
   under the `local` distributor `mutating.mutate_and_test` returns `normal`, `no-test` or
   `exception` only. *Corrected:* both stay mapped and are proved by normalization examples over a
   fixture, and the spec now says plainly which outcomes a DYD-103 campaign can produce.

Feature consequence: one row's wording follows entry 3 —
`python | killed after the engine timeout | timeout` replaces
`python | killed without a suite completion line | timeout` in "A mutant that is not killed is a
measured finding", because "without a completion line" no longer picks out one outcome. Nothing else
in `mutation-assurance.feature` changes and what it demands of the product is untouched. This
2026-09-10 packet carries entries 3-7 (Engines and pins, Report normalization, Report validation,
Failure semantics, step 3, Plan review, `owned-paths.json`).

Lanes: `none` (adapter, normalizer, fixtures, probes and steps interlock). Empty hops: none.
Production prerequisite for final integration (resume condition, never a parent-Done blocker): DYD-96's implemented and
independently checked inventory artifact at the integrated feature head after DYD-130, the
`execution_seconds_maximum` keyword present on `windows_job.validate` and `windows_job.run` at that
head (DYD-96's production hop implements it; absent at `cc6705b0`), plus an integrated passing
baseline (`run_tests.py` green at that head); a whole-M pass additionally needs DYD-105 merged
(extensionless JavaScript target).
