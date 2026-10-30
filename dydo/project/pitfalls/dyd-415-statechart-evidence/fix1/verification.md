---
area: project
type: reference
---

# DYD-415 correction hop 1

Writer: code-writer/model gpt-6-astra/high. Contract: DYD-415 snapshot
2026-10-29T21:15:11.600Z plus the binding Windows LC FIFO amendment.
This corrects the Sol review (`review-gpt-6.1-sol.txt`) and
Astra review (`review-gpt-6-astra.txt`) of `53df22e002a0e5c5e4429cc6908e0b01d4fb6ab3`.
The contract snapshot (`contract-snapshot.txt`) and fix commission (`commission.txt`)
retain the exact acceptance and resource instructions.
The captain requested DYD-428's optional-JAVA test correction in this same hop.
No threshold or project-wide policy was changed. This is implementation evidence,
not an independent reviewer PASS or an acceptance waiver.

## Source and corrections

Before edits: clean `DYD-415-statechart`, isolated root
`C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f`, HEAD `53df22e`, with
`8553172dfaa7601d34f6b884c10350183f9faaa5` an ancestor. All changes remain in the
contracted skill and evidence paths. No rewrite, push, PR or watch occurred.

- The Java regression now resolves the installation through the actual executable's
  `java.home`. Real TLC cases keep the caller-relative jar/executable and empty output
  assertion, and also run with absolute, bare `java`, and unset overrides.
- CLI cognitive complexity measured 37 at the reviewed commit. Extracting output
  validation, worker launch and publication reduces its module entry to 18, with no
  callable above 20. Before/after measurement (`complexity-red.json`).
- The maintained verdict classifier no longer nests ternaries. New controlled
  ownership/cleanup failure cases supplement the real Windows Job Object tests.
  POSIX tool-discovery cases exercise existing tools, installation and failed installation.
- Java/tool discovery, checker processes and recurring Windows test helpers request
  hidden consoles. The reported 00:14 local burst closely follows the 00:12Ă˘â‚¬â€ś00:13
  Java proof and is likely related, but no window/PID trace proves attribution;
  separate Codex MCP helper churn was also reported. This is not a sole-cause claim.
  The user's standing requirement is that routine suites do not repeatedly flash
  consoles; preserve all assertions and lifecycle protections. Captain and fresh
  reviewers must retain that requirement.

The console regression removes only the two runtime hiding options under an
inherited launch interceptor. It fails before an unhidden Java child is created,
then passes restored: 15/15 applicable cases, three POSIX skips, and all 49 observed
launches hidden. Normal and killed-parent Job ownership and scratch cleanup still
pass. Safe red (`hidden-red.txt`), green (`hidden-green.txt`),
launch audit (`hidden-green-spawns.jsonl`). This verifies launch options and behavior,
not a desktop window observation. The final complete Windows suite uses the same
fail-before-launch interceptor; it does not repair or suppress missing options.
That suite records 521 launch API calls, all explicitly hidden and none refused
(`windows-spawns.jsonl`). The remaining unhiding sites are POSIX-only harness
branches; Windows tool execution and all applicable recurring test cases passed
under the interceptor.

The immutable Linux archive comes from `git archive HEAD` plus the named owned
overlay, with executable modes preserved. Snapshot identity (`snapshot.json`) records
every skill file's SHA-256 and mode. Windows identity (`windows-source.json`) records
the actual files present when the instrumented suite starts. Existing manifests in
the parent evidence directory remain the initial candidate's historical record.
The evidence inherits byte-preserving Git attributes. Its local whitespace rule
recognizes native CRLF endings while retaining the standard blank-line, trailing
space and space-before-tab checks; report bytes are not rewritten to pass Git.

## Platform proof

| Package suite | Discovered | Passed | Skipped | Failed | Exit |
|---|---:|---:|---:|---:|---:|
| Windows, Node 22.23.2 / Java 11 | 357 | 180 | 177 | 0 | 0 |
| Native Linux, Node 22.23.0 / Java 17.0.21 | 390 | 383 | 7 | 0 | 0 |

Windows finished in 222.097 seconds. Its 177 skips are the original 174 POSIX
registrations and the three new POSIX tool-discovery cases. Linux executes all of
them, including the raw-TLC parent and its 33 corpus subtests; its only seven skips
are the native PowerShell acceptance case and six Windows lifecycle/tool cases.
Linux finished in 464.859 seconds. Windows TAP (`windows.txt`), Windows command (`windows.json`),
Linux TAP (`linux-suite.txt`), Linux command (`linux.json`), Linux environment (`linux-environment.txt`).

