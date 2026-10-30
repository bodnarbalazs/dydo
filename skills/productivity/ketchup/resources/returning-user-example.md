# Frozen Dydo project catch-up example

## Example

> **Frozen Dydo project example — 30 October 2026, 16:43 UTC.** This scenario is for refining ketchup. Its questions and actions are not requests to answer or act now, and grant no new permissions. It includes the statechart and guard explanations prepared at 16:38:14 and 16:39:45 UTC.

**Dydo 3.6.0 is improving the tools and shared methods agents use to work safely and keep delivery moving.** The concrete work includes fixing a shell-command guard that rejects harmless searches and writes, adding a tool that makes formal-model failures visible, and making completed delivery lead to the next authorized action.

**Two guidance changes have landed; guard and statechart acceptance remain unresolved, and genuine Codex/T3 overnight approval support is still missing.** The admiral has now prepared the guard and statechart explanations below. Those choices are unanswered. Catch-up wording is being rewritten from your feedback; neither the rewrite nor a partial walkthrough means the release is accepted.

### Agent shell-command guard — the narrow correction is held

#### Purpose

🎯 **Let agents run harmless shell searches and file writes while retaining real-command protections.**

This is Dydo's guard on agent shell commands, separate from LC TeasePlayer's checking of user JavaScript. The October 28 retro found 18 occurrences across six problems, and you asked one agent to fix those cases rather than redesign the parser:

- Quoted search/process text and literal file-write bodies were mistaken for executable commands.
- Python `pass` and quoted `grep PASS` triggered the password-store rule; plain PowerShell read assignments looked like writes; PR-command help looked like a mutation.
- Conversely, Python programs supplied on stdin could write protected files and needed to be refused.

The correction touches `Services/BashCommandAnalyzer.cs`, its shell reader and guard/nudge callers, PR-readiness parsing, paired fixtures and the protected-file documentation. Existing correct refusals must remain correct.

#### Recently completed

✅ **Round-eight review found two ways the candidate loses real launch warnings.**

- **Printed data can become downstream code.** A command that prints a literal body can feed it into a shell, which executes it. Treating the first command as a harmless data consumer hides the later execution.
- **Shell dialect changes whether text is inert.** Text shaped like a PowerShell here-string can close a quote under Bash and expose a real command; the candidate incorrectly grants it data relief.

These findings came from inert strings supplied to the hook harness, not executed payloads. Earlier green general tests missed them.

✅ **The final conditional correction attempt returned NOT IMPLEMENTED before its queued proof ran.**

Nested analysis extracts a shell's stdin or script text but loses where the enclosing command sends its output. A local “this is data” check therefore cannot establish that the text stays data downstream. No production correction or new runtime proof resulted. Eight review FAIL rounds remain; this feasibility return was not a ninth review round.

✅ **The admiral has prepared the alternatives and their lost-relief consequences.**

Earlier review had already removed single-quoted-data relaxation. You then approved finite quoted-data/comment/body relief while preserving conservative treatment of unquoted launch words and unknown wrappers. The existing tribunal allowed one conditional final attempt under that boundary. That authorization is exhausted; it is not an invitation to repeat the attempt.

#### In flight

**Nothing is running in this lane; the candidate remains held.**

The frozen correction still has both safety blockers. No new design or implementation crew is commissioned.

#### Next

