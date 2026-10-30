# Frozen Dydo project catch-up example

## Example

> **Historical Dydo example, with operational state frozen at 30 October 2026, 16:43 UTC.** These questions and actions illustrate the scenario. They need no answers or actions now. The guard and statechart explanations were prepared at 16:39:45 and 16:38:14 UTC. The mutation recommendation includes the statechart explanation's revision at 17:22:08.457 UTC. That later explanation changes no recorded answer, permission or work status.

🎯 **Dydo 3.6.0 improves the tools and shared methods agents use to work safely and keep delivery moving.** The work includes a shell-command guard that rejects harmless searches and writes, a tool that shows formal-model failures, and guidance for acting on completed delivery.

🧭 **The delivery guidance has landed. Guard and statechart acceptance are unresolved, and Codex/T3 still lacks genuine overnight approval support.** The admiral has prepared both technical explanations below. The catch-up rewrite is running. Its wording and the release still need your verdict.

### Agent shell-command guard

#### Purpose

🎯 **Let agents run harmless searches and file writes without losing real-command protections.**

This is Dydo's guard on agent shell commands, separate from LC TeasePlayer's checks on user JavaScript. The October 28 retro found 18 occurrences across six problems. You asked one agent to fix those cases:

- Quoted search/process text and literal file-write bodies looked executable.
- Python `pass` and quoted `grep PASS` triggered the password-store rule.
- Plain PowerShell read assignments looked like writes, and PR-command help looked like a mutation.
- Python programs received on stdin could write protected files. That real hole needed refusal.

The work covers `Services/BashCommandAnalyzer.cs`, its shell reader and guard/nudge callers, PR-readiness parsing, paired fixtures and protected-file documentation. Existing correct refusals must stay correct. The commission excluded a parser redesign.

#### Recently completed

✅ **Round-eight review found two ways the candidate misses real launch warnings.**

- **Printed text can become code downstream.** A command prints a literal body, then a shell receives and executes it. Treating the first command as harmless hides that execution.
- **The shell dialect changes what text means.** Text that looks like a PowerShell here-string can close a Bash quote and expose a command. The candidate wrongly treats it as data.

Reviewers supplied inert strings to the hook harness. They did not execute those payloads. Earlier green general tests missed both failures.

✅ **The final conditional attempt stopped before its queued proof ran.**

Nested analysis extracts a shell's stdin or script text but loses where the enclosing command sends its output. A local check cannot then prove that the text stays data downstream. The author returned NOT IMPLEMENTED. No production correction or new runtime proof resulted. The eight review FAIL rounds remain; this was a feasibility return, not a ninth review.

✅ **The admiral prepared the alternatives and the relief each would preserve or lose.**

Earlier review removed single-quoted-data relaxation. You then approved finite relief for quoted text, comments and bodies, with unquoted launch words and unknown wrappers still treated conservatively. The existing tribunal allowed one final attempt within that boundary. That attempt exhausted its authority.

#### In flight

⏸️ **The candidate is held with both safety blockers. No crew is running.**

No new design or implementation has been commissioned.

#### Next