The acceptance mutation changes the expected shortest path from one step to two
without changing Gherkin wiring or the actual model. Its real-TLC assertion fails
with exit 1; restoring the expected value passes with exit 0.
Mutation red (`acceptance-mutation-red.txt`), restoration (`acceptance-mutation-restored.txt`).
The first driver's broad `java-` dispatch branch ran extra green probes instead of
its intended matrix/revert actions. `java-matrix.txt` and `java-revert.txt` are those
four-case green probes, not caller-environment or revert-red evidence. The corrected
`java-proof` mode supplies those distinct records.

The corrected driver restores the reviewed test and reproduces
`ERR_INVALID_ARG_TYPE` with unset `JAVA`, exit 1, then restores the corrected bytes.
All four real-tool cases pass under each caller configuration: absolute `JAVA`,
bare `JAVA=java`, and `JAVA` unset with Java on PATH (12/12 total, each command exit 0).
Revert red (`java-reverted-red.txt`), absolute (`java-absolute.txt`), bare (`java-bare.txt`),
unset (`java-unset.txt`). This closes the commissioned DYD-428 correction.

The package command remains `npm test`, resolving to
`node --disable-warning=ExperimentalWarning --test --test-concurrency=1 test/*.test.ts`.
The final Windows run executes that exact resolved command with inherited launch
auditing; its driver asserts equality with `package.json`. The initial correction
suite used `npm test` directly, retained in `before-console/`. The final Linux run
uses `npm test` directly on the final immutable snapshot. This rerun was required
by the additional console-visibility correction, not a repeat of unchanged proof.
Linux runs on a native container filesystem extracted from the immutable archive,
with both host projections created by `node setup-skills.mjs`. It uses Node 22.23.0,
Debian's Java 17 and the same SHA-256-pinned TLC 1.7.4 jar. The container uses
`--init`, two CPUs and 4 GiB; the launcher and recording shim retain executable
bits (launcher 0775 and overlaid Java shim 0755, recorded by `stat`).

Every provisioning/suite driver books the live Windows LC FIFO, then nonwaiting
Heavy, then exclusively creates `TEMP/dydo-fullsuite.lock`. The Windows parent
remains alive throughout Docker execution. `*-reservation.json` records tickets
and owners; the driver's `finally` releases only its own dydo lock, and context
exit releases its owned Heavy/FIFO locks. No Linux-side no-op booking substitutes
for that ownership. Original driver (`driver.py`), final driver (`final-driver.py`),
Linux recipe (`linux.sh`), image (`Dockerfile`).
The final platform run held ticket `0000000001-461ffa2ddbf62d1e`;
report processing held `0000000003-2799b92a64cd6341` after 1,680 seconds queued.
Both are released in `cleanup.json`, alongside every earlier owned reservation.

## Maintained assurance

The upstream inventory has 407 files, of which 391 remain byte-identical. The
six runtime targets are `frontends/dot.ts`, `run.ts`, `statechart.ts`, `worker.ts`,
`windows.ts`, and `windows-cleanup.ts`. Static analysis also measures the thirteen test
or harness files with local changes. Tests provide coverage; they are not themselves
coverage targets. Native method rows (`walked.json`), scope/accounting (`static-summary.json`).
Every maintained runtime module has a named test association (`test-associations.json`).

Whole changed modules are coverage targets. For static findings, unchanged method
bodies are compared byte-for-byte to the canonical upstream pin; their inherited
findings stay visible. Authored files, changed methods, and changed module entries
remain in the maintained gate. No local function is excluded merely because its
folder contains vendor code.
For module-entry analysis, the native walker's callable spans are removed from
both sources and the remaining bytes compared exactly. This preserves the
unchanged recording shim's top-level cognitive score of 23 as inherited evidence;
its edited Java-launch function remains measured. No threshold changes.

| Supported measurement | Result |
|---|---|
| Strict TypeScript 5.9.3 | Pass; no maintained unused-local/parameter finding |
| ESLint/SonarJS | 68 maintained callables plus module entries; max cognitive 19, max parameters 5; no maintained nested ternary or suppression |
| jscpd 15-line AND 100-token clones | 0 across all 19 measured files, 4,616 lines / 36,702 tokens |
| dependency-cruiser, including type imports | 57 nodes / 78 nonexternal edges; 0 cycles / unresolved dependencies |
| knip unused files/exports | 0 maintained findings; 44 TypeScript sources and 9 plugin-discovered corpus views processed |
| Pester 3.4.0 | 4/4 tests; launcher 12/12 commands, Job adapter 11/11 commands; native parser 0 errors and 0/1 parameters |

The unfiltered no-unused compiler invocation exits 2 on five diagnostics in
byte-identical `pipeline.ts` and `test/chart.test.ts`; knip exits 1 on two exports
in byte-identical `ir.ts` and `passes/lift.ts`. Five nested-ternary diagnostics are
inside byte-identical vendor methods. The scoped summary preserves each finding
and its identity witness; it does not relabel the unfiltered command as exit 0.
See `*-command.json`, `typescript-strict-unused.txt`, `knip.txt`, and `metrics.txt`.