❓ **HITL — should we commission a bounded design that preserves the approved body-text relief, explicitly reduce that relief, or keep the correction held?** ([DYD-437](https://linear.app/bodnar-balazs/issue/DYD-437))

I recommend **design only** first, to obtain a defensible approach and cost before committing to implementation. The old narrow commission is exhausted; a new design boundary or withdrawal of relief you approved needs your decision.

- **Preserve the intended relief through a bounded design.** Its deliverable would show how nested analysis retains the enclosing output consumer, actual shell dialect, exact source span and executable expansions, with unknown cases still counted conservatively. It must cover the harmless cases and both failure families, including nested shells. No completed design, feasibility proof or size estimate exists; a broad parser rewrite has not been shown necessary. This choice authorizes design preparation, not production changes or another full gate campaign.
- **Narrow the promised body-nudge relief.** The proposed boundary is removing the GS-03 body exemption while retaining finite quoted-search fixes and the other fixes. Harmless documentation text mentioning a long-running command, or a literal PowerShell here-string written as data, could again trigger a launch warning. The separate GS-02 file analysis might still recognize the write as literal. This gives up part of the approved experience; no implementation has proved it safe, trivial or cheaper. It needs a precise acceptance amendment and scoped correction/review.
- **Keep the candidate held.** The six-fix delivery stays unfinished. Safe-merge guard completion still waits for its actual merge, and host-parity work retains its prerequisites. Removing that work from 3.6.0 would be a separate release-scope choice.

**AFK, conditional — the admiral contracts the selected next step after your ruling.**

For design, the admiral must pin the exact deliverable and select the captain/author through normal custody; for narrowing, it must first record the changed acceptance. Without a ruling, the hold remains. The unapplied protected-root documentation patch stays user-owned; no immediate source inspection or patch application is requested here.

### Statechart — the maintained runner works, but two assurance choices remain

#### Purpose

🎯 **Make formal-model failures visible by running TLC and showing the actual state graph and shortest counterexample paths.**

You requested the upstream statechart skill for 3.6.0, including its report and rendered chart. Its POSIX launcher and process cleanup needed Windows adaptation. Dydo added a supported-Node PowerShell launcher and ownership of Windows worker/Java descendants. The acceptance questions concern this locally maintained executable runner, not a TLA+ model you might later ask it to check.

The work also evolved beyond platform adaptation: review required maintained-code measurements, and the captain commissioned complexity-driven refactoring. The entry's measured complexity fell from 37 to 18 after extracting output validation, worker launch and publication; another change simplified a verdict classifier. The record explains that gate-driven commission, but does not establish that these extractions were the only possible design or separately approved harmonization.

#### Recently completed

✅ **The adaptation has retained Windows/Linux functional proof and reviewed TypeScript measurements.**

The six maintained runtime modules under `skills/engineering/statechart/scripts/` are `frontends/dot.ts`, `run.ts`, `statechart.ts`, `worker.ts`, `windows.ts` and `windows-cleanup.ts`. Their retained measurements meet line coverage ≥80%, branch coverage ≥60%, cognitive complexity ≤20 and HCRAP ≤20. Platform campaigns cover Windows and Linux behavior; four scenario sensitivity/restoration pairs are complete.

✅ **The embedded Windows native adapter has exact-source C# proof; it is no longer waiting for instrumentation.**

The byte-identical compiled `StatechartJob.Attach` implementation achieved 15/15 lines, 6/8 branches, cognitive complexity 5 and HCRAP 5. Baseline and Coverlet-instrumented tests both passed 6/6 while preserving native error codes 87 and 5. Original PowerShell Add-Type lifecycle proof supplies the separate integration layer. Earlier AltCover failures remain failures.

✅ **The reviewed integration and source-binding correction is complete, and the admiral's acceptance explanation is prepared.**

Candidate `ce3ecdb0` integrates master `f3e0a161`; its retained proof is tied to the relevant source identities. Master has since advanced, so this is not a claim of integration with the latest landings. These receipts complete supported preparation, not overall acceptance.

#### In flight

**No statechart proof crew is running; final readiness is held on two separate assurance gaps.**

**The first gap is measurement of the two PowerShell scripts' control flow.**

`statechart.ps1` selects and starts supported Node. `windows-job.ps1` binds worker descendants to a Windows Job Object so they terminate with their owning parent. Their PowerShell control flow lacks executable-line ≥80%, branch ≥60%, cognitive ≤20 and HCRAP ≤20 measurements. Command coverage and PSScriptAnalyzer results do not supply these; the embedded C# results do not measure the surrounding PowerShell. Cyclomatic complexity is an HCRAP input, not a separate ≤20 gate.

**The second gap is test sensitivity across the maintained runtime.**

Changed-code mutation asks whether tests detect deliberately introduced defects in the six maintained TypeScript modules, two PowerShell scripts and local native adapter. No reviewed TypeScript/PowerShell mutation mechanism was adopted, and no embedded-C# mutation campaign ran. The four scenario-value pairs show those assertions respond to changed expected values; they do not establish sensitivity across the maintained runtime. There is no mutation score or surviving-mutant inventory. Unchanged vendored source is not automatically new mutation scope.

**A later acceptance amendment holds readiness despite DR048's missing-mechanism provision.**

DR048 permits recording a missing mechanism rather than building a new measurement engine. This delivery's later amendment requires explicit capability disposition. The existing three-member tribunal authorized runnable proof/review and preparation of the choice while keeping final readiness blocked; it made no acceptance decision. The admiral has prepared the explanation, but neither gap is waived. Only you can settle that acceptance boundary.

#### Next

❓ **HITL — accept recorded measurement gaps for the two PowerShell scripts, require measurement tooling before delivery, or keep statechart held?** ([DYD-434](https://linear.app/bodnar-balazs/issue/DYD-434))

- **Record the two scripts' bounded gaps and continue delivery.** Existing functional proof remains, but unmeasured branches or complexity could conceal weaknesses. The admiral owns the follow-up record and a separately contracted measurement adoption; nothing is relabeled PASS.
- **Require measurements first.** The admiral still owes a concrete contract for native line/branch attribution, the PowerShell complexity model and trustworthy source/method joins. Feasibility, duration and resulting findings are unknown; no reliable estimate exists. Rerunning functional tests or merely upgrading Pester would not supply this proof.
- **Keep statechart held.** This preserves the acceptance boundary and postpones the feature without starting tooling. Removing it from 3.6.0 would require a separate scope decision.

I recommend the narrowly named skip-and-record route with the admiral owning follow-up, rather than making adoption depend on new measurement machinery. No answer has been recorded.

❓ **HITL — separately record missing changed-code mutation as a delivery gap, or require a defined mutation campaign before delivery?** ([DYD-434](https://linear.app/bodnar-balazs/issue/DYD-434))

- **Record a separate delivery gap.** Retain the functional/static/review evidence and an explicit admiral-owned follow-up, accepting that some implementation regressions may escape the tests. This avoids coupling delivery to unadopted tooling, without claiming mutation passed.
- **Require mutation proof first.** The admiral must define operators, changed-code inclusion, native/lifecycle containment, equivalent-mutant handling and an acceptance criterion, then obtain a bounded tooling commission. No reviewed campaign or cost estimate is prepared. This is new tooling work, not a routine rerun.

My recommendation is to decide this explicitly and separately: accepting the PowerShell measurement gaps must not silently accept mutation absence. Without the required dispositions, statechart cannot be offered as ready to merge.

**AFK, conditional — the admiral records the exact disposition and contracts any selected follow-up before delivery resumes.**

If gaps are accepted, each needs its named owner and an explicit contract amendment. If tooling is required, the missing mechanism/campaign contract comes first. Latest-master applicability, final acceptance checks, PR/CI and human merge remain subsequent work, not completed consequences of an answer.

**HITL, later — the upstream/local comparison stays in the next walkthrough, after the other work.**

That conversation will distinguish unchanged modeling guidance, packaging, Windows adaptation and complexity refactoring. It is not an immediate inspection prerequisite. A possible later grilling conversation has not been commissioned.

### Completed captain returns — guidance now requires the next authorized action

#### Purpose

🎯 **Stop completed work sitting idle until a watch nudges the admiral.**

The overnight retro found completed captain results followed by delayed continuation or fallback selection. It also found repeated expensive correction cycles whose strongest reviewer counterexamples still failed. Notification loss was a hypothesis, not an established cause.

#### Recently completed

✅ **The completion and correction guidance has been human-merged.** ([PR217](https://github.com/bodnarbalazs/dydo/pull/217))

The Admiral paragraph now calls for the authorized continuation/fallback, or an owned hold with its reason, and reconciliation of current project state. The Code Writer paragraph brings the strongest prior counterexample and accepted invariant to the exact successor before expensive broad gates. Independent review and final gates remain. The reviewed two-paragraph change landed at 16:32:22 UTC with three hosted checks successful.

#### In flight

**The documentation is delivered; subsequent real handoff behavior is not established by these receipts.**

No host notification, waiting-state or approval implementation is claimed by this change.

#### Next

**AFK — the admiral applies the completion step to the next actual return, within that work's existing authority.**

Pickable authorized work advances; human-held work keeps its hold and owner. A correction author uses the strongest counterexample at the implicated boundary before expensive proof. These are the landed workflow duties, not a new delivery or observation campaign commissioned by this catch-up.

### Overnight on/off — fallback is delivered, actual host approval is missing

#### Purpose

🎯 **Let an authorized agent complete overnight activation and morning shutdown through genuine Codex/T3 human approval.**

At departure, you had to return after being told goodnight and run the command yourself. The problem recurred for `dydo mode set overnight off`. Dydo's supported approval path identified Claude Code, while Codex/T3 could not obtain the execution-bound approval. Your authorization alone did not make that host path exist.

#### Recently completed

✅ **Early detection, a precise terminal fallback and independent state readback are merged.**

PR214 reduces surprise and prevents an unverified success announcement. The morning read-only check at 11:01:11 UTC returned overnight off after your reported command. That proves the observed state, not that the agent performed the transition or that Codex approval works.

✅ **The host-side request is prepared, but has no identified recipient or writable host checkout.**

It asks for supported real-human approval and transient per-thread attachment/close behavior with explicit lifetime and provider binding. The bounded lookup and established contacts did not identify a T3 maintainer/support endpoint or authorized host checkout. The packet has not been delivered to an identified owner.

#### In flight

**Actual host support is blocked on the maintainer/access route; a runnable real-UI check is not established.**

The existing host-support captain retains delivery ownership; the admiral owns routing. PR217's workflow guidance neither implements this interface nor gates host-owner discovery.

#### Next

**AFK — the admiral and existing captain need a supported host-owner/access route for the prepared request.** ([DYD-433](https://linear.app/bodnar-balazs/issue/DYD-433))

That is the concrete prerequisite for progressing supported registration and an owned scratch-state approval check. The source investigation is already available; no repeat search or canary is required merely to report the gap. There is no request for you to approve a bypass or choose an unexplained architecture.

**HITL, conditional — a genuine approval-UI exercise follows a prepared, supported scratch canary.**

The captain would first owe the exact registration, isolated state, human interactions and cleanup procedure. None is ready to run here. Real host approval, denial/state preservation and readback must eventually be observed; synthetic hooks cannot establish them. Balazs's requirement remains actual support for 3.6.0, not fallback alone.

### Shared feature intent — the planning route and its first anchor have landed

#### Purpose

🎯 **Make shared feature intent reach implementation and review, rather than leaving working-spec references unused.**

You asked whether the spec convention is actually used. The source inspection found that readers expected an anchor, but project capture and charting could assume someone else had created one. Atomic ticket intent and an adequate reviewed plan remain valid alternatives; this is not a spec requirement for every task.

#### Recently completed

✅ **The planning skills now explicitly choose or create the appropriate shared intent anchor.** ([PR218](https://github.com/bodnarbalazs/dydo/pull/218))

The change covers project capture, wayfinding, ticket creation and the admiral's charting step. It carries the reviewed-plan alternative into the handoff, while existing author/reviewer pinning rules remain.

✅ **The safe-feature-merging spec and its map/ticket navigation are delivered.**

`dydo/reference/working-specs/dydo-3-6-0/safe-feature-merging.md` records the existing command/guard/host-parity intent. The map and affected guard/parity tickets now link its sections. PR218 was human-merged at 16:29:55 UTC. Historical safe-merge work is not retrospectively described as spec-driven.

#### In flight

**Documentation and navigation are delivered; future guard/parity consumption remains unproved.**

The adoption receipt kept those tickets' existing statuses and blockers unchanged.

#### Next

**AFK, conditional — future authorized guard/parity captains pin the relevant sections into author and reviewer briefs.**

That use follows each lane's existing prerequisites. A source-correction review and a linked spec do not themselves prove that later delivery consumed it, and this documentation landing does not restart held guard work.

### Project explanations — the catch-up rewrite is in progress

#### Purpose

🎯 **Make a returning reader understand the project and its choices without reconstructing ticket history.**

You rejected the earlier catch-up experience and clarified that it should describe the project, use four plain sections, put icons on actual items and explain concrete decisions. The separate walkthrough lets you inspect what was built and discuss its fit with your intent.

#### Recently completed

✅ **The earlier catch-up candidates and reviews were preserved, and the new contract was clarified.**

The prior five-section candidate is historical, not accepted wording. The admiral supplied the guard and statechart explanations used above and authorized synchronizing only ketchup's description and matching catalog row with its project scope.

#### In flight

✈️ **The existing captain and docs author are rewriting the canonical prompt and complete example.**

This is the current commissioned documentation work. Fresh final reviews and the exact-example reading follow the new candidate; earlier PASSs do not cover it. The walkthrough facilitator retains the human conversation, while the admiral retains delivery and dispatch.

#### Next

**AFK, commissioned — finish the source candidate and bounded proof, then obtain fresh review through the captain.** ([DYD-442](https://linear.app/bodnar-balazs/issue/DYD-442))

The return must include the complete readable example and exact source binding. It remains a review candidate until the required review and human verdict; no source acceptance follows from the selected icons.

❗ **HITL, later — read the complete returned example and give the wording/experience verdict in the walkthrough.** ([DYD-432](https://linear.app/bodnar-balazs/issue/DYD-432))

The facilitator will bring the new example when the captain returns it. This concerns whether the catch-up is clear and useful; it does not accept the guard, waive statechart proof or approve release. Additional landed changes belong in staged walkthroughs, with a final walkthrough before your release approval.

## Optional provenance

- [Guard's six-fix contract](https://linear.app/bodnar-balazs/issue/DYD-406), [terminal disposition](https://linear.app/bodnar-balazs/issue/DYD-437) and [prepared guard explanation](https://linear.app/bodnar-balazs/document/guard-decision-brief-original-six-fixes-lost-relief-and-bounded-design-0fee2c0f8f92), frozen at 2026-10-30T16:39:45.276Z.
- [Statechart adoption](https://linear.app/bodnar-balazs/issue/DYD-415), [acceptance record](https://linear.app/bodnar-balazs/issue/DYD-434) and [prepared statechart explanation](https://linear.app/bodnar-balazs/document/statechart-acceptance-local-runner-proven-behavior-remaining-assurance-a2263a58d7ad), frozen at 2026-10-30T16:38:14.227Z.
- [Completion/counterexample guidance and human landing receipt](https://linear.app/bodnar-balazs/issue/DYD-438); [actual-host support prerequisite](https://linear.app/bodnar-balazs/issue/DYD-433).
- [Shared-intent anchor and human landing/adoption receipt](https://linear.app/bodnar-balazs/issue/DYD-443).
- [Catch-up correction](https://linear.app/bodnar-balazs/issue/DYD-442) and [staged human walkthrough](https://linear.app/bodnar-balazs/issue/DYD-432).
- Prior LC examples remain in the earlier committed revisions. The original failed reply and HTML remain on [DYD-439](https://linear.app/bodnar-balazs/issue/DYD-439).

This reconstruction uses the pinned project records and prepared explanations. Earlier candidate reviews bind their earlier text; this example still requires Balazs's verdict through the walkthrough.
