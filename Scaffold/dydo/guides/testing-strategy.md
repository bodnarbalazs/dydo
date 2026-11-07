---
area: guides
type: guide
---

# Testing Strategy

This portable policy needs project-owned commands and selectors. Declare their actual support in
this guide when adopting it; a policy name alone supplies no runner capability.

## Feature proof and gate placement

Feature/subfeature tests and parent-owned shared tests are the primary proof. Select every own
and reached feature from the actual change: inspect affected consumers of shared contracts,
fixtures and configuration, not just edited paths. A selected parent includes its descendants
and ancestor-owned shared tests. Selecting a child includes that child's tests and its ancestors'
shared tests; siblings join when the change reaches them. Selection may widen, never omit reach.

Light means the project's ordinary tests; Heavy adds its declared costly tests. Cost, feature
ownership and Ripple membership are separate dimensions. The project declares cost criteria and
the commands that implement selection; a cheap test is not automatically sufficient proof.

| Stage | Required proof |
|---|---|
| While building | Surgical tests that decide the change's behavior. |
| Each committed code hop | Affected-feature Light, including reached features and shared tests, plus applicable static assurance. |
| Final candidate | Own-feature Heavy when the declared plan contains Heavy, including any reached Heavy behavior and every Heavy-only source owed to it; ticket-scoped coverage producer and consumer once, as below. Retain required static and mutation gates. |
| Documentation-only hop | Bounded documentation, setup and reference checks its contract names; runtime tests only where the change actually reaches behavior. |
| CI or explicit human request | Whole suites for broad observations; hosted CI also requires whole-stack coverage. A coverage producer may itself execute a stack suite; reuse its suite verdict where supported instead of launching the same suite twice. |

Coverage is scoped to the ticket. Its coverage producer runs the ticket's own Light feature
selection: own and reached features, with their ancestors' shared tests. Where the facade supports
it, that is the same instrumented run as the Light gate, so the suite runs once. The consumer judges
the ticket's changed files under the unchanged thresholds; a changed line the selection does not
reach fails the gate rather than being waved through. A changed Heavy-only source, one only the
project's declared Heavy tests reach, takes the Heavy route: the Light coverage gate reports it as
owed to Heavy, and the final candidate's Heavy stage measures it under the same thresholds or fails.
No gate reports an owed source as pending with exit 0; DR 048 leaves no escape hatch. A facade
without feature selection keeps whole-stack coverage per ticket and records that as its concrete
gap; the ticket claims no selector the facade lacks.

The ticket's Exact gates bind real project commands, reached features, levels, prerequisites and
stage. Use a supported selector/adapter; missing selection or Heavy support is a concrete gap to
return to the captain, not a flag to invent or proof to skip. Required integration, combined-merge
and release gates remain at their boundaries. Whole-stack coverage is required at the landing, at
every sync of the default branch into a feature branch and in hosted CI, and it measures Heavy-only
sources under the unchanged thresholds or fails. Existing active ticket gates remain binding until
their owner explicitly reconciles the contract under governing authority, naming retained,
replaced and still-required gates; new guidance never retrospectively cancels an owed run.

## Runner prerequisites and shared hosts

Before an expensive run, cheaply verify the checkout/snapshot and exact source, supported command
and selection plan, and nonzero native discovery for every intended selection. An empty selection
is not a pass. Verify the runtime/tool versions and locked dependencies in the checkout that will
execute, including each selected route's install; another worktree's packages prove nothing here.
Missing prerequisites return a named setup gap before expensive execution.

Declare the owner of processes, worktrees, temporary data and result paths before launch. Preserve
source identity across isolation or copying, use per-run artifacts where needed, and retain raw
evidence through cleanup. Cleanup must verify owned resources are gone; it must not touch another
crew's processes or shared services. Native proof uses authorized isolated scratch and reports
skips as missing proof, not passes. The adapter owns isolation and cancellation cleanup.

The session's shared FIFO, concurrency limits and host restrictions take priority over project
local scheduling exemptions, including slot-free Light. Use the supported queue and let a healthy
queued or quiet run continue. A Light label grants neither a bypass nor host access.

Memory belongs to the whole host: one runaway run can exhaust it and end every session on the
machine. Run each ad-hoc test, gate or probe under a hard cap with swap off:
`[env MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false] systemd-run --user --scope --slice=<agent slice> -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 3600 <cmd>`.
The bracketed env prefix applies to .NET commands; omit the brackets when using it. Use your
session's agent slice: the nearest `.slice` in `/proc/self/cgroup` within the user manager. On a
T3 host, `app-t3code-agents.slice` is an example; a nearer session slice takes precedence. Leave
out `--slice` where no agent slice exists. Each run has its own cap; concurrent runs share the
caller's slice budget. Give a focused probe `MemoryMax=4G` and a short timeout. Keep any host gate
lock before the command, outside the cap. Pass input that holds `$` through a script file, and
never scale an input upward uncapped. The testing facade caps its own operations the same way,
and `DYDO_MEMORY_MAX` may only lower its cap.

