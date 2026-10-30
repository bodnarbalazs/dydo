# Historical returning-user example

Reconstructed LC Test optimisation catch-up from 2026-10-30, after a roughly ten-hour absence. This illustrates the ketchup contract in the source admiral's voice. It uses the retained merge snapshot and read-only owner records, including the subsequently verified mode-off correction. It is not a current status report, an execution commission or authority to change any project's policy. The original failed reply and HTML remain unchanged on [DYD-439](https://linear.app/bodnar-balazs/issue/DYD-439).

The example below stands alone; provenance is optional. This revised structure awaits Balazs's judgment through DYD432. Earlier reviews apply only to their earlier text.

## Example

*Historical catch-up · 30 October 2026. This reconstructs the retained snapshot, not current operations or new execution authority.*

The goal is **cheaper testing without losing protection against breakage elsewhere**. Three improvements are merged: clearer rules for running relevant tests, a safer recipe for retaining test evidence, and a realistic deadline for one flaky test. They improve the testing workflow; we have not measured a speed-up.

The focus now is finishing the extra-test system and proving the Linux setup works. The extra-test fix is held after failed verification, a separate cleanup fix still lacks Linux proof, and the Linux updater still needs live acceptance. **One policy decision is ready for you:** how a command should report missing expensive-test evidence. Installation will need you later, after readiness checks. Optional policy changes can stay parked; the agents own the remaining preparation.

### Missing test evidence — your decision is ready

**🎯 Purpose & now**

A test result should distinguish broken code from code that has not yet been fully checked. Routine checks can pass while designated expensive checks remain outstanding. Today both missing proof and failed tests return the same failure exit code. The choice of a clearer signal is reserved for you.

**⏮️ Before**

An agent can finish the routine checks successfully yet receive a failure code because 45 source-code entries on the project's explicit list still await their designated expensive checks. Here, coverage means evidence that tests exercised the code being assessed. Missing coverage evidence does not establish that the code is broken.

**🛠️ Work & decisions**

The printed result remains **PENDING**, not PASS. The unresolved policy is the exit code that scripts read. No change to that policy, or permission to merge without required proof, has been approved by this catch-up.

**➡️ Next**

Once you choose, the responsible agents can carry the decision into the command and its callers under the normal delivery process. Until then, today's failure code stays. Other work can continue within its existing limits.

**🙋 Attention & blockers**

I recommend a **distinct “evidence pending” exit code**: scripts could tell “not proved yet” from “tests failed,” while the printed result stays PENDING and required proof is still owed.

The alternatives are to keep the failure code and have callers interpret the text; return success, which blurs missing proof with complete proof and requires changing the governing policy; or routinely run the expensive checks, which adds cost. Reusing evidence from other commits or machines would be a separate design, not an approved shortcut.

Your answer would settle how agents and scripts interpret this gate. The recorded ruling reserves that policy choice for you, so I cannot settle it myself. Without an answer, this policy change waits; it does not freeze the other authorized work.

---

### Everyday testing — three improvements merged; no decision needed

**🎯 Purpose & now**

Routine development should run the relevant checks and leave trustworthy evidence. The testing policy, evidence-retention recipe and one test deadline are now improved. These are merged changes, not a measured speed-up or proof that the entire testing project is finished.

**⏮️ Before**

Running whole suites after each edit spent effort beyond the feature being changed. Some runs lost proof files when concurrent runs rotated shared temporary folders. A test checking that a child process reports an error had only half a second to finish, making a slow response look like a timeout.

**🛠️ Work & decisions**

- **Relevant checks while building.** The policy you approved now tells agents to normally run the affected feature's fast tests. Expensive tests for that feature belong at final gates; whole suites belong in periodic CI or an explicit request from you.
- **Proof survives cleanup.** The new recipe gives each evidence-producing run its own temporary location and collects the proof before cleanup. It changes the instructions, not pytest's global retention.
- **A realistic error-test deadline.** The child-process error test now gets ten seconds. Its assertions remain intact, and the separate test that deliberately times out keeps its half-second limit.

**➡️ Next**

I own finishing the post-merge records so the recorded delivery state matches these changes. The separate proposal to split test groups stays deferred until its owner updates it against the policy you already approved.

**🙋 Attention & blockers**

Nothing is needed from you for these delivered changes. Updating records and preparing a deferred proposal are agent work, not another decision for you.

---

### Extra tests for collateral breakage — held with agents; optional policy parked

**🎯 Purpose & now**

**Ripple** chooses a small extra set of tests from past failures to catch damage outside the feature being changed. A fix to its incident counts is held after two failed verification runs. The agents must establish a permitted way to investigate before recovery can proceed; no ready approval request is being put to you.

**⏮️ Before**

If one browser test fails and twenty later tests cannot run because of it, those twenty skipped tests can make one failure look like a broad incident. You already approved correcting that behavior. A different case is one shared fixture breaking and preventing more than twenty tests from running: the foundation itself is broken, and current policy still treats that as a broad incident.

**🛠️ Work & decisions**

The browser-skip fix has not passed verification, and its remote pull request conflicts with newer code. The environment investigation could not establish that Git Bash's temporary-directory mapping could be inspected without touching another task's files. Review rejected the proposed check, and a third run was not started. This does not prove that another task caused the failures.

**➡️ Next**

The responsible captain and I own preparing a concrete, compliant inspection method, or keeping the hold if none exists. Once that prerequisite is cleared, the captain owns reconciling the fix with current code, passing verification and obtaining fresh review before offering it for merge.

**🙋 Attention & blockers**

**The blocker belongs to the agents:** a permitted inspection method is still missing. Asking you vaguely to “change the evidence boundary” would not provide a concrete proposal to approve.

**The shared-fixture policy is optional and can stay parked.** I recommend keeping the current broad-incident behavior because the shared foundation is broken. Counting that failure once for Node and Vitest would add a new grouping rule; extending it to Python would also require additional failure evidence. Changing that definition is your policy choice, distinct from the already-approved browser-skip fix. It blocks nothing, and no answer is needed to retain today's behavior.

---

### Cleaning up hung setup tests — Linux proof blocked; preparation belongs to agents

**🎯 Purpose & now**

A hung setup-wizard test should not leave child processes running. The cleanup fix passed its Windows proof, but Linux proof never began. It remains held while the agents prepare a bounded investigation; there is no ready decision for you here.

**⏮️ Before**

A wizard test that hung could leave its scripts' child processes behind after the test stopped.

**🛠️ Work & decisions**

The fix has Windows evidence. On Linux, container setup stopped at a check of process resource isolation before the actual test could run. The retained evidence does not establish why that check stopped, so this is missing Linux proof, not a Linux test pass or an established cause.

**➡️ Next**

Its captain and I own preparing a limited diagnosis of which Linux process-isolation controls are available and whether they enforce the required resource limits. That diagnosis needs its own allocation. Linux proof and fresh whole-change review are still required afterward.

**🙋 Attention & blockers**

No action is requested from you now. The captain and I must prepare the diagnosis and establish its allocation before another run. This catch-up authorizes no further test run and does not lift the hold.

---

### Linux test-machine update — human installation later, after readiness checks

**🎯 Purpose & now**

The Linux test machine needs the reviewed updater and the code that lets agents retrieve the published list of extra tests. Live acceptance is still open. You will have a human-only installation step later; the agents must first establish that the machine is ready.

**⏮️ Before**

The last inspection found a real job still running, an old separately installed updater, and the new Ripple-list retrieval code absent from the deployed release. These are historical observations, not a claim about the machine's health now.

**🛠️ Work & decisions**

The updater code is merged, but that does not show that the deployed machine has it or that list retrieval works there. The existing contract reserves running the installation wizard for you. We will not stop a healthy job merely to create an installation window.

**➡️ Next**

I own checking current readiness and coordinating with the host owner. Once ready, I will bring the tested command and a concrete idle window. After your installation, we still need to verify the installed version and health, then check that agents can retrieve the published extra-test list from the Linux host.

**🙋 Attention & blockers**

**Later human step; no scheduling answer needed now.** I recommend proceeding when the machine is genuinely idle. Proceeding then installs the reviewed updater and enables those live checks; deferring preserves running work but leaves live acceptance open. I cannot replace an explicitly human-run installation with an agent action. Readiness preparation remains mine with the host owner.

---

### Reliable project status — no confirmation needed from you

**🎯 Purpose & now**

The project summary should tell you what is actually ready and who acts next. The mode flag has been checked, and reconciling stale summaries remains my work. You do not need to repeat a confirmation.

**⏮️ Before**

The failed catch-up asked you to confirm that overnight mode was off and left you to interpret stale board summaries alongside completed work and held work.

**🛠️ Work & decisions**

The subsequent read-only check returned **overnight off**. That resolves the mode question for this historical snapshot; it is not a fresh readback of today's state.

**➡️ Next**

I own reconciling stale board summaries with the recorded outcomes and holds, so current descriptions do not present parked work as ready.

**🙋 Attention & blockers**

Nothing is needed from you for the mode flag or these record updates. Neither a stale summary nor this catch-up releases a hold.

## Optional provenance

- Delivered feature-testing policy, evidence-retention recipe and error-test deadline: LC1374, LC1359 and LC1358, with the retained DYD439 merge snapshot.
- Extra-test incident-count fix: LC1342; [inspection constraint and recovery hold](https://linear.app/bodnar-balazs/document/lc-1385-prepared-morning-decision-strict-boundary-or-explicit-new-eeba2113926d).
- [Hung-wizard cleanup and Linux proof hold](https://linear.app/bodnar-balazs/issue/LC-1378#comment-86fefc6d-8f8a-475e-935c-a76fc8b8e3d4).
- [Updater's historical host observations and human installation contract](https://linear.app/bodnar-balazs/document/lc-1372-exact-candidate-proof-6f8908182-31b5345a6907).
- [Pending-evidence exit-code choice and user reservation](https://linear.app/bodnar-balazs/issue/LC-1240#comment-84f98472-28da-40d3-998c-a4f5f7184122).
- [Optional shared-fixture incident grouping](https://linear.app/bodnar-balazs/issue/LC-1375).
- Deferred test-group split: LC1381. Mode-off readback: the retained DYD439 description.
