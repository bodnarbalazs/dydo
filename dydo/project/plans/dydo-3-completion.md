---
title: dydo 3.0 / Completion route after beta
status: draft
area: project
type: context
linear-project: https://linear.app/bodnar-balazs/project/dydo-30-consolidate-and-release-54b8939d748e
---

# dydo 3.0 / Completion route after beta

This dated successor map starts from the reviewed and locally installed `3.0.0-beta.1` source at
`a4916c9140e70f8c7ddb1dec0df3ba7cdf9cbc2f`. It preserves the accepted destination and evidence in
[the consolidation plan](./dydo-3-consolidation.md), while replacing its obsolete bootstrap order
with the shortest credible route from a usable beta to a reviewed 3.0.0 candidate.

## 1. Specification

### Intent

Finish one coherent system that can update its local authored templates, compile the enabled skills
for Claude and Codex, guide agents through the same work protocol, and expose one memorable project
testing command. DynaDocs must use that system itself and must satisfy the accepted static and
mutation policy before it calls a stable candidate ready.

### In scope

- **Local template ownership:** DYD-111's local copies, discovery, switchboard, update semantics,
  extensions and exact cleanup of disabled compiler-owned outputs.
- **Simpler documentation graph:** DYD-112's retirement of compulsory per-folder `_index.md` hubs
  while retaining the root entry point, authored navigation, broken-link checks and nudges.
- **Testing entry point:** DYD-113's project-local Swiss army knife and portable ASP.NET,
  React/Vite and Python/uv example, launched through DYD-117's thin `dydo gap-check` command.
- **Assurance:** an amended DYD-96 using the simplest credible DR-048 measurements, DYD-103's real
  changed-code mutation run, DYD-105's measurable npm entry point, and remediation of valid failures.
- **Host and onboarding integration:** DYD-86 host configuration, DYD-91 portable setup, DYD-75 final
  compiler reflection, and DYD-88's bounded observations on the generated candidate.
- **Closure:** DYD-65's confirmed audit and record delivery, any resulting Bugs, and DYD-11's stable
  package, CI, local installation, release evidence and landing preparation.

### Out of scope

Private Desktop/LC production changes remain under DYD-95. DYD-89, DYD-93 and DYD-97 remain deferred.
The CLI does not become a universal test engine, dependency solver, process scheduler, Linear client,
or arbitrary command registry. Final master landing, version tag, registry publication and the human
walkthrough remain human actions under DR 047.

### Acceptance criteria

1. A fresh project receives local shipped template sources and a generated JSON switchboard. New
   valid custom skills default to enabled; explicit choices survive update; shipped copies are
   overwritten by update; custom variants and extensions survive. Two syncs are byte-identical and
   disablement removes only exact compiler-owned outputs from both hosts.
2. Fresh init, update, check and sync do not require generated per-folder `_index.md` files. Broken
   links still fail, nudges still run, the root `dydo/index.md` remains useful, and authored
   navigation and custom files survive.
3. `dydo gap-check` resolves one structured runner command from the nearest project configuration,
   runs at the selected repository root, appends caller arguments without shell re-parsing, and
   forwards standard streams, cancellation and the child exit code. `dydo gap-check --help` is the
   project runner's useful help.
4. The project runner has distinct operations for a selected test, all tests, static gates,
   coverage gates, mutation and capabilities. Targeted tests do not silently run expensive gates.
   The shipped ASP.NET/React-Vite/Python-uv example uses visibly invalid placeholders until adapted,
   faithful argv vectors and explicit capability states. Unsupported or unavailable applicable work
   returns 2; it cannot become a green hard gate.
5. DynaDocs' actual runner uses its existing isolated .NET runner and real Python and Node test
   commands. Its final full static run covers every maintained stack and applies DR 048 exactly:
   warnings/strictness, no dead code, all tests and a test file for every non-trivial module,
   per-module line at least 80% and branch at least 60%, per-method HCRAP and cognitive complexity at
   most 20, at most seven non-constructor parameters, no supported nested ternary, no clone meeting
   both 15 lines and 100 tokens, and no dependency cycles. Genuine unavailable mechanisms are named
   as gaps; an available but unwired mechanism is unfinished work.
6. The separate mutation operation actually runs on changed maintained C#, Python and npm JavaScript
   with the project's no-surviving-or-uncovered-mutant policy. A conservative wider campaign is
   valid when exact changed-member selection is uncertain. Tool failure, malformed/missing output,
   a substantive zero-mutant campaign or an unavailable applicable stack cannot pass.