❓ **Should we design a way to preserve body-text relief, reduce that relief, or keep the correction held?** ([DYD-437](https://linear.app/bodnar-balazs/issue/DYD-437))

The old commission is exhausted. A new design boundary or a reduction in the relief you approved needs your choice.

- **Prepare a bounded design.** It must show how nested analysis keeps track of the output consumer, shell dialect, exact text span and executable expansions. Unknown cases must remain conservative. It must cover the harmless cases and both failures, including nested shells. No completed design, feasibility proof or estimate exists. A broad parser rewrite has not been shown necessary. This choice permits design preparation only.
- **Reduce body-text relief.** The proposed boundary removes the GS-03 body exemption but keeps the finite quoted-search fixes and other fixes. Harmless documentation that mentions a long-running command could trigger a launch warning again. So could a literal PowerShell here-string written as data. GS-02 file analysis might still recognize the write as literal. No implementation proves this narrower route safe or cheaper. It needs a precise acceptance change and scoped correction/review.
- **Keep the candidate held.** The six-fix delivery remains unfinished. Safe-merge guard completion still waits for its merge, and host-parity work keeps its prerequisites. Removing that work from 3.6.0 needs a separate release-scope decision.

➡️ **The admiral recommends design only, to establish a feasible approach and cost before committing to implementation.**

This would aim to preserve the approved relief. It would not authorize production changes or another full gate campaign. No answer is recorded.

🤖 **AFK, conditional: the admiral contracts the route you select.**

For design, the admiral must define the deliverable and select the captain and author. For reduced relief, it must first record the changed acceptance. Without a ruling, the hold remains. The unapplied protected-root documentation patch stays user-owned.

### Statechart runner

#### Purpose

🎯 **Show formal-model failures as an actual state graph and shortest counterexample paths.**

You requested the upstream statechart skill for 3.6.0, including its report and rendered chart. It runs TLC to check a TLA+ model. Its launcher assumed POSIX, the Unix-style shell and process behavior used on Linux. Windows needed a different launcher and process cleanup.

Dydo added a PowerShell launcher for supported Node and cleanup for Windows worker/Java descendants. The acceptance choices concern this maintained runner, not a model you might later give it.

The work also grew beyond platform adaptation. Review required code measurements, and the captain commissioned refactoring to reduce complexity. Extracting output validation, worker launch and publication reduced the entry's score from 37 to 18. Another change simplified a verdict classifier. The record explains why those changes were commissioned. It does not prove they were the only design or separately approved harmonization.

#### Recently completed

✅ **Windows/Linux functional proof and TypeScript measurements are complete.**

The six maintained modules under `skills/engineering/statechart/scripts/` are `frontends/dot.ts`, `run.ts`, `statechart.ts`, `worker.ts`, `windows.ts` and `windows-cleanup.ts`. They meet line coverage ≥80%, branch coverage ≥60%, cognitive complexity ≤20 and HCRAP ≤20. HCRAP combines complexity and untested code into a risk score. Four scenario sensitivity/restoration pairs are also complete.

✅ **The embedded C# adapter has exact-source proof. It is no longer waiting for instrumentation.**

The separately compiled `StatechartJob.Attach` source is byte-identical to the embedded code. It achieved 15/15 lines, 6/8 branches, cognitive complexity 5 and HCRAP 5. Baseline and Coverlet-instrumented tests each passed 6/6 and preserved native error codes 87 and 5. The original PowerShell Add-Type lifecycle proof covers the separate integration layer. Earlier AltCover failures remain failures.

✅ **The integration/source-binding correction is complete, and the admiral prepared the acceptance explanation.**

Candidate `ce3ecdb0` integrates master `f3e0a161`. Its retained proof names the source it covers. Master has since advanced, so this does not prove compatibility with the latest landings or overall acceptance.

#### In flight

⏸️ **No proof crew is running. Two assurance gaps hold final readiness.**

DR048 allows a missing measurement mechanism to be recorded. A later acceptance amendment still requires an explicit decision for this delivery. The existing three-member tribunal permitted runnable proof/review and preparation of the choice. It kept readiness blocked and waived neither gap.

⏸️ **The two PowerShell scripts still lack control-flow measurements.**

`statechart.ps1` selects and starts supported Node. `windows-job.ps1` binds worker descendants to a Windows Job Object so they stop when their owning parent exits. Each lacks executable-line ≥80%, branch ≥60%, cognitive complexity ≤20 and HCRAP ≤20 measurements.

Command coverage, PSScriptAnalyzer results and the embedded C# metrics do not measure that PowerShell control flow. Cyclomatic complexity feeds HCRAP; it is not a separate ≤20 gate.

⏸️ **Tests have not been checked for sensitivity to defects across the maintained runtime.**

Changed-code mutation deliberately alters code to see whether tests detect the defects. Here it concerns the six maintained TypeScript modules, two PowerShell scripts and local native adapter. No reviewed TypeScript/PowerShell mechanism was adopted, and no C# mutation campaign ran. Unchanged vendored source is not automatically in scope.

The four scenario pairs changed expected values and showed those assertions respond. They do not establish sensitivity across the runtime. There is no mutation score or inventory of defects that survived the tests.

#### Next

❓ **Accept the two PowerShell measurement gaps, require tooling first, or keep statechart held?** ([DYD-434](https://linear.app/bodnar-balazs/issue/DYD-434))

Only you can settle the acceptance boundary set by the later amendment.

- **Record the gaps and continue delivery.** The functional proof remains useful, but unmeasured branches or complexity could hide weaknesses. The admiral owns the follow-up record and a separately contracted measurement adoption. No result becomes PASS.
- **Require measurements first.** The admiral owes a contract for executable-line/branch attribution, a PowerShell complexity model and reliable links between measurements, methods and source. Feasibility, duration and findings are unknown. There is no reliable estimate. A functional rerun or Pester upgrade alone would not supply the proof.
- **Keep statechart held.** This preserves the boundary without starting tooling. It postpones the feature. Removing it from 3.6.0 needs a separate scope decision.

➡️ **The admiral recommends recording the two scripts' gaps with an owned follow-up.**

This uses DR048's skip-and-record route without making adoption depend on new measurement machinery. It leaves the stated risks visible. No answer is recorded.

❓ **Separately, record missing mutation proof as a delivery gap or require a defined campaign first?** ([DYD-434](https://linear.app/bodnar-balazs/issue/DYD-434))

- **Record the gap.** Keep the functional, static and review evidence, with follow-up owned by the admiral. Tests could still miss defects in launch, error handling or cleanup. This avoids making delivery depend on unadopted tooling; it does not claim mutation passed.
- **Require mutation proof first.** The admiral must define the supported mutations, changed-code scope, native/lifecycle containment and treatment of mutations that do not change behavior. It also owes an acceptance criterion and estimate. No reviewed campaign or cost estimate is prepared. This is new tooling work, not a routine rerun.

➡️ **The admiral recommends accepting the absent mutation campaign as a named gap for this delivery, with the admiral owning follow-up.**

Windows/Linux functional and native lifecycle proof, TypeScript measurements, exact-source C# proof, independent reviews and corrected scenario checks support proceeding to the remaining delivery gates. They do not prove mutation effectiveness. Requiring mutation first adds an unprepared campaign with unknown feasibility and cost. This recommendation accepts a specific assurance gap; it does not claim defect-free code or a measured delay.

🤖 **AFK, conditional: the admiral records each decision and contracts the selected follow-up before delivery resumes.**

Accepting a gap needs an explicit contract amendment, the affected runtime scope, missing proof and named owner. Later scoped adoption is not yet commissioned or scheduled. Requiring tooling means preparing the measurement or mutation contract first. DR048's static skip does not waive mutation, and the earlier tribunal did not decide it. Without the required decisions, the hold remains.

Latest-master applicability, final acceptance checks, PR/CI and human merge still follow. An answer completes none of those steps.

🗓️ **HITL, later: compare upstream and local changes in the next walkthrough, after the other work.**

That conversation covers unchanged modeling guidance, packaging, Windows adaptation and complexity refactoring. It is not an immediate inspection prerequisite. No later grilling session is commissioned.

### Acting on completed captain returns

#### Purpose

🎯 **Stop completed work waiting for a watch to nudge the admiral.**

The overnight retro found delays between captain returns and the next action or fallback. It also found expensive correction cycles that still failed the strongest reviewer counterexamples. Notification loss was a hypothesis, not an established cause.

#### Recently completed

✅ **The completion and correction guidance is human-merged.** ([PR217](https://github.com/bodnarbalazs/dydo/pull/217))

The Admiral paragraph requires the next authorized action or fallback, or an owned hold with a reason. It also requires updating the project state. The Code Writer paragraph brings the strongest prior counterexample and accepted invariant to the exact successor before expensive broad gates. Independent review and final gates remain. The two-paragraph change landed at 16:32:22 UTC with three hosted checks successful.

#### In flight

⏹️ **The documentation is delivered. These receipts do not yet show how the next real handoff behaves.**

The change implements no host notification, waiting-state or approval behavior.

#### Next

🤖 **AFK: the admiral applies the completion step to the next actual return under its existing authority.**

Authorized work advances. Human-held work keeps its hold and owner. A correction author checks the strongest counterexample at the affected boundary before expensive proof. These are landed workflow duties, not a new observation campaign.

### Overnight on/off

#### Purpose

🎯 **Let an authorized agent turn overnight mode on and off through genuine Codex/T3 human approval.**

At departure, you had to return after being told goodnight and run the command yourself. The problem recurred for `dydo mode set overnight off`. Dydo's supported approval path named Claude Code. Codex/T3 could not obtain approval bound to that execution. Your authorization alone could not create the missing host support.

#### Recently completed

✅ **Early detection, an exact terminal fallback and independent state readback are merged.**

PR214 reduces surprise and prevents an unverified success announcement. At 11:01:11 UTC, the morning read-only check returned overnight off after your reported command. It proves that observed state, not an agent-run transition or working Codex approval.

✅ **The host request is prepared, but has no identified recipient or writable host checkout.**

It asks for supported human approval and temporary per-thread attachment/close behavior, with explicit lifetime and provider binding. The bounded lookup and established contacts found neither a T3 maintainer/support endpoint nor an authorized host checkout. The packet has not reached an identified owner.

#### In flight

⏸️ **Host support is blocked on a maintainer/access route. A real-UI check is not ready.**

The existing host-support captain owns delivery; the admiral owns routing. PR217 neither implements the interface nor makes host-owner discovery wait for it.

#### Next

🤖 **AFK: the admiral and existing captain need a supported host-owner/access route for the request.** ([DYD-433](https://linear.app/bodnar-balazs/issue/DYD-433))

That route is required before supported registration and an isolated approval check can proceed. The source investigation is already available. Reporting the gap requires no repeat search or new probe.

🗓️ **HITL, conditional: exercise the genuine approval UI after the captain prepares a supported, isolated check.**

The captain first owes the registration, isolated state, human interactions and cleanup procedure. These are not ready. The check must eventually observe real approval, denial without state change, and state readback. Synthetic hooks cannot prove those behaviors. Your 3.6.0 requirement remains actual host support, not fallback alone.

### Shared feature intent

#### Purpose

🎯 **Carry shared feature intent into implementation and review.**

You asked whether agents actually use working-spec references. Inspection found that readers expected an anchor, but project capture and charting could assume someone else had created it. Atomic ticket intent and an adequate reviewed plan remain valid alternatives. Not every task needs a spec.

#### Recently completed

✅ **Planning skills now explicitly choose or create the shared intent anchor.** ([PR218](https://github.com/bodnarbalazs/dydo/pull/218))

The change covers project capture, wayfinding, ticket creation and the admiral's charting step. The handoff carries the reviewed-plan alternative. Existing author/reviewer pinning rules remain.

✅ **The safe-feature-merging spec and its map/ticket links are delivered.**

`dydo/reference/working-specs/dydo-3-6-0/safe-feature-merging.md` records the existing command, guard and host-parity intent. The map and affected tickets link its sections. PR218 was human-merged at 16:29:55 UTC. Earlier safe-merge work is not retrospectively called spec-driven.

#### In flight

⏹️ **The documentation and links are delivered. Later guard/parity use remains unproved.**

The adoption receipt left the tickets' statuses and blockers unchanged.

#### Next

🤖 **AFK, conditional: future authorized guard/parity captains pin the relevant sections in author and reviewer briefs.**

Each lane keeps its existing prerequisites. A source review and a linked spec do not prove that later work used it. This landing does not restart held guard work.

### Project explanations

#### Purpose

🎯 **Help a returning reader understand the project and its choices without rebuilding ticket history.**

You rejected the earlier catch-up experience. You asked for a project account, four plain sections, icons on actual items and concrete decision explanations. The separate walkthrough lets you inspect the work and discuss whether it fits your intent.

#### Recently completed

✅ **Earlier candidates and reviews are preserved, and the new contract is clear.**

The five-section candidate is historical, not accepted wording. The admiral supplied both technical explanations above. It also authorized changing ketchup's description and matching catalog row to reflect the project scope.

#### In flight

✈️ **The existing captain and docs author are rewriting the prompt and complete example.**

Fresh reviews and a full-example reading follow the candidate. Earlier PASSs do not cover it. The walkthrough facilitator owns the human conversation; the admiral owns delivery and dispatch.

#### Next

🤖 **AFK, commissioned: finish the source and bounded proof, then return it to the captain for fresh review.** ([DYD-442](https://linear.app/bodnar-balazs/issue/DYD-442))

The return includes the complete readable example and exact source binding. The selected icons do not establish source acceptance or your wording verdict.

❗ **HITL, later: read the complete returned example in the walkthrough and give your wording verdict.** ([DYD-432](https://linear.app/bodnar-balazs/issue/DYD-432))

The facilitator will bring it after the captain's return. The verdict concerns clarity and usefulness. It does not accept the guard, waive statechart proof or approve release. Other landed changes belong in staged walkthroughs, with a final walkthrough before release approval.

## Optional provenance

- [Guard's six-fix contract](https://linear.app/bodnar-balazs/issue/DYD-406), [terminal disposition](https://linear.app/bodnar-balazs/issue/DYD-437) and [prepared guard explanation](https://linear.app/bodnar-balazs/document/guard-decision-brief-original-six-fixes-lost-relief-and-bounded-design-0fee2c0f8f92), frozen at 2026-10-30T16:39:45.276Z.
- [Statechart adoption](https://linear.app/bodnar-balazs/issue/DYD-415), [acceptance record](https://linear.app/bodnar-balazs/issue/DYD-434) and [prepared statechart explanation](https://linear.app/bodnar-balazs/document/statechart-acceptance-local-runner-proven-behavior-remaining-assurance-a2263a58d7ad), frozen at 2026-10-30T16:38:14.227Z and revised at 17:22:08.457Z.
- [Completion/counterexample guidance and human landing receipt](https://linear.app/bodnar-balazs/issue/DYD-438); [actual-host support prerequisite](https://linear.app/bodnar-balazs/issue/DYD-433).
- [Shared-intent anchor and human landing/adoption receipt](https://linear.app/bodnar-balazs/issue/DYD-443).
- [Catch-up correction](https://linear.app/bodnar-balazs/issue/DYD-442) and [staged human walkthrough](https://linear.app/bodnar-balazs/issue/DYD-432).
- Prior LC examples remain in earlier committed revisions. The original failed reply and HTML remain on [DYD-439](https://linear.app/bodnar-balazs/issue/DYD-439).

The pinned records support this reconstruction. Earlier reviews bind their earlier text. Balazs's verdict on this example remains outstanding.
