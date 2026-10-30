# Historical returning-user example

## Example

> **Historical example for reviewing ketchup — no answers or actions are requested now.** This reconstructs the LC Test optimisation snapshot of 30 October 2026. The policy and installation asks below belong to that old scenario; they are not current instructions or new permissions.

**The goal is cheaper testing without losing protection against breakage elsewhere.** Three everyday-workflow improvements are merged: relevant-test guidance, a safer evidence-retention recipe and a realistic deadline for one test. No measured speed-up is claimed.

**The focus is now the unfinished extra-test system and Linux verification.** One exit-code policy choice is ready for you; installing the updater will need you later. The browser-test fix and Linux cleanup proof remain held, while optional policy changes can stay parked. The record identifies work owners by role, but does not establish active commissions for the remaining planned steps.

### Missing test evidence — one policy question is ready

**🎯 Purpose**

**Tell missing proof apart from broken code.** Agents and scripts need to distinguish a failed test from code that has not yet received all its required checks.

**⏮️ Recently completed**

**No completed change to this exit-code policy is recorded.**

**🛠️ In flight**

**The policy remains open; missing evidence still returns the failure code.** Routine checks can pass while 45 source-code entries on the project's explicit list await designated expensive checks. Coverage means evidence that tests exercised the code being assessed. The printed result is PENDING, not PASS; this does not establish that those entries contain defects. An implementation owner and active commission are not identified in the record.

**➡️ Next**

- **Settle the reporting policy — with you, before changing the command.** Your ruling determines how missing expensive-test evidence should be signaled.
- **Commission the command-and-caller change — planned agent work after that ruling.** The delivery owner must be identified before implementation, proof and review. Scripts that consume the result need to handle the selected signal too.

**🙋 Your attention**