| Maintained runtime module | Lines | Branches | Maximum maintained HCRAP |
|---|---:|---:|---:|
| `frontends/dot.ts` | 93.51% | 88.89% | 4 |
| `run.ts` | 98.14% | 94.81% | 6 |
| `statechart.ts` | 100.00% | 88.71% | 18 |
| `worker.ts` | 100.00% | 83.33% | 13 |
| `windows-cleanup.ts` | 100.00% | 90.00% | 10 |
| `windows.ts` | 100.00% | 100.00% | 2 |

All six modules pass the unchanged 80%/60% floors, and every maintained callable
passes HCRAP/cognitive 20 and parameter count 7. Unchanged upstream `check` and
`judge` retain cognitive scores 40 and 64; their exact unchanged bodies are explicitly
marked in the per-method report rather than called passing maintained code.
Final gate (`coverage-final-result.json`), per-method rows (`coverage-final-methods.json`),
LCOV (`coverage-final/lcov.info`), Istanbul counters (`coverage-final/coverage-final.json`).

V8 child-process output is filtered only to the six declared runtime modules.
The final captures contain 1,100 Linux and 172 Windows files with target counters. A first attempt to merge raw V8 ranges made worker line coverage
fall from the Windows-only 100% to 90.24%. The `coverage-combined*` files retain that
rejected intermediate measurement and are not the gate verdict. The accepted method
converts each native platform independently with c8, then merges source-location
counters with `istanbul-lib-coverage`. An explicit audit verifies that no covered
source line from either platform is lost. The unchanged repository
`typescript_join.py` joins the final LCOV to the ESLint/SonarJS callables.
Merge audit (`coverage-merge-audit.json`), merge recipe (`merge-coverage.cjs`),
score recipe (`coverage-summary.py`), native counters (`normalized-v8.tar.gz`).

Both setup projections and all 24 setup tests pass on the corrected source; strict
typecheck passes. The doc/whitespace and final source-identity checks are retained
beside these runtime records. `source-manifest.json` describes the successor skill
bytes; `proof-manifest.json` binds the evidence. All owned FIFO tickets and the dydo
lock are released, the `--rm` container is gone, and only this task's image tag is
removed. Cleanup (`cleanup.json`). Task-local prerequisites/counters remain for review.

## Capabilities requiring captain disposition

- PowerShell: Pester provides command coverage, including actual native Job execution.
  It supplies neither branch counters nor cognitive/CC/HCRAP, and PSScriptAnalyzer
  is absent. There is no adopted PowerShell static/dead-code/clone/cycle mechanism.
  The 23/23 command result is not a DR048 line/branch/HCRAP pass.
- Embedded C# in `windows-job.ps1`: `Add-Type` compiles and the real tests exercise
  the native calls, but Pester counts the complete string as one command. The
  repository's project/assembly-based Roslyn/Coverlet adapters do not instrument
  this dynamic assembly. Per-method coverage/HCRAP and analyzer assurance remain
  unavailable without a separately scoped adoption. Extracting this code into a
  compiled project and adopting a harness is a concrete alternative, not performed here.
- Changed-code mutation: no reviewed TypeScript/PowerShell mutation mechanism exists.
  The bounded acceptance expected-value mutation is recorded separately and is not
  represented as a full mutation gate.

The captain must resolve these exact capability gaps before claiming complete
acceptance. No universal-policy edits or unrelated viewer/.NET suites were made.

## Reproduce

The final proof uses an ordinary Windows TEMP copy of `final-driver.py`; it queues
once, audits the Windows resolved package command, snapshots the exact source and
holds the FIFO throughout Docker's native `npm test`. Copy `driver.py` to TEMP
and run `final-assurance` for the scoped static and coverage conversion commands.
The earlier `java-proof` mode retains the separate JAVA caller matrix and revert
recipe. `assurance.py` supplies the scope-specific static commands;
`knip-audit.py` retains native file discovery and `static-summary.py` audits findings.
Run `coverage.py --windows-only` and `coverage.py --linux-only` into fresh output
directories, then `node merge-coverage.cjs` and `python coverage-summary.py`.
Keep the two native coverage conversions separate. A new capture uses fresh task
TEMP/evidence directories; the explicit driver roots are part of this recorded run.
`powershell-coverage.ps1` invokes installed
Pester against the two original adapter files.

## Related

- [Initial Windows proof](../../dyd-415-statechart-verification.md)
- [Testing strategy](../../../../guides/testing-strategy.md)
- [DR048](../../../decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md)
