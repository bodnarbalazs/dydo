# SOURCE BLOCKED: cancellation ownership is not closed

code-writer/model: gpt-6.1-sol/high. Captain/records only.
Controlling [captain contract](https://linear.app/bodnar-balazs/document/dyd-415-two-signal-source-only-diagnostic-custody-and-scope-a1923d77f1d5)
and [failure disposition](https://linear.app/bodnar-balazs/document/dyd-415-signal-proof-timeout-and-stopped-lane-diagnosis-disposition-f477bc7c245a)
read fully. HEAD remains6e3fe7e8018130af6b8586ef0cf40c1474740864 on the retained
DYD-415-statechart worktree. No product/canonical-test/payload/history edits.

## Exact obstacle, source established only

The contract requires prospective cancellation to settle the async body, close
owned watchers, terminate identity-proven owned children and actually reap direct
children before return; it also requires a bounded case without relying on the
broad outer timeout. The prepared draft does not close that contract.

In diagnostic.mjs's finally block, the draft closes its watcher/observation timer,
sends controlled SIGTERM to the exact CLI ChildProcess, then after5s attempts
identity-checked worker/janitor group cancellation while the CLI is alive. Missing
ownership evidence is retained as uncertainty, never used to signal an unknown
PID. After a further5s it can force-kill the direct CLI and close its owned read
pipes; it then waits5s for actual CLI exit/close. If that observation remains
unconfirmed it throws STOP. That throw settles the async body WITHOUT confirmed
direct-child close/reap. Calling this cleanup success would violate the contract.
Changing that branch to an unbounded await merely hands the hang back to the
native50s/Python65s/outer1200s emergency deadlines, which are not permitted as
the normal cancellation mechanism. No such change was made.

There is a second ownership limit on that path: if descendant identity/close
cannot be established, forcing the original CLI can orphan worker/janitor
processes into the known non-reaping Python PID1. The strict namespace guard
must reject residual live or zombie processes; exact-ID container teardown is
separate infrastructure drain, not product reaping or a settled-case proof.
Destroying test-owned read pipes does not establish descendant completion.
The draft never converts missing PID data, a kill return or a timeout to close.

This is a concrete uncovered return path in the proposed source, NOT an observed
runtime failure, NOT proof that every cancellation will fail, and NOT a claim
that the entire statechart capability is infeasible. The two original30s signal
failures remain unattributed. Diagnostic authoring stopped at this finding per
the captain's explicit stop condition. No generated assembler, driver, fixture,
test, discovery, container, runtime, signal or probe was executed.

## Retained draft and minimal proposed selectors

inventory.json preceded authoring. before-custody.json freezes existing RMI
source/evidence bytes and the two current live files/payload declarations;
before-status.txt records the authorized dirty draft. All three optional TEMP
counterparts and this successor directory were absent before inventory. No TEMP
counterpart was subsequently materialized. The optional assembler was not authored.

The partial diagnostic.mjs/preload.mjs source records the original fs.watch
callback separately from ready-created and2ms polling ready-observed witnesses.
Logging occurs outside the watched proof directory. Polling cannot send a proof
signal, and no explicit readiness handshake is proposed. It records real
signal-send attempt/result, scratch-only original-handler entry, child spawn/
PID/starttime/PPID/group/session, exit/close, watcher closure and cancellation.
The handler wrapper forwards the actual original listener, this and arguments;
sync logging still changes timing and may fail. No original libuv timing or
Docker/inotify cause is asserted. Original signal/status/stdout/stderr/output/
janitor/worker assertions remain in the draft; cancellation success cannot
satisfy them after a prior failure.

commands.json proposes only:

- diagnose original fs.watch controlled SIGINT boundary;
- diagnose original fs.watch controlled SIGTERM boundary.

Each would be one separate native command and would require a separate admitted
signal. No auto-chain, healthy Counter, ten partial ok, B28,22-contract or real-TLC
C repeat. Proposed bounds are30s original boundary,5+5+5s cancellation windows,
50s native emergency,65s direct runner emergency/5s direct runner drain,16MiB
regular-file limits and the inherited1200s outer attach ceiling. These are
PROPOSALS WITH AN OPEN CANCELLATION OBSTACLE, not an inspected safe envelope.

The driver/launcher draft reuses the prior owned network-none container/FIFO/
nonwaiting Heavy/exclusive normal TEMP lock pattern. worker.py reuses only frozen
Node-DATA preparation, source verification, environment and strict census from
the admitted foundation worker. It does not call the old Counter or regression
functions. The reused51-file DATA/source snapshot plus two new MJS files is
finite; canonical tests and CounterInstance are unexecuted reference data.
Official checker/helper remain DATA and no JRE/JDK/npm/host runtime is supplied.
The frozen manifest copy and original two-file historical mapping are unchanged.

No execution-release artifact or future output/run directory exists. The
instrumentation.diff requested for a completed fixture packet is not authored:
source preparation stopped before that checkpoint. The current exact draft
sources and hashes are retained for diagnosis of this obstacle, not represented
as a complete runnable source closure or assurance PASS. New maintained helper
direct tests/static/coverage and discovery remain owed; associations stay frozen.

## Decision needed from captain/admiral

Do not release these drafts. A next source disposition must close ownership of
the exceptional cancellation path before any diagnostic execution. The narrow
question is whether a separately pinned scratch-only cancellation seam may retain
the original CLI's actual child handles and observe their real close while the
CLI is alive, or which existing authoritative ownership mechanism can provide
equivalent bounded drain without reparenting/reaping substitutions. This is a
proposal for inspection, not permission inferred here to add a handler,
supervisor, init/subreaper, container topology or product hook. An outer emergency
kill may retain failed evidence but cannot certify the mandated settled case.

after-custody.json/source-pin.json bind the stopped source packet and read-only
hash audit. The custody auditor imports no project module, writes no file, and
runs only Git metadata reads; its stdout may be retained as a custody record.
It is not an assembler/driver and does not create runtime inputs or run a recipe.
One initial variable-based shell capture was rejected by the command-analysis
hook before execution; the literal native Get-FileHash pipeline captured custody
instead. No approval bypass or runtime alternative was used.

Frozen correction pin30bbe7be70c9482971410d1ddef6764481da0828237c5d1d3a6096f8ec73efce
and failure receiptcdedf5f1729f6f15e405c26235be0d22329de25204f54ad786d9651fd966754a
remain unchanged. Original C FAIL/attribution RED/focused FAIL are immutable.
Clean Counter is narrow normal-drain evidence; ten ok records are not GREEN.
Abrupt-parent/PID1, desktop Windows Java HOLD, isolated Windows/prompt proof,
Java assurance and all final acceptance obligations remain. No readiness,
PS retirement, release/version/tag/publish/rollout, review, commit/PR/push/merge,
new crew or delegation. Both settled HITL threads remain NO CONTACT.
