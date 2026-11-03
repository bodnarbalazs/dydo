# Exact-owned lock retention and recovery

code-writer/model: gpt-6.1-sol/high. SOURCE ONLY; no live lock was acquired, examined for deletion, or removed.

The successor calls the actual retain_and_release path from main's finally block. It starts retained=false, sets it true only after common.retain_work returns, and never lets a successfully saved retain-work-error record stand in for retention. Deletion additionally requires confirmed owned cleanup, no accumulated evidence-write errors, and byte-for-byte equality with this invocation's lock_bytes. A changed or missing lock becomes a failure; it is never treated as permission. The prior product/retention failure object remains the returned error and eventual nonzero cause. New error records do not manufacture product PASS.

The direct tests call this path using isolated TemporaryDirectory locks. They cover successful logging after retain_work throws, healthy release, logging failure, preexisting evidence failure, cleanup uncertainty, changed/missing owner bytes, original failure priority and actual main's nonzero propagation. The main test supplies fake booking/Heavy contexts and a temporary TEMP root. It cannot damage the shared live FIFO, Heavy mutex or normal TEMP dydo-fullsuite.lock. Unit temporary-directory cleanup removes only those unittest fixtures; it is not failed-run lock recovery.

A real failed-run lock is owned by its recorded original run owner (this retained writer for this lane). Captain/admiral must coordinate with that owner and issue a case-specific recovery disposition AFTER assessing:
- exact lock raw bytes, admission owner/ticket/nonce/command and route identity;
- current default LC/host namespace, Docker executable/context/endpoint/daemon where relevant;
- all recorded exact-owned container IDs and labels, volume name/nonce, process identities and independent absence/teardown receipts;
- whether raw evidence and scratch work have been fully retained, which records failed to write and whether authorized recovery can retain them;
- any live booking/Heavy ownership and other project writers.

Cleanup success, stale age, a missing PID, a dead parent, an empty namespace, a matching filename or a generic stale-lock policy cannot authorize unlink. No foreign cleanup, guessed ID/name cleanup or automatic recovery is implemented here. If identities/absence/evidence cannot be established, keep the owned lock and return the concrete uncertainty. FIFO/Heavy context release remains the existing behavior; retained TEMP lock continues to refuse a later suite.

No retention failure occurred in the retained successful SIGINT/SIGTERM artifacts. Their exact recorded results remain credited. This source correction does not relabel a historical release or repair an old receipt.
