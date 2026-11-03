# Direct B/C successor contracts: source checkpoint only

code-writer/model: gpt-6.1-sol/high. Same author, HEAD
`6e3fe7e8018130af6b8586ef0cf40c1474740864` and retained draft. Authority:
[captain finite release and direct-test source scope](https://linear.app/bodnar-balazs/document/dyd-415-inspected-b-c-finite-execution-and-direct-test-source-scope-7c4922586390).

The new native unittest source is
[test_statechart_rmi_bc.py](../../../../../../../DynaDocs.Tests/coverage/tests/test_statechart_rmi_bc.py).
Its `load` function imports the actual unchanged b3-bc-source driver.py,
launcher.py and worker.py. Tests call their actual admission, parsing,
selection, extraction-verification, process-completion and cleanup functions;
test fixtures/mocks replace external command and environment seams only. No
bridge, stub target module, alternative runner framework or metric tool is added.

Source parsing declares **22 tests**. No test module was imported, discovered or
executed while preparing this packet. No pass, coverage or association-discovery
claim follows. Expected future native count is 22 once each, zero skips,
duplicates or unselected tests, exit 0, empty stdout and all exact names in raw
stderr. [Inventory](test-inventory.json), [four-file input manifest](input-manifest.json),
[commands](commands.json), [proposed association delta](association-proposal.json)
and [source pin](source-pin.json) bind the source checkpoint.

## Meaningful direct assertions

- Launcher: exact group/state/pin acceptance; wrong state/group/hash and missing
  files reject; wrong platform and redirected namespaces reject.
- All three entrypoints: missing or A/D groups reject before the material-work
  boundary. Driver: redirected namespace rejects before even mocked Git; wrong
  HEAD, changed source, changed preservation and changed retained input reject.
  Controlled fixture bytes remain unchanged by rejection.
- Worker: original B16+12 files and anchored six-name C selector; exact TAP
  passes and filtered unselected cases; reject missing, selected skip, duplicate,
  failed and unselected results. Wrong native totals/skips, command exit or stderr
  stop B before the second command. C requires both bootstrap and separate SANY
  endpoint records and rejects raw TLC or missing endpoints.
- Worker child environment: injection/update/replay variables are removed in a
  child copy, with stable Node/JDK-shim/checker paths and no parent mutation.
- Extraction: wrong archive hash, member metadata drift, traversal, absolute or
  unsupported member rejects before extractall. The admitted path uses the data
  filter for all three archives. These contracts mock extraction; no archive is
  extracted and no toolchain is installed by the tests.
- Source verification: changed bytes, execute mode, extra or missing file rejects
  before importing the JDK verifier. Container checks reject foreign owner,
  host networking or writable test-stage toolchain before start.
- Cleanup: foreign owner never removed; unknown ID remains uncertain; only the
  exact owned ID is stopped/removed, with container and volume absence required.
  Successful worker main cannot emit completion while another namespace process
  remains. Actual process probes, Docker, Node and Java are replaced by local
  mocks in these unit contracts; B/C raw runtime evidence remains separate.

All fixture files are inert owned temporary bytes. Simulated admission
environments are scoped unit mocks inside the future contained Python child;
they are not real host admission or environment spoofing. No real queue booking,
network request, extraction, compiler, Node, Java, container or installer occurs
inside these tests.

## Existing mechanism and execution boundary

The exact future child command is the existing native unittest discovery grammar
selecting only this one file. Input closure is four plain files: the test and
three unchanged maintained targets; their imports are standard-library modules.
Worker import requires Linux's existing resource module. The same pinned cached
Linux Python image can supply it without setup or a dependency download.

The prior nine-contract envelope supplies the already inspected containment
pattern: actual-host default-live FIFO -> nonwaiting Heavy -> owned TEMP lock,
hidden owned Docker children, one network-none/pull-never container, no toolchain
volume or extraction, read-only input/root, disjoint owned work/output,
cap-drop ALL/no-new-privileges,128PIDs/1GiB/2CPUs,180-second child deadline,
16MiB/file hard output bound, five-second owned-session drain and independent
exact-ID/label removal/absence. Its source identities are retained in commands.

That earlier envelope hardcodes a different file, class and count (nine).
It **cannot run these 22 contracts unchanged**. This checkpoint provides exact
test/input/argv/expectation and resource requirements, without pretending that
the old launcher admits new input. A separately inspected bounded envelope
adaptation is required before any future execution; no new executable envelope
or automatic test launch is authored here. No execution-release file exists.

The current test-associations.json stays byte-identical to its admitted hash.
Only a proposed three-row delta is supplied. Each row names the actual module
loaded/called by this native test file, with its witness methods. Captain must
inspect the test sources, disposition the manifest update and pin its proof
applicability before execution/assurance closure. No fabricated association,
static exclusion or policy/collector edit is supplied.

## Unchanged custody and holds

The released B/C source pin remains
`8c4eef594e25a177aa0b8d11c13835ccc6a6d588e9c94b8864605887f8845473`.
Every existing test, model, expected report/chart, recording shim, build/dependency
lock, helper artifact, payload and historical receipt remains unchanged. This
new test source is outside the frozen 415-row B/C snapshot and does not change
the running proof inputs. Proposed associations are not applied.

Source/behavior contracts do not supply Java metrics or Windows/prompt proof.
Desktop Java/javac/install/probe HOLD and all remaining assurance/final gates
remain. No commit, PS retirement, readiness, PR/push/watch/merge/version/release/
publication, global/security change, new crew or settled-HITL contact. Captain/
records only; direct-test execution and assurance remain owed.
