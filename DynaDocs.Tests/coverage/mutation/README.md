# Mutation assurance

Run from a checkout with the reviewed parent assurance tooling installed:

```text
python DynaDocs.Tests/coverage/run_mutation.py --since <immutable-base-commit>
```

The command snapshots tracked, dirty and nonignored untracked files through the shared isolated
test runner. It asks the parent `gap_check.py --mutation-input` mode for changed source fragments,
test commands and coverage, then uses the parent's all-file fingerprint to detect drift. Missing
parent helpers or evidence returns **2**; fixture reports never establish repository acceptance.

The JSON and HTML paths printed at exit retain the inventory, exact commands, streams, native
reports, baselines, mutation jobs and bootstrap hashes. These artifacts live beneath the calling
checkout's ignored `dydo/_system/.local/mutation/` directory. Disposable candidate worktrees are
removed in `finally`; an unconfirmed process-tree cleanup retains its worktree and fails closed.

| Exit | Meaning |
| --- | --- |
| 0 | Complete evidence; every selected valid mutant killed, or the parent proves no executable change |
| 1 | Complete evidence proves a survivor, uncovered mutation, or changed member with zero valid mutants |
| 2 | Missing, stale, ambiguous, malformed, contradictory or incomplete evidence; dominates exit 1 |

Each changed source fragment is its own obligation. The unique smallest containing fragment owns
a mutation. Nested kills cannot rescue an outer fragment's zero-mutant result. Shared constructor
initializers remain one authored obligation. Scores use `killed / (generated - invalid)` and are
null when that denominator is zero.

## Native execution

Pins come from the shared locks: Stryker.NET 4.16.0, StrykerJS 9.6.1 and CosmicRay 8.7.0. Installs
stay local; reporters are JSON/HTML only. Both configured and observed Stryker concurrency must be
1, with all thresholds 100. Public `--since` selects parent inventory; the .NET engine receives
exact fragment ranges without its native `--since` optimization.

Stryker.NET's primary report retains suppressed block mutations. Supplemental block-only batches
execute each required block while excluding its strict descendants for scheduling. Exact source
hash, path, span, mutator and replacement join each supplemental result back to the primary
denominator. Scheduling-ignored rows contribute no kills, and a conflicting survivor remains a
failure. Every batch retains its own full baseline and native test identities.

StrykerJS uses its command runner with native coverage disabled. The actual Node event reporter
must witness the expected leaf cases and complete suite tree. Parent Istanbul callable execution
and body-point evidence are mandatory; a file or assignment hit cannot cover an uncalled body.
The pinned engine's `__STRYKER_ACTIVE_MUTANT__` variable identifies each mutation. The environment
is preserved, including any unrelated `STRYKER_MUTANT` value. An independent nested Node runner
removes only `NODE_TEST_CONTEXT` from its copied environment and keeps default child isolation.

CosmicRay initializes the complete native database, then executes one selected original WorkItem
per session. Its exit status and `killed` label alone prove nothing: a complete real unittest
lifecycle must show a testcase failure or error. Import/discovery failures, skipped cases, timeouts,
partial suites and missing launch receipts remain incomplete. Syntax-invalid mutated Python is
classified separately after compilation, and exact source bytes are restored after each job.

## Coordinates and immutable judging

The public inventory uses 1-based lines, 0-based UTF16 columns and half-open ends. Positional text
excludes a leading UTF8 BOM; fingerprints preserve the raw BOM and line endings. Native Python
code-point columns are converted to UTF16. Stryker report columns are 1-based; StrykerJS additionally
counts a preserved BOM on the first line, whereas Stryker.NET strips it. Exact source comparison
and boundary checks prevent an adjacent callable from joining the selected fragment.

The orchestrator, normalizer, shared runner, parent fingerprint helper and maintained suite adapters
are copied outside the mutable candidate and hash-checked before launches and acceptance. Each
launcher receives a hashed job envelope through structured arguments; shell-native engines receive
only the fixed launcher plus that opaque envelope. Sidecars identify the run and native job and
must have one matching start and atomic terminal record. Missing, duplicate or stale receipts fail.

The maintained Python adapter and Node reporter remain mutation targets. Tests of those files invoke
their candidate copies while immutable outer adapters account for the actual test cases.

## Verification

```text
python -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_mutation*.py"
python -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_*.py"
```

Native strong/weak, zero-operator, nested-block, Unicode and adapter-as-target wrapper proofs are
retained with the Issue's evidence. Repository M additionally requires the actual parent interface
and a passing integrated baseline. StrykerJS 9.6.1 cannot parse extensionless files: such a target
remains incomplete until the separately reviewed launcher path change makes it a normal `.cjs`
target. No engine patch, source transformation or target exclusion is used.