❓ **Should missing expensive-test evidence have its own exit code, distinct from failed tests?** ([LC-1240](https://linear.app/bodnar-balazs/issue/LC-1240#comment-84f98472-28da-40d3-998c-a4f5f7184122))

I recommend a distinct **“evidence pending”** code while keeping the printed result PENDING. Callers could distinguish “not proved yet” from “tests failed,” and required proof would still be owed.

Other ways forward:

- **Keep the failure code.** Callers must interpret the text to distinguish missing proof from failed tests.
- **Return success.** This blurs incomplete and complete proof and requires changing the governing policy.
- **Run the expensive checks routinely.** This addresses the missing proof itself, at added cost. Reusing evidence from other commits or machines would need a separate design.

The recorded ruling reserves this policy choice for you. Your answer settles the direction for the command and its callers, not a complete numeric-code migration or permission to merge without proof. Without an answer, this policy change waits; other authorized work can continue.

---

### Everyday testing — three improvements merged

**🎯 Purpose**

**Spend testing effort where it matters and keep the proof inspectable.** Routine development needs relevant checks, evidence that survives long enough to review, and test deadlines that match what is being tested.

**⏮️ Recently completed**

- **The feature-testing policy is merged.** Agents now normally run the affected feature's fast tests while building. Expensive tests for that feature belong at final gates; whole suites belong in periodic CI or an explicit request from you. This implements the policy you approved.
- **Evidence retention has a safer recipe.** Each evidence-producing run gets its own temporary location and collects proof before cleanup, addressing losses when concurrent runs rotated shared folders. This changes instructions, not pytest's global retention or a guarantee that evidence cannot be lost.
- **The error-reporting test now gets ten seconds.** It previously had half a second to prove that a child process reports an error. Its assertions remain intact, and the separate deliberate-timeout test keeps its half-second limit.

**🛠️ In flight**

**Post-merge records remain unfinished; the project coordinator owns them.** The separate proposal to split test groups remains deferred.

**➡️ Next**

- **Finish the delivery records — project coordinator; planned, without needing you.** Use the merged outcomes to bring the records up to date.
- **Update the test-group proposal before reconsidering a split — deferred.** Its owner must reconcile it with the already-approved policy. The record does not identify that owner or a commission to resume.

**🙋 Your attention**

**Nothing is needed from you for these merged changes or their records.**

---

### Extra tests for collateral breakage — recovery held

**🎯 Purpose**

**Catch damage outside the feature being changed.** Ripple selects a small extra set of tests from past failures. Its incident counts should distinguish one browser failure with later skipped tests from a broken shared test foundation.

**⏮️ Recently completed**

- **Two verification attempts failed.** The browser-skip counting fix has not passed verification; a third run was not started.
- **The proposed environment inspection was rejected in review.** The investigation could not establish how to inspect Git Bash's temporary-directory mapping without touching another task's files. That does not prove another task caused the failures.

**🛠️ In flight**

**Recovery is held on a permitted inspection method, and the change conflicts with newer code.** The project coordinator and browser-skip correction captain own preparing the method.

**The browser-skip correction is approved; shared-fixture regrouping is still optional.** If one browser test fails and twenty later tests cannot run because of it, those skipped tests should not inflate one failure into a broad incident. A shared fixture breaking and preventing more than twenty tests from running is different: current policy treats that broken foundation as a broad incident.

**➡️ Next**

- **Prepare a compliant inspection method — coordinator and browser-skip captain; planned agent work.** Respect the existing access boundary. If no permitted method can be established, retain the hold.
- **Resume correction and proof only after that prerequisite clears — browser-skip captain; conditional work.** Reconcile the code conflict, pass verification and obtain fresh review before offering the change for merge.

**🙋 Your attention**

❗ **Agent blocker: the coordinator and browser-skip captain must establish a permitted inspection before recovery.** ([LC-1385](https://linear.app/bodnar-balazs/document/lc-1385-prepared-morning-decision-strict-boundary-or-explicit-new-eeba2113926d))

There is no concrete proposal ready for you to approve. A vague request to change the access boundary would not resolve the missing method.

❓ **Optional: should a shared-fixture failure still count as a broad incident?** ([LC-1375](https://linear.app/bodnar-balazs/issue/LC-1375))

I recommend keeping today's behavior because the shared foundation is broken. Counting the failure once for Node and Vitest would add a grouping rule; extending it to Python would also need additional failure evidence. This definition is your policy choice. An answer changes that separate grouping direction, not the browser-skip recovery hold. No answer retains current behavior and blocks nothing.

---

### Cleaning up hung setup tests — Linux proof still blocked

**🎯 Purpose**

**A stopped wizard test should leave no child processes behind.** Cleanup must work when a setup-wizard test hangs, including on Linux.

**⏮️ Recently completed**

- **The cleanup fix passed its Windows proof.**
- **The Linux attempt stopped before the test began.** Container setup halted at a check of process resource isolation. The retained evidence does not establish why that check stopped.

**🛠️ In flight**

**Linux verification is held pending a separately authorized diagnostic.** The cleanup-fix captain and project coordinator own preparing the limited investigation. A further attempt needs a separate allocation: permission to run with a bounded scope. The record does not identify its grantor or show it granted.

**➡️ Next**

- **Define the diagnostic and establish who can authorize it — cleanup-fix captain and coordinator; planned preparation without you.** Determine which Linux isolation controls are available and whether they enforce the required resource limits.
- **Run the diagnostic, then obtain Linux proof and fresh review — conditional on permission and a usable environment.** The cleanup-fix captain owns the route; execution remains held until its prerequisites are resolved.

**🙋 Your attention**

❗ **Agent blocker: the cleanup-fix captain and coordinator must establish permission and scope before another diagnostic attempt.** ([LC-1378](https://linear.app/bodnar-balazs/issue/LC-1378#comment-86fefc6d-8f8a-475e-935c-a76fc8b8e3d4))

Preparation can proceed within existing limits, but the record supplies no prepared approval request for you. Missing Linux proof and fresh review remain owed.

---

### Linux test-machine update — installation needs you later

**🎯 Purpose**

**Let agents use the reviewed updater and retrieve the published extra-test list.** The Linux machine must have the right release installed and its live behavior checked.

**⏮️ Recently completed**

- **The updater code is merged.** This does not establish deployment or successful list retrieval.
- **The last host inspection found readiness obstacles.** A real job was running, an older updater was installed separately, and the deployed release lacked the new Ripple-list retrieval code. No later health observation is established.

**🛠️ In flight**

**Live acceptance remains open; a ready installation window is not established.** The project coordinator owns readiness preparation with the Linux host owner. A healthy running job must not be stopped merely to create a window.

**➡️ Next**

- **Check readiness and prepare a tested command and idle window — coordinator with the host owner; planned agent work.** This comes before asking you to schedule or install anything.
- **Run the updater wizard — with you, after that preparation.** The existing contract reserves installation to you; it is not yet scheduled.
- **Verify version, health and extra-test-list retrieval — planned agent work after installation.** The record says these checks are owed but does not identify their commissioned owner.

**🙋 Your attention**

❗ **Later, run the updater wizard when ready so the Linux installation can be checked and agents can retrieve the extra-test list.** ([LC-1372](https://linear.app/bodnar-balazs/document/lc-1372-exact-candidate-proof-6f8908182-31b5345a6907))

I recommend proceeding when the coordinator brings the tested command and a genuinely idle window. Running the wizard installs the reviewed updater and enables the follow-up checks; deferring preserves running work but leaves live acceptance open. The contract requires your execution, not general permission for an agent to substitute. No command or scheduling answer is ready yet.

---

### Reliable project status — no confirmation needed

**🎯 Purpose**

**Make the project summary match the work.** You should be able to distinguish delivered, ready and held work without reconstructing the board yourself.

**⏮️ Recently completed**

**The read-only mode check returned overnight off.** The earlier request for you to confirm the flag is resolved.

**🛠️ In flight**

**Stale summaries still need reconciliation; the project coordinator owns it.**

**➡️ Next**

**Reconcile current descriptions against recorded outcomes and holds — coordinator; planned, without needing you.** Parked work must remain described as parked.

**🙋 Your attention**

**Nothing is needed from you for the mode flag or these record updates.**

## Optional provenance

- Delivered feature-testing policy, evidence-retention recipe and error-test deadline: LC1374, LC1359 and LC1358, with the retained DYD439 merge snapshot.
- Extra-test incident-count fix: LC1342; [inspection constraint and recovery hold](https://linear.app/bodnar-balazs/document/lc-1385-prepared-morning-decision-strict-boundary-or-explicit-new-eeba2113926d).
- [Hung-wizard cleanup and Linux proof hold](https://linear.app/bodnar-balazs/issue/LC-1378#comment-86fefc6d-8f8a-475e-935c-a76fc8b8e3d4).
- [Updater's historical host observations and human installation contract](https://linear.app/bodnar-balazs/document/lc-1372-exact-candidate-proof-6f8908182-31b5345a6907).
- [Pending-evidence exit-code choice and user reservation](https://linear.app/bodnar-balazs/issue/LC-1240#comment-84f98472-28da-40d3-998c-a4f5f7184122).
- [Optional shared-fixture incident grouping](https://linear.app/bodnar-balazs/issue/LC-1375).
- Deferred test-group split: LC1381. Mode-off readback: the retained DYD439 description.
- Original failed reply and HTML, preserved unchanged: [DYD-439](https://linear.app/bodnar-balazs/issue/DYD-439).

This reconstruction uses the retained merge snapshot and owner records, including the mode-off correction. The wording awaits Balazs's verdict through DYD432. Earlier reviews bind only their earlier text.