7. The accepted candidate has no valid unremediated G or M findings. Candidate/source identity, tool
   versions, commands and raw artifacts make its results reproducible. Useful retained DYD-96,
   DYD-103 and DYD-105 source and evidence are explicitly adopted or dispositioned; nothing is
   silently discarded.
8. The final authored source and emitted Claude/Codex artifacts agree, every pointer resolves, sync
   is byte-idempotent, custom host configuration survives, and the recorded host observations state
   what was configured, observed, unavailable or host-version-specific.
9. Before a later captain relies on newly integrated dydo behavior, a bounded dogfood refresh records
   the reviewed feature SHA, local package SHA-256, reported version, installed executable path and
   executable bytes; installs that exact package; then runs template update and sync in the reviewed
   dogfood workspace. A second sync is identical. Any repository output change has an explicit owner,
   review and commit; the checkpoint never leaves an unexplained generated diff. Captains already
   running in this desktop task may continue from explicit briefs pinned to current reviewed template
   source; a refresh does not claim that their in-memory generated catalog hot-reloaded.
10. The exact 3.0.0 candidate passes the full isolated tests, documentation check, full static gate,
   mutation gate, Release build, fresh installation flows, local package upgrade/rollback, native
   Windows AOT and non-publishing five-target CI validation. NuGet, npm and both CLI version forms
   agree. Validation jobs precede every publication job.
11. The confirmed Inquisition audits that exact integrated SHA, retains its record through reviewed
    delivery and turns confirmed findings into Bugs. Release-impacting Bugs are resolved before the
    landing merge receives an independent PASS. The Project completes only after the human lands it
    and an empty walkthrough closes the loop.

### Questions and answers

- **Should dydo own a test command?** Yes. The official surface is the thin `dydo gap-check`
  launcher; project-local `gap_check.py` owns testing and gate behavior. This removes path recall
  without centralizing stack logic.
- **What does `all` mean?** All configured tests for the selected stacks. Static, coverage and
  mutation remain explicit operations, so a convenient test run cannot be mistaken for final
  assurance.
- **What configuration shape is safe?** A structured argv vector, for example
  `"testing": {"runner": ["python", "DynaDocs.Tests/coverage/gap_check.py"]}`. The first item is
  the executable and the rest are fixed arguments; dydo invokes it directly, never through a shell.
- **Must the portable example run before adoption?** No. Placeholder paths remain visibly invalid
  and a configured command containing one is rejected. A project replaces them with real commands
  and records every unavailable capability.
- **Do DR 048's tests require a per-test receipt graph?** No. A complete maintained-source inventory,
  a simple source-to-test-file association and ordinary full-suite per-stack coverage satisfy the
  stated policy. Exact method metrics and stable candidate/artifact identity remain necessary.
- **Must mutation use a new semantic equivalence engine?** No. It may widen safely to a containing
  file, project or full stack. The no-survivor policy and real execution matter more than a narrow
  selector.

No unresolved human judgment blocks the first delivery wave.

## 2. Prior art

- [The accepted consolidation plan](./dydo-3-consolidation.md) at `15846dfb` fixes the destination,
  final evidence and human boundary. Its beta amendment produced the installed milestone.
- [DR 047](../decisions/047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md)
  governs admirals, captains, hop statuses, Merge Issues, audit, landing and walkthrough.
- [DR 048](../decisions/048-one-level-static-gates-certainly-wrong-no-escape-hatch.md) fixes the hard
  policy and says dydo owns rules while each project owns its runner.
- `a4916c9140e70f8c7ddb1dec0df3ba7cdf9cbc2f` is the independently reviewed beta integration. Its
  retained evidence records 2,029 tests, clean documentation validation, local beta install,
  rollback and reinstall.
- DYD-116's retained `dydo/agents/workspace/research-testing-facade-20260905.md` report distinguishes
  the working isolated .NET adapter from unavailable final G/M and corrects earlier
  executable-looking placeholder suggestions. The workspace report is evidence outside this branch;
  its conclusions are recorded here before production depends on them.
- Retained DYD-96 contains working source inventory, static collectors and Windows process-isolation
  work alongside five declared measurement gaps. DYD-103 contains an incomplete mutation adapter;
  DYD-105 contains a useful red npm-launcher test and a known exit-code contract conflict. These are
  inputs to amendments, not accepted final behavior.

## 3. Design

There are three layers, each deliberately small:

```text
dydo gap-check <forwarded argv>
        |
        v
dydo.json testing.runner argv  -- resolved from project root
        |
        v
project gap_check.py           -- tests, gates, isolation, reports
        |
        +-- stack commands     -- dotnet / Node-Vitest / uv-Python
        +-- project collectors -- static / coverage / mutation
```

`dydo gap-check` only locates configuration and launches the argv vector. It does not understand
stacks, rewrite forwarded flags or reinterpret exit codes. The runner's `--help` explains operations,
selection, defaults, examples, exit meanings and artifact locations. The runner reports every
selected independent result before choosing its aggregate exit: 0 all selected configured work
passed, 1 measured test or policy failure, 2 invalid input or unavailable/missing/malformed evidence.

DYD-113 owns the public DynaDocs `gap_check.py` facade and its portable contract. DYD-96 later plugs
reviewed collectors into that surface; its retained incompatible facade is not merged over DYD-113.
The DYD-96 amendment keeps credible inventory and collectors, replaces its receipt graph and
recursive self-measurement with ordinary complete stack coverage plus simple source-to-test-file
association, and closes the five real gaps before a G PASS. DYD-103 keeps mutation separate and may
broaden its selection whenever precision is uncertain. This simplifies implementation without
changing a threshold or turning absence into success.

The template compiler remains. DYD-111 establishes its local source and switchboard ownership;
DYD-112 then removes hub machinery; DYD-117 and DYD-86 add configuration only after DYD-111 transfers
that seam. DYD-91 teaches the resulting update, sync, host and test-adoption flow. Each source owner
may reflect the exact generated outputs its reviewed contract names. DYD-75 owns final integrated
parity and remaining reflection after all source work, rather than being the only permitted emitter.

Installation is a recurring dogfood checkpoint, not a new subsystem. Whenever a subsequent captain
needs behavior that has just merged, the admiral first takes the reviewed integration SHA through
local pack and install, records source/package/executable identity, runs `template update` and `sync`
through that installed executable, and proves repeat identity. Use a new prerelease number when the
tool manager cannot distinguish changed package bytes at the same version. Intermediate refreshes
may use a scratch consumer; using this repository's generated skills requires a clean reviewed
dogfood checkout and a named owner for every resulting diff.

The first refresh is the bounded DYD-118 immediately after DYD-111: one beta.2 metadata
change plus the smallest parameterization of the existing beta acceptance script needed to package
and install an exact reviewed feature SHA. Later refreshes reuse that established checkpoint and do
not grow a new installer framework or require a whole Feature for every local reinstall.

Every primary source Issue merges into `feature/dydo-3-consolidation` through its own Merge
Sub-issue and fresh merge review. The feature stays the only integration line. Retained dirty
worktrees are read as evidence and harvested through explicit new hops; they are never rebased,
cleaned or deleted merely to make the map look tidy.

## 4. Implementation Issue map

### In-flight baseline

DYD-111 continues on `codex/DYD-111-switchboard` from governing feature source
`a4916c9140e70f8c7ddb1dec0df3ba7cdf9cbc2f`. Its outcome, ownership and gates remain the current
Linear contract and `DynaDocs.Tests/Features/template-switchboard.feature`; this durable map does not
mirror its volatile hop status or captain state. DYD-112, DYD-117 and DYD-118 stay blocked until a
corrected DYD-111 specification passes independent review and the resulting implementation receives
its reviewed merge into `feature/dydo-3-consolidation`.

### First pickable Issue

DYD-113 is the presently commissionable tracer: it is `Todo`, its DYD-110 and DYD-116 blockers are
complete, and it owns no DYD-111 worktree or template/configuration source.

| Issue | Type / Mode / status | Outcome | Owned paths | Blockers | Gate | Base branch |
|---|---|---|---|---|---|---|
| DYD-113 | Feature / AFK / Todo | Stable project-local runner grammar, useful help, an honest initial portable three-stack example and a real DynaDocs test/capabilities adapter. | `DynaDocs.Tests/coverage/gap_check.py`, its new capability/example data and focused tests; `dydo/guides/testing-strategy.md`, `dydo/reference/coverage-tools.md` and corresponding shipped policy/example sources. It may call but does not replace `run_tests.py`; retained DYD-96 collectors stay owned by DYD-96. | Completed DYD-110 and DYD-116. | Contract tests for operations/help/selection/exit 0/1/2/placeholders; actual isolated .NET plus Python/Node test invocations; full suite, build and docs check. An initial unavailable hard-gate row is honest but does not satisfy final example adoption. | Fresh branch from `a4916c91`; merge after DYD-111 unless its merge review proves no shared path. |

