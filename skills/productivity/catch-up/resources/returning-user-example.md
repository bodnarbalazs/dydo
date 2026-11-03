# Frozen Dydo project catch-up example

## Example

> **Historical example. Operational snapshot: 30 October 2026, 16:43 UTC.** The questions and actions below belong to that scenario. They need no answers or actions now. The guard and statechart explanations were prepared at 16:39:45 and 16:38:14 UTC. The mutation recommendation includes the later explanation revision at 17:22:08.457 UTC. That revision changes no recorded answer, permission or work status.

🎯 **Dydo 3.6.0 improves the tools and shared methods agents use to work safely and keep delivery moving.** The work includes fixing false shell-command warnings, showing formal-model failures, and acting on completed delivery.

⏸️ **Guard and statechart delivery are on hold. Codex/T3 still lacks genuine overnight approval support.** Delivery guidance and shared-intent planning have landed. The catch-up rewrite is running. Its wording and the release still need your verdict.

### Shell-command guard: the correction is held

#### Purpose

🎯 **Let agents run harmless searches and file writes while keeping real-command protections.**

The October 28 retro found 18 occurrences across six problems. You asked one agent to fix those cases, not redesign the parser. This is Dydo's guard on agent shell commands. It is separate from LC TeasePlayer's checks on user JavaScript.

<details>
<summary>🔎 The six fixes and their scope</summary>

- Quoted search/process text looked like command launches.
- Literal file-write bodies looked executable.
- Python `pass` and quoted `grep PASS` triggered the password-store rule.
- Plain PowerShell read assignments looked like writes.
- PR-command help looked like a mutation.
- Python programs received on stdin could write protected files. This real hole needed refusal.

The work covers `Services/BashCommandAnalyzer.cs`, its shell reader and guard/nudge callers, PR-readiness parsing, paired fixtures and protected-file documentation. Correct refusals must stay correct.

</details>

<details>
<summary>📖 Terms in this area</summary>

- **Guard:** the check that allows or refuses an agent's command.
- **Body text:** text a command receives as input or writes to a file.
- **Relief:** allowing a harmless case that previously caused a warning or refusal.
- **Stdin:** a program's input stream. Another command can feed text into it.

</details>

#### Recently completed

✅ **Round-eight review found two ways the candidate misses real launch warnings.**

A command can print text that a later shell executes. Text that looks like a PowerShell here-string can also expose a real command when Bash reads it. The candidate wrongly treats these cases as harmless data.

✅ **The final correction attempt stopped before its queued proof ran.**

Nested analysis loses where the enclosing command sends its output. A local check cannot then prove that the text stays data. The author returned NOT IMPLEMENTED. No production correction or new runtime proof resulted.

✅ **The admiral prepared the alternatives and what each would preserve or lose.**

You had approved finite relief for quoted text, comments and bodies. Unquoted launch words and unknown wrappers still needed conservative treatment. The existing tribunal allowed one final attempt within that boundary. That attempt exhausted its authority.

<details>
<summary>🔎 What the reviews establish</summary>

Reviewers supplied inert strings to the hook harness. They did not execute the payloads. Earlier green general tests missed both failures.

The analyzer extracts a shell's stdin or script text without retaining the enclosing output consumer. A harmless-looking inner data command can therefore hide later execution, including through a nested shell.

Earlier review removed single-quoted-data relaxation. The eight review FAIL rounds remain. The final feasibility return was not a ninth review.

</details>

#### In flight

⏸️ **No crew is running. Both safety blockers keep the candidate held.**

No new design or implementation has been commissioned.

#### Next