A cap hit is a finding to diagnose; nobody raises the cap to get past it. When the facade
survives, the hit is a `failed` row marked `memoryCapHit`. Otherwise the caller sees 137, or 143
when systemd stops a scope under its default `OOMPolicy=stop`, and the `journalctl --user` OOM line
confirms it. Behind `timeout`, a default-policy scope can even return 0; that race is why the
command sets `OOMPolicy=continue`. This ad-hoc command applies only where systemd exists.
On Windows the facade caps itself with `JOB_OBJECT_LIMIT_JOB_MEMORY` in its own nested Job object,
without restarting or killing itself or adding kill-on-close. The effective limit is the lower of
its target and the outer job's job-memory limit, when present; Windows enforces nested limits.
Its cap is `enforced`, with mechanism `windows job object`. Setup or query failure records
`unavailable` with the Windows error and one warning, and the operation proceeds.

The operation's `memoryCap` and every covered row record `memoryCapHitReporting`: `exact` on
Linux, `best-effort` on Windows. A delivered `JOB_OBJECT_MSG_JOB_MEMORY_LIMIT` on the facade's own
completion port makes that Windows row `failed`, exit 1, with `memoryCapHit: true` and
`memory cap hit (MemoryMax=<cap>): a finding; do not raise the cap`. Windows message delivery is
not guaranteed: a missing message proves nothing and never changes a failed row into a pass.
`peakJobMemory` records the job's cumulative `PeakJobMemoryUsed` at each row's end as diagnosis
only; neither a peak nor a child exit code establishes a hit. macOS and Linux without usable
systemd still record `unavailable` with a reason and warning; only deadlines bound those runs.

## Evidence and inherited failures

Each result names candidate/source identity, command and selection, environment/session, exit and
raw artifact location. Bind dirty inputs to their bytes; a HEAD label alone is not the tested tree.
Keep native output and producer/consumer receipts, including unavailable or deferred assurance.
Pending work has no invented success or numeric exit: preserve the runner's actual result.

Reuse evidence only for the exact candidate, selection and compatible environment when the gate
allows it. Final Heavy and coverage each run once per final candidate; a changed final tree
(including a fix or combined merge tree) requires fresh applicable final gates. A combined merge
tree reruns the ticket-scoped coverage gate, not whole-stack coverage, unless it is the landing or a
sync of the default branch. New concerns and
distinct mandatory integration/release boundaries also require their specified reruns. Within one
gate run, deduplicate execution where the producer's contract proves both verdicts. Independent
review and the captain's own proof-recipe rerun remain required.

A failing result stays failed. Distinguish a change-caused defect from inherited debt, a fixture
fault or runner/environment failure with exact-candidate output and comparable base/source
evidence. An assertion that it was already broken is not attribution. Return the failure and
evidence to the captain; repair change-caused defects in scope. For inherited or infrastructure
failures, the accountable owner records disposition, separately routes repair or escalation, and
states which required gates remain blocked. Neither attribution nor filing a follow-up makes the
candidate green, waives a threshold/mutation obligation, or authorizes unrelated repair work.
Flaky or unexplained failures require an accountable diagnosis and repair owner; they are not
an accepted final state.

## Ripple

Ripple supplements feature proof with cheap, high-value cross-feature detectors. Where supported,
the runner selects the deduplicated union of required feature tests and evidence-selected Ripple
members. Membership starts empty, admits only demonstrated value and excludes Heavy tests;
agents select features rather than hand-curating members. Empty or disabled Ripple gives zero
collateral protection and leaves feature proof mandatory.

Reward distinct, verified collateral product defects missed by feature proof and the other
members. Duplicate detectors of the same defect, flakes and infrastructure failures earn no
additional defect credit. Full CI supplies broad observations, including tests outside current
membership. Count opportunities only from eligible outside-feature observations: executed,
interpretable results with known change reach and comparable source/test versions. Required
own-feature executions earn neither detection credit nor a place in the opportunity denominator
or window. Repeated own-feature passes therefore cannot dilute or age out a prior collateral
catch; eligible outside-feature observations can affect the window. Missing, skipped or ambiguous
results are unknown, not passes; unresolved failures cannot establish that feature proof missed
the defect. No opportunity means no evidence of decay merely because time passed.

Selection values marginal catches per eligible opportunity relative to marginal runtime,
including added route overhead. A cost cap is a ceiling, not a target to fill. Numerical windows,
minimum samples, caps, catalogs and deployment belong to the project's proven mechanism.

When a project supports published membership, retain the actual list version/digest and source
used by each run. Bounded retrieval may use a valid compatible cache or disable Ripple, recording
the fallback reason and zero protection when disabled. Exact-version replay must report an
unavailable version rather than substitute another. This policy supplies no host service or
permission to deploy one.

## Related

- [Working-tree contract](./working-tree-contract.md) - custody, review and integration.
- [Linear workspace standard](../reference/linear-workspace-standard.md) - Exact gates and evidence.