### Contracted immediate successors

These records are `Todo` but not pickable while their native DYD-111 blocker remains open.

| Issue | Type / Mode / status | Outcome | Owned paths | Blockers | Gate | Base branch |
|---|---|---|---|---|---|---|
| DYD-118 | Feature / AFK / Todo | Install a beta.2 built from the reviewed DYD-111 integration, then update and sync so subsequent dogfood uses its template behavior. | Version metadata in `Program.cs`, `DynaDocs.csproj` and `npm/package.json`; minimal parameterization of `DynaDocs.Tests/Acceptance/RunExperimentalBeta.ps1`; only exact reflected outputs explicitly transferred by DYD-111. | Corrected DYD-111 SPEC PASS and independently reviewed merge. | Source/package/executable identity, local install/rollback/reinstall, template update, two identical syncs, clean or explicitly owned generated diff. | Feature head after DYD-111. |
| DYD-112 | Feature / AFK / Todo | Compulsory per-folder hubs, generation and validation disappear while links and authored navigation remain sound. | `Rules/HubFilesRule.cs`, `Rules/FolderMetaFilesRule.cs`, `Rules/OrphanDocsRule.cs` only as its spec justifies; `Services/HubGenerator.cs`, hub parts of `FolderScaffolder`/`TemplateGenerator`, `Commands/CheckDocValidator.cs`, `FixCommand.cs`, `IndexCommand.cs`, exact generated hubs, incoming links and focused tests/docs. | Corrected DYD-111 SPEC PASS and independently reviewed merge transfer the compiler/template paths. | Plain valid docs without hubs pass; broken links fail; init/fix/index do not recreate hubs; custom navigation survives; full suite/build/check pass. | Feature head after DYD-111. |
| DYD-117 | Feature / AFK / Todo | Thin official launcher resolves `testing.runner`, forwards argv/help/streams/cancellation/exit, and works from nested directories. | New `Commands/GapCheckCommand.cs`; `Program.cs`; `Models/DydoConfig.cs`; `Serialization/DydoJsonContext.cs`; `Services/ConfigService.cs` only if required; command/config references, completion/help seams and narrowly named tests. No runner or stack implementation. | Corrected DYD-111 SPEC PASS and independently reviewed merge transfer the configuration seams. Coordinate docs with DYD-113. | Real-process argv/cwd/stream/exit/cancellation/help tests, missing/malformed config errors, full suite/build/docs check. | Feature head after DYD-111. |

### Later bearings

1. **Amend and resume assurance.** After DYD-113 fixes the facade, commission a fresh spec amendment
   on DYD-96 against the new public surface. Preserve and assess every retained collector; remove
   receipt-graph, recursive-coverage and semantic-selection obligations that do not follow from
   DR 048. Land a complete failure inventory, then bounded remediation, with full G returning 0.
2. **Make the npm entry measurable.** Correct DYD-105's pre-existing Windows invalid-executable exit
   expectation before production. Preserve the red launcher cases and make the smallest product
   change needed for Node coverage/mutation without changing the public `dydo` command.
3. **Run mutation for real.** Re-spec DYD-103 after the source inventory interface is stable. Adopt
   compatible retained adapters, widen uncertain selection conservatively, and require real C#,
   Python and JavaScript campaigns with no surviving or uncovered changed-code mutant.
4. **Finish setup surfaces.** After DYD-112 and DYD-117, DYD-86 owns idempotent host configuration
   and preservation. After DYD-96 and DYD-103 supply the real gate commands, DYD-91 owns the final
   adoption pass over the portable example and ships the local-template, switchboard,
   `dydo gap-check --help`, portable-adoption and host-setup checklist. It verifies that each
   applicable example row has a faithful command and evidence contract or a genuinely unavailable
   mechanism. No guide may call an unavailable DynaDocs gate green; the initial DYD-113 delivery
   alone does not close this final-example acceptance.
5. **Reflect and observe.** DYD-75 regenerates the final managed files and proves source/output and
   installed-tool parity. DYD-88 runs its bounded Codex lifecycle canaries on that generated
   candidate and corrects claims to the observations; unsupported host behavior uses DR 047's fresh
   commission fallback.
6. **Refresh the dogfood tool.** DYD-118 performs the first refresh immediately after DYD-111.
   Package and install that exact reviewed feature SHA, record package and executable hashes, and run
   template update and sync. Captains in the current desktop task may continue with explicit briefs
   pinned to current reviewed Templates; no checkpoint claims hot reload. Repeat the existing gate at
   later coherent milestones when a captain needs newer CLI behavior, and do not rely on
   `dydo --version` alone. The fresh generated-role native canary remains DYD-88 work.