❓ **Should we preserve body-text relief through a new design, reduce that relief, or keep the correction held?** ([DYD-437](https://linear.app/bodnar-balazs/issue/DYD-437))

- **Prepare a bounded design.** Aim to keep the approved harmless cases while fixing both failures. No completed design, feasibility proof or estimate exists. This permits design only.
- **Reduce the relief.** Harmless documentation mentioning a long-running command, or a literal PowerShell here-string written as data, could trigger warnings again. This gives up part of the approved behavior. It needs a precise acceptance change and scoped correction/review. It is not proved safe or cheaper.
- **Keep the correction held.** The six-fix delivery stays unfinished. Safe-merge guard completion still waits for its merge, and host-parity work keeps its prerequisites. Removing that work from 3.6.0 needs a separate release-scope decision.

The old commission is exhausted. A new design boundary or a change to the relief you approved needs your choice.

➡️ **The admiral recommends design only, to establish a feasible approach and cost before implementation.**

It aims to preserve the approved relief. It does not authorize production changes or another full gate campaign. No answer is recorded.

🤖 **AFK, after your choice: the admiral defines and commissions the selected next step.**

For design, the admiral must name the deliverable, captain and author. For reduced relief, it must first record the acceptance change. Without a ruling, the hold remains. The protected-root documentation patch stays user-owned.

<details>
<summary>🔎 The proposed design and narrowing boundaries</summary>

The design must retain the enclosing output consumer, actual shell dialect, exact text span and executable expansions during nested analysis. Unknown cases must stay conservative. It must cover the harmless cases and both known failures, including nested shells. A broad parser rewrite has not been shown necessary.

The narrower alternative would remove the GS-03 body exemption while keeping finite quoted-search fixes and the other fixes. GS-02 file analysis might still recognize a write as literal even when the separate launch check warns about its body. That distinction must survive the acceptance change.

</details>

### Statechart works on Windows and Linux. Delivery is on hold.

#### Purpose

🎯 **Run formal checks and show where a model fails.**

You asked for the upstream statechart skill, including its report and chart. It uses TLC to check a TLA+ model and show the actual state graph and shortest paths to a failure.

The original launcher used POSIX shell tools. Dydo added Windows support and cleanup for the processes it starts. The acceptance questions concern this maintained runner, not a model you might later give it.

<details>
<summary>📖 Terms in this area</summary>

- **Model:** a precise description of a system's states and rules. The checker explores what those rules allow.
- **TLC:** the program that checks a TLA+ model for broken rules. It does not check the application's source code.
- **POSIX:** a family of operating-system standards. Here it means the launcher expects Unix-style shell tools, such as those used on Linux.
- **Coverage:** how much code the tests execute. Executing code does not prove the tests would catch a fault in it.
- **Mutation testing:** deliberately change code, then check whether the tests detect the fault.
- **HCRAP:** our hybrid version of CRAP. It uses cognitive complexity as the base score, then adds a penalty for untested code based on cyclomatic complexity. With full test coverage, the score equals cognitive complexity.

HCRAP is `CC² × (1 − line coverage)³ + cognitive complexity`. CC is cyclomatic complexity. It remains in the coverage penalty; only the base term changes to cognitive complexity. The HCRAP limit is 20. Cyclomatic complexity is not a separate ≤20 gate.

</details>

#### Recently completed

✅ **The adaptation passed its Windows and Linux checks.**

The maintained TypeScript code meets its coverage and complexity limits. Four scenario checks also detected changes to their expected results.

✅ **The native Windows adapter passed its tests and measurements.**

This covers the embedded C# code that manages Windows processes. It does not measure the PowerShell code around it.

✅ **The integration correction and decision brief are ready.**

The evidence is tied to the reviewed source. The candidate still needs to be checked against newer changes on master. These results do not establish overall acceptance.

<details>
<summary>🔎 Evidence and code scope</summary>

The six maintained TypeScript modules under `skills/engineering/statechart/scripts/` are `frontends/dot.ts`, `run.ts`, `statechart.ts`, `worker.ts`, `windows.ts` and `windows-cleanup.ts`. They meet line coverage ≥80%, branch coverage ≥60%, cognitive complexity ≤20 and HCRAP ≤20. All four scenario sensitivity/restoration pairs are complete.

The separately compiled `StatechartJob.Attach` source is byte-identical to the embedded C#. It reached 15/15 lines and 6/8 branches. Cognitive complexity and HCRAP were both 5. Baseline and Coverlet-instrumented tests each passed 6/6 and preserved native error codes 87 and 5. Original PowerShell Add-Type lifecycle proof supplies the separate integration layer. Earlier AltCover failures remain failures.

Candidate `ce3ecdb0` includes master `f3e0a161`. Later master changes are outside that integration result.

Review also led to refactoring beyond the Windows adaptation. The captain commissioned complexity reduction. The entry's score fell from 37 to 18 after extracting output validation, worker launch and publication. Another change simplified a verdict classifier. The record explains that commission. It does not show that extraction was the only design or that you separately approved harmonization.

</details>

#### In flight

⏸️ **No proof work is running. Two gaps keep delivery on hold.**

- **PowerShell measurements are missing.** The launcher and process-cleanup scripts lack the required coverage and complexity measurements. The C# results do not cover them.
- **Mutation testing is missing.** We do not know how well the tests detect faults across the maintained runner. The four scenario checks do not answer that wider question.

DR048 allows a missing measurement method to be recorded. This delivery's later acceptance amendment still requires your decision. The existing three-member tribunal allowed proof work and preparation of these choices. It kept readiness held and waived neither gap.

<details>
<summary>🔎 What remains unmeasured</summary>

`statechart.ps1` selects and starts supported Node. `windows-job.ps1` binds worker descendants to a Windows Job Object so they stop when their owning parent exits. Each script lacks executable-line ≥80%, branch ≥60%, cognitive complexity ≤20 and HCRAP ≤20 measurements. Command coverage and PSScriptAnalyzer results do not supply these.

Mutation scope concerns the six maintained TypeScript modules, two PowerShell scripts and local native adapter. No reviewed TypeScript/PowerShell mechanism was adopted, and no C# campaign ran. Unchanged vendored source is not automatically included.

There is no mutation score or inventory of mutations that survived the tests. The four scenario pairs changed expected values. They did not test sensitivity to defects throughout the runtime.

</details>

#### Next

❓ **Can this delivery proceed without the PowerShell measurements?** ([DYD-434](https://linear.app/bodnar-balazs/issue/DYD-434))

- **Proceed with the gaps recorded.** Keep the existing test evidence. Some branches and complexity remain unmeasured and could hide weaknesses.
- **Require measurements first.** The admiral must prepare a workable method and estimate. Neither is ready. A functional rerun or Pester upgrade alone would not supply the proof.
- **Keep the feature held.** This postpones delivery without starting tooling. Removing it from 3.6.0 needs a separate scope decision.

➡️ **The admiral recommends proceeding with the two scripts' gaps recorded and a named follow-up owner.**

The existing checks provide useful evidence. New measurement tooling has unknown feasibility and cost. This applies DR048's skip-and-record route without calling missing measurements PASS.

❓ **Can this delivery proceed without mutation testing?** ([DYD-434](https://linear.app/bodnar-balazs/issue/DYD-434))

- **Proceed with this separate gap recorded.** Some faults in launch, error handling or cleanup may still escape the tests.
- **Require mutation testing first.** The admiral must define the method, code scope, safety measures and acceptance criterion, then estimate the work. No reviewed campaign or cost estimate exists.

➡️ **The admiral recommends proceeding with the mutation gap recorded and the admiral owning follow-up.**

Platform tests, native-process checks, TypeScript measurements, exact-source C# proof, independent reviews and corrected scenario checks provide evidence. They do not establish how well the tests detect deliberate faults. Requiring mutation first adds an unprepared tooling campaign with unknown feasibility and cost.

These are separate decisions about delivery requirements. Neither is answered in this snapshot. The static missing-mechanism rule does not waive mutation. The earlier tribunal did not decide this choice. A reply can settle either question; no new discussion session is required.

🤖 **AFK, after your answers: the admiral records the limits and arranges the next work.**

Accepting a gap needs an explicit contract amendment, missing proof, code scope and follow-up owner. Requiring more proof means preparing that work first. Later scoped adoption is not commissioned or scheduled. Without the required decisions, delivery stays held.

Latest-master checks, final acceptance checks, PR/CI and human merge still follow. An answer completes none of them.

💬 **HITL, later: compare the upstream skill with Dydo's version in the next walkthrough, after the other work.**

That live discussion covers unchanged modeling guidance, packaging, Windows support and refactoring. It is not a condition for answering the questions above. No later grilling session is commissioned.

<details>
<summary>🔎 Preparation the admiral still owes if tooling comes first</summary>

For PowerShell measurements, the contract must define how to attribute executed lines and branches, measure complexity, and link each result to the correct method and source. Feasibility, duration and findings are unknown.

For mutation, it must define supported code changes, which changed code to include, and how to contain native processes and lifecycle tests. It must also handle mutations that do not change behavior, set an acceptance criterion and provide an estimate. This is new preparation and tooling, not a routine rerun.

</details>

### Completed returns: guidance now requires the next action

#### Purpose

🎯 **Stop completed work waiting for a watch to nudge the admiral.**

The overnight retro found delays after captain returns. It also found costly correction cycles that still failed the strongest reviewer counterexamples. Notification loss was a hypothesis, not an established cause.

#### Recently completed

✅ **The completion and correction guidance is human-merged.** ([PR217](https://github.com/bodnarbalazs/dydo/pull/217))

The admiral must take the next authorized action or fallback, or record an owned hold and reason. Correction authors must check the strongest prior counterexample on the exact successor before expensive broad gates. Independent review and final gates remain.

<details>
<summary>🔎 What landed</summary>

The Admiral paragraph also requires updating the current project state. The Code Writer paragraph carries both the strongest counterexample and the accepted invariant to the affected boundary.

The two-paragraph change landed at 16:32:22 UTC with three hosted checks successful. It implements no host notification, waiting-state or approval behavior.

</details>

#### In flight

⏸️ **No work is running on these paragraphs. The documentation is delivered.**

The receipts do not yet show how the next real handoff behaves.

#### Next

🤖 **AFK: the admiral applies the completion step to the next actual return under its existing authority.**

Authorized work advances. Human-held work keeps its hold and owner. A correction author checks the strongest counterexample before expensive proof. These are landed workflow duties, not a new observation campaign.

### Overnight on/off: fallback works, host approval is missing

#### Purpose

🎯 **Let an authorized agent turn overnight mode on and off through genuine Codex/T3 human approval.**

You had to return after being told goodnight and run the command yourself. The problem recurred at morning shutdown. Your authorization could not create the host approval path that Codex/T3 lacked.

<details>
<summary>📖 Approval in this area</summary>

Approval must be bound to the actual execution in the host. A general instruction from you is not the same as that supported execution path. Dydo's existing path named Claude Code; Codex/T3 could not use it for the mode change.

</details>

#### Recently completed

✅ **Early detection, an exact terminal fallback and independent state readback are merged.**

PR214 reduces surprise and prevents an unverified success announcement. The morning check observed overnight off after your reported command. It did not prove an agent-run transition or working Codex approval.

✅ **The host request is prepared. It has not reached an identified owner.**

The bounded lookup and established contacts found neither a T3 maintainer/support endpoint nor an authorized host checkout.

<details>
<summary>🔎 The observed state and prepared request</summary>

At 11:01:11 UTC, the read-only check returned overnight off after you reported running `dydo mode set overnight off`.

The host request asks for supported human approval and temporary per-thread attachment/close behavior. It specifies lifetime and provider binding. No recipient or writable host checkout has been identified.

</details>

#### In flight

⏸️ **Host support is blocked. A real-UI check is not ready.**

The existing host-support captain owns delivery. The admiral owns routing. PR217's workflow guidance neither implements the host interface nor makes owner discovery wait for it.

#### Next

❗ **Agent-owned prerequisite: the admiral and existing captain need a supported host-owner/access route to deliver the request.** ([DYD-433](https://linear.app/bodnar-balazs/issue/DYD-433))

This enables supported registration and an isolated approval check. It is not an action assigned to you. The source investigation is available; reporting the gap requires no repeat search or new probe.

💬 **HITL, conditional: exercise the genuine approval UI after the captain prepares a supported, isolated check.**

The captain first owes the registration, isolated state, human steps and cleanup procedure. None is ready. The check must eventually observe approval, denial without state change, and readback. Synthetic hooks cannot prove those behaviors. Your 3.6.0 requirement remains actual host support, not fallback alone.

### Shared feature intent: the planning route has landed

#### Purpose

🎯 **Carry shared feature intent into implementation and review.**

You asked whether agents actually use working-spec references. Readers expected a shared source, but project capture and charting could assume someone else had created it.

<details>
<summary>📖 Shared intent in this area</summary>

An **intent anchor** is the agreed source of what a feature must do. It can be a working spec or an adequate reviewed plan. Atomic ticket intent remains enough for work that fits one ticket. Not every task needs a spec.

</details>

#### Recently completed

✅ **Planning skills now choose or create the shared intent anchor.** ([PR218](https://github.com/bodnarbalazs/dydo/pull/218))

The handoff carries the reviewed-plan alternative. Existing author/reviewer rules for pinning intent remain.

✅ **The safe-feature-merging spec and its map/ticket links are delivered.**

The spec records the existing command, guard and host-parity intent. The adoption receipt left the tickets' statuses and blockers unchanged.

<details>
<summary>🔎 Source and landing</summary>

The planning change covers project capture, wayfinding, ticket creation and the admiral's charting step.

The spec is `dydo/reference/working-specs/dydo-3-6-0/safe-feature-merging.md`. The map and affected tickets link its sections. PR218 was human-merged at 16:29:55 UTC. Earlier safe-merge work is not retrospectively called spec-driven.

</details>

#### In flight

⏸️ **No work is running on this documentation. Later guard/parity use remains unproved.**

The documentation and links are delivered.

#### Next

🤖 **AFK, conditional: future authorized guard/parity captains pin the relevant sections in author and reviewer briefs.**

Each lane keeps its prerequisites. A source review and a link do not prove that later work used the spec. This landing does not restart held guard work.

### Project explanations: the rewrite is running

#### Purpose

🎯 **Help a returning reader understand the project and its choices without rebuilding ticket history.**

You rejected the earlier catch-up. You asked for a project account, four plain sections, icons on items and concrete choices. The separate walkthrough lets you inspect the work and discuss whether it fits your intent.

#### Recently completed

✅ **Earlier candidates and reviews are preserved, and the new contract is clear.**

The five-section candidate is historical, not accepted wording. The admiral supplied both technical explanations above and authorized syncing ketchup's description and catalog row with the project scope.

#### In flight

✈️ **The existing captain and docs author are rewriting the prompt and complete example.**

Fresh reviews and a full-example reading follow. Earlier PASSs do not cover this candidate. The facilitator owns the human conversation; the admiral owns delivery and dispatch.

#### Next

🤖 **AFK, commissioned: finish the source and bounded proof, then return it to the captain for fresh review.** ([DYD-442](https://linear.app/bodnar-balazs/issue/DYD-442))

The return includes the complete readable example and exact source binding. Selected icons do not establish source acceptance or your wording verdict.

💬 **HITL, later: read the complete returned example in the walkthrough and give your wording verdict.** ([DYD-432](https://linear.app/bodnar-balazs/issue/DYD-432))

The facilitator will bring it after the captain's return. The verdict concerns clarity and usefulness. It does not accept the guard, waive statechart proof or approve release. Other landings belong in staged walkthroughs, with a final walkthrough before release approval.

## Optional provenance

- [Guard's six-fix contract](https://linear.app/bodnar-balazs/issue/DYD-406), [terminal disposition](https://linear.app/bodnar-balazs/issue/DYD-437) and [prepared guard explanation](https://linear.app/bodnar-balazs/document/guard-decision-brief-original-six-fixes-lost-relief-and-bounded-design-0fee2c0f8f92), frozen at 2026-10-30T16:39:45.276Z.
- [Statechart adoption](https://linear.app/bodnar-balazs/issue/DYD-415), [acceptance record](https://linear.app/bodnar-balazs/issue/DYD-434) and [prepared statechart explanation](https://linear.app/bodnar-balazs/document/statechart-acceptance-local-runner-proven-behavior-remaining-assurance-a2263a58d7ad), frozen at 2026-10-30T16:38:14.227Z and revised at 17:22:08.457Z.
- [Completion/counterexample guidance and human landing receipt](https://linear.app/bodnar-balazs/issue/DYD-438); [actual-host support prerequisite](https://linear.app/bodnar-balazs/issue/DYD-433).
- [Shared-intent anchor and human landing/adoption receipt](https://linear.app/bodnar-balazs/issue/DYD-443).
- [Catch-up correction](https://linear.app/bodnar-balazs/issue/DYD-442) and [staged human walkthrough](https://linear.app/bodnar-balazs/issue/DYD-432).
- Prior LC examples remain in earlier committed revisions. The original failed reply and HTML remain on [DYD-439](https://linear.app/bodnar-balazs/issue/DYD-439).

The pinned records support this reconstruction. Earlier reviews bind their earlier text. Balazs's verdict on this complete example remains outstanding.