7. **Audit and fix.** Wire DYD-65 to wait for all source, G and M deliveries, pin the integrated SHA,
   run the confirmed Inquisition, deliver its record through an ordinary reviewed Feature, and
   resolve every release-impacting Bug it files. Proof branches remain reachable until adoption is
   recorded.
8. **Prepare stable delivery.** DYD-11 owns version `3.0.0`, changelog/adoption notes, release and CI
   dependency repair, five-target non-publishing evidence, local Native AOT/package/install/rollback
   proof and the independently reviewed landing PR. The human then lands, walks through, authorizes
   tag/publication, or sends findings around another bounded fix lap.

### Exact gates

Issue captains pin filters and artifact paths in their specs. Repository .NET tests always run through:

```powershell
& 'C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal
dotnet build DynaDocs.sln -c Release --warnaserror
dydo check
```

After DYD-117 and the corresponding runner capabilities land, the stable project-facing gates are:

```powershell
dydo gap-check all
dydo gap-check --force-run
dydo gap-check gate mutation --since <base-sha>
```

The first command runs all configured tests. The second is the compatibility full G gate and cannot
pass while any applicable static or coverage capability is unavailable. The third is separate M.
Final acceptance retains the raw reports and binds all three to the exact candidate SHA. DYD-11 adds
the candidate-specific fresh-init, packaging, AOT and CI commands rather than copying beta claims.

## 5. Ordering and isolation

Run at most two production Issues while keeping one independent review slot. Start or continue
DYD-111 and DYD-113. Merge DYD-111 first because it transfers template and configuration seams;
then run DYD-118 before captains rely on the new installed template behavior. DYD-112 and DYD-117
may run in parallel if their reviewed specs name disjoint files. Merge
DYD-113 before any DYD-96 facade change. DYD-86 follows the configuration owners. DYD-105 may run
beside DYD-96 after its spec correction; DYD-103 waits for the accepted inventory/mutation input.
After later reviewed CLI or template changes, repeat the established source/package/install/update/
sync checkpoint only when the next dogfood task needs that behavior.

The default feature merge order is DYD-111, DYD-118, DYD-113, DYD-112, DYD-117, DYD-86, DYD-105, DYD-96,
DYD-103, DYD-91, DYD-75, host-observation corrections, audit Bugs and DYD-11. The admiral may move a
ready independent merge ahead of a stalled predecessor only after wiring the native Merge blockers
to record the order that actually ran. Each merge receives its own captain and review; the admiral
does no Git production.

Only one owner at a time edits `dydo.json` models/serialization, compiler services, `gap_check.py`,
generated provider surfaces or release metadata. Every captain branches from the current reviewed
feature head, uses its own worktree, preserves unrelated dirty work and hands off committed evidence.

## 6. Watch-outs

- `3.0.0-beta.1` proves packaging and local rollback, not G, M, stable release or fresh-role runtime
  acceptance.
- A capabilities manifest is useful only when unavailable rows fail closed. DYD-113 is not complete
  merely because every hard gate is described as unavailable.
- Do not merge DYD-96's retained `gap_check.py` over the reviewed facade. Harvest compatible
  collectors explicitly and record the disposition of the rest.
- DR 048 does not demand a per-test receipt graph, recursive measurement of test bodies, or a novel
  semantic mutation selector. It does demand complete maintained-source accounting, real coverage,
  exact method metrics, unchanged thresholds and honest missing-data failure.
- A portable example must not contain executable-looking fake commands. Placeholder-bearing commands
  are examples and are rejected until the adopter replaces them.
- The root `dydo/index.md` is a durable entry point; generated per-folder `_index.md` files are the
  retired mechanism. Do not turn DYD-112 into broad frontmatter or document-format deletion.
- A source owner may reflect only its exact reviewed generated outputs. DYD-75 proves final aggregate
  parity and handles the remaining final reflection. Unowned regeneration can hide ownership
  violations and erase useful local evidence.
- Keep private LC changes out of this repository. Its runner is prior art only.
- No green build, clean documentation check, installed beta or audit record substitutes for the
  explicit final G, M, merge-review and human gates.

## Not yet specified

Exact remediation Issues depend on DYD-96's measured inventory, and exact audit Bugs depend on
DYD-65's proofs. The admiral creates those only from evidence and wires them before their waiters;
their absence from this map is not permission to waive a finding.
