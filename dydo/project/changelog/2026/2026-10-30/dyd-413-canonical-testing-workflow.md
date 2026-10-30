---
area: project
type: changelog
date: 2026-10-30
---

# DYD-413 - Canonical feature-first testing workflow

Dydo owns the reusable testing policy; LC is its proving ground. This documentation candidate
replaces the full-changed-stack-per-hop rule with feature-first proof, retains existing assurance
and integration obligations, and supplies a portable authored testing guide for new projects.
It changes no runner, guard, threshold, machine mode or global configuration.

## Source pins and pre-author comparison

Authority: [DYD-413](https://linear.app/bodnar-balazs/issue/DYD-413), description as of
`2026-10-30T12:13:56.247Z`, and the captain's
[complete pre-author comparison and bounded gate contract](https://linear.app/bodnar-balazs/document/dyd-413-complete-source-comparison-path-pins-and-bounded-gate-contract-b9346f924276).
The captain read all 23 files of LC PR 523's merge diff against its first parent before authoring:
reviewed head `a2d394a96b296503dd2518fd5a0480d074b53ac8`, merge
`2a8e440642d98f5f1f176a97b6a205b0aa007749`. The read-only LC source checkout is
`C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea`.

Later LC source `3e0c8b4351c07ee3cf3afbe5aaf71611fbf7bd4f` supplies the testing-strategy Ripple
examples, ripple-selection plan section 3.5 and DR 070's host publication/cached-consumption
contract. The writer inspected those witnesses and the canonical files; source review is not a
candidate PASS. Dydo comparison base is `f3e0a161886621395f2dba47b689fba19f3bf3de`.

The complete captain comparison below was accepted before edits. "Missing" describes that base,
not a claim that the LC policy was already canonical.

| LC merge-diff file | Classification at Dydo base | Disposition and canonical witness |
|---|---|---|
| `.ci/ripple-impact/LC-1374-feature-group-gates.json` | Project-local | LC impact closure and feature IDs; no import. |
| `AGENTS.md` | Mixed: missing policy / local queue | Feature-first guidance goes in shared skills and testing guide. LC slot-free Light and Windows queue stay local; no Dydo entry-point edit. |
| `dydo.json` | Project-local | LC nudges; enforcement changes excluded. |
| `dydo/guides/frontend/coding-standards.md` | Project-local | LC frontend commands stay local; preserve applicable generic static assurance. |
| `dydo/guides/testing-strategy.md` | Missing selection/scheduling; covered facade | Add reach, levels, prerequisites, failure attribution and Ripple intent. Preserve faithful argv, unavailable capabilities, source identity, isolation and cleanup. |
| `dydo/guides/working-tree-contract.md` | Missing | Replace per-hop full stacks with feature proof and final Heavy/coverage placement; preserve integration/release gates and host constraints. |
| `dydo/project/decisions/051-test-cost-levels-and-heavy-run-governance.md` | Project-local authority / reusable outcome | LC amendment is evidence, not a transplanted Dydo DR. Current user instruction authorizes adoption. |
| `dydo/reference/api-client.md` | Project-local | LC route manifest and development-tools contracts; no import. |
| `dydo/reference/linear-workspace-standard.md` | Missing | Update Exact gates in canonical and Scaffold copies. |
| `dydo/reference/testing/coverage-tools.md` | Mixed | Final producer/consumer placement is reusable. LC Heavy-only sources, PENDING and exits stay local; LC-1240 Q1 remains unanswered. |
| `dydo/reference/testing/test-metadata.md` | Project-local | Keep registry commands local; adopt generic discovery/selection guidance. |
| `dydo/reference/tools/agent-permissions.md` | Project-local / prerequisite lesson | Keep LC paths/tests; require checkout-local dependency preflight. |
| `dydo/reference/tools/agent-sync.md` | Project-local | Heavy routes and Bash/Python fixtures stay LC. |
| `dydo/reference/tools/tools.md` | Project-local | LC command inventory is not Dydo runtime support. |
| `skills/engineering/verify-lc/SKILL.md` | Project-local | Keep control-lc routes/runtime/browser in LC; preserve Dydo source identity/isolation and leave DYD-415 catalogs untouched. |
| `skills/productivity/to-ticket/SKILL.md` | Missing | Pin project commands, reached features, levels, final gates and reuse. |
| `skills/roles/crew/code-writer/SKILL.md` | Missing | Replace full-suite Prove and automatic blame with feature proof and evidenced attribution. |
| `skills/roles/crew/docs-writer/SKILL.md` | Missing stale reference | Bounded docs proof and project-declared gate handoff. |
| `skills/roles/crew/reviewer/SKILL.md` | Missing | Judge reach and exact-candidate records; preserve independent whole-change review and docs applicability. |
| `skills/roles/crew/reviewer/resources/code.md` | Missing | Update placement; retain DR 048 static/coverage and changed-code mutation obligations, not LC's profile-only mutation limit. |
| `skills/roles/officers/admiral/SKILL.md` | Partly covered / missing disposition | Preserve review/gates; add inherited-failure owner routing only, outside DYD-438. |
| `skills/roles/officers/ticket-captain/SKILL.md` | Missing | Direct feature schedule and failure disposition; preserve captain proof rerun, CI/review and human merge. |
| `tools/testing/coverage/tests/gate/test_periodic_suite_guard.py` | Project-local | LC guard-message regression; no runtime/enforcement extraction. |

Additional pinned repairs: maintain-verification-skill's Proof selection wording; the two existing
Scaffold contract counterparts; the new portable Scaffold testing guide; this dated record.
Catalogs, navigation, runtime tooling and DYD-438 proposals are outside this candidate.

## Outcome and release split

**3.6.0 guidance:** the testing guide owns feature/subfeature/parent-shared reach, affected-feature
Light per committed hop, final own-feature Heavy where declared, changed-product-stack coverage
producer/consumer once per final candidate, preflight, host constraints and honest failure
disposition. Roles point to it at their decision points. Existing active gates need explicit
contract reconciliation; this change cancels none retrospectively. DR 048, changed-code mutation,
independent review and mandatory integration/release obligations retain their strength.

Ripple guidance preserves marginal verified collateral catches, interpretable opportunities and
marginal runtime; duplicate detectors, flakes and mere failure frequency earn no extra credit.
Full CI supplies broad observations. Version/source receipts, visible cache fallback and truthful
zero protection are portable requirements, not a claim that a host service ships here.

**Later machinery, when ready:** reusable runner selection, Ripple and CI-worker tooling follow
LC proof and separate delivery, with no fixed release promise. Dydo's present facade has no
`--feature`, `--plan`, `--heavy` or Ripple support.
LC feature catalogs, fixtures, scoring constants, deployment and queue exemptions remain local.
LC-1240 Q1 is unanswered; no numeric pending exit is invented. DYD-406/437 terminal holds and
DYD-434 acceptance decisions remain untouched.

## Original candidate validation and evidence

Custody before authoring: clean T3-bound checkout
`C:/Users/User/.t3/worktrees/DynaDocs/t3-e1e2e01a`, branch `t3/canonical-testing-workflow`, HEAD
exactly the comparison base above, base ancestor verified, and all 15 intended files explicitly
pinned. Baseline `dydo check`: exit 0, 846 files, zero errors/warnings.

The captain pinned this recipe before authoring, from that checkout in PowerShell:

```powershell
git status --short
git rev-parse HEAD
git merge-base --is-ancestor f3e0a161886621395f2dba47b689fba19f3bf3de HEAD
dydo fix dydo/project/changelog/2026/2026-10-30/dyd-413-canonical-testing-workflow.md
dydo fix Scaffold/dydo/guides/testing-strategy.md
dydo check
dydo check Scaffold/dydo
node --test DynaDocs.Tests/coverage/tests/setup-skills.test.cjs
node setup-skills.mjs
node -e 'const fs=require("node:fs"),p=require("node:path"); const files=fs.readdirSync("skills",{recursive:true}).filter(x=>p.basename(x)==="SKILL.md"); for(const f of files){const src=p.resolve("skills",p.dirname(f));for(const host of [".claude",".agents"]){const dst=p.resolve(host,"skills",p.basename(src));if(fs.realpathSync(dst)!==fs.realpathSync(src)||!fs.readFileSync(p.join(dst,"SKILL.md")).equals(fs.readFileSync(p.join(src,"SKILL.md"))))throw Error(dst);}} console.log(files.length+" canonical skills resolve with identical bytes in both host roots");'
python DynaDocs.Tests/coverage/sync_testing_example.py --check
git diff --check
git diff --name-only f3e0a161886621395f2dba47b689fba19f3bf3de HEAD
rg -n 'full suite|full suites|fast suite|CI runs|changed stack|Feature gate|Light|Heavy|Ripple' skills/roles skills/productivity/to-ticket skills/engineering/maintain-verification-skill dydo/guides/working-tree-contract.md dydo/guides/testing-strategy.md dydo/reference/linear-workspace-standard.md Scaffold/dydo
```

Run `python C:/Users/User/.codex/skills/.system/skill-creator/scripts/quick_validate.py` on each
changed skill directory: code-writer, docs-writer, reviewer, ticket-captain, admiral, to-ticket and
maintain-verification-skill, at their canonical paths. Missing dependencies are named limitations,
not passes. Inspect the full diff for semantics, reference resolution and unchanged assurance
contracts. No application full suite, Gherkin gate, native canary, LC probe or mode toggle applies.

Raw writer output and command/candidate/environment/exit manifests are retained outside tracked
production at `C:/Users/User/AppData/Local/Temp/dyd-413-writer-evidence`; manifests bind authored
file bytes by SHA-256. The writer return supplies the committed candidate and actual check results.
The captain retains those artifacts durably and reruns the bounded recipe as proof of record.
Fresh independent Astra/high and 6.1-sol/high whole-change reviews remain captain-owned, at most
two concurrent crew. Writer delivery is IMPLEMENTED, never independent PASS.

Observed validation uses Windows, dydo 3.5.1, Node 22.23.2 and Python 3.13.1. Main documentation
checks return 0 with 847 files and no warnings. Setup tests pass 24/24 after an authorized rerun
outside the sandbox's junction restriction; local setup creates 80 projections for 40 skills,
and the exact realpath/byte check passes. The runner example consistency check also returns 0.

Two validation qualifications remain visible:

- `dydo check Scaffold/dydo` returns 2: "Path is outside the docs tree". `--root Scaffold`
  resolves back to the enclosing project, so it is not Scaffold proof. The safe replacement is
  a byte-verified copy of `Scaffold/dydo` to the evidence directory's
  `scaffold-validation/docs`, then `dydo check` from `scaffold-validation`. That checks 24
  Markdown files, exit 0, zero errors, with two unchanged starter-document warnings for About
  and Architecture. `scaffold_check.py` and `scaffold-copy-sha256.json` retain the exact recipe
  and copy receipt. It invokes no init, writes no settings and creates no Git worktree.
- Six skill validators return 0. Admiral returns 1 because the generic validator rejects
  `disable-model-invocation`, an existing key explicitly supported by canonical skill-mechanics.
  The base Admiral reproduces the same failure and its frontmatter is unchanged. Preserve the
  explicit-only invocation contract; the captain owns disposition of this validator limitation.
  No dependency was missing or installed, and no validator success is claimed for Admiral.

## Distribution and adoption handoff

`node setup-skills.mjs` is the sole local projection producer: it creates symlinks/junctions from
canonical `skills/` into `.claude/skills` and `.agents/skills`; it never reconciles prose.
The portable `Scaffold/dydo/guides/testing-strategy.md` is authored input. `DynaDocs.csproj`
embeds `Scaffold/**`; `Services/ScaffoldTree.cs` writes absent files and preserves existing
project files. There is no generic documentation sync producer.

After human merge/release, downstream owners use `update-dydo` in a user-owned top-level session:
prove each file's upstream base, three-way reconcile local adaptations, follow release upgrade
steps, perform supported checkout-local activation (`dydo init <host> --join`, then
`node setup-skills.mjs`), and validate host links and `dydo check`. Record adopted commit/version,
local command mapping, retained adaptations and validation receipts; an unknown base requires
manual reconciliation. LC and every other project remain pending until their own receipts exist.
This ticket overwrites no downstream file and changes no host settings.

The captain hands the reviewed PR and evidence, the 3.6 guidance/later machinery split, and pending
adoption receipts to [DYD-432 preparation](https://linear.app/bodnar-balazs/issue/DYD-432).
That handoff starts no walkthrough before the other agreed work is complete.

## Daytime successor correction

The current authority is DYD-413's description as of `2026-10-30T12:48:50.829Z`, including the
four-path successor pin. The original writer continues from clean
`6f68171d60a273182b6030e621936d6cd4d0f75e`; that commit, its evidence and both round-1 reviews
remain historical. The [combined round-1 FAIL](https://linear.app/bodnar-balazs/document/dyd-413-independent-round-1-6f68171-combined-fail-5fcbb4554ea6)
binds the original candidate: Astra's B1 blocker and S1 should are not overridden by Sol's PASS.

B1 is corrected in both testing guides: required own-feature executions count toward neither
Ripple detection credit nor the eligible-opportunity denominator/window. Repeating own-feature
passes cannot dilute or age out a collateral catch; eligible outside-feature observations can
affect the window. Comparable versions, interpretable results, unknown observations, attribution
and marginal runtime remain required. Flaky/unexplained failures require a diagnosis and repair
owner, not acceptance as a final state.

S1, tracked in [DYD-441](https://linear.app/bodnar-balazs/issue/DYD-441), is serially corrected in
the code-review rubric: HCRAP comes from applicable coverage evidence at its declared stage.
Ordinary static hop checks, thresholds and final assurance remain; no per-hop coverage campaign
is added. The should stays open until independent verification of the successor. Neither review
severity is relabelled. Fixed future-release wording is superseded by later delivery when ready;
the original comparison and proof artifacts retain their historical wording.

Successor proof uses the original bounded recipe with helpers copied to
`C:/Users/User/AppData/Local/Temp/dyd-413-writer-fix-evidence` so old snapshots and receipts are
not overwritten. Recheck documentation, standalone Scaffold, setup tests, the sole projection
producer and exact host resolution/bytes, runner consistency, whitespace, scope and semantic
closure on the committed successor. Compare root entry files byte-for-byte with the original
candidate and reuse skill-validator receipts only after proving unchanged `SKILL.md` bytes.
The inherited Admiral rejection remains a checker limitation, never a PASS. The successor return
retains exact commands, environment, exits, raw artifacts and the captain's replay recipe. Fresh
independent review applies to the successor and corrected contract, not another old-candidate run.

## Corrected source-only ownership and adoption

The captain's [corrected adoption and permission-wizard assessment](https://linear.app/bodnar-balazs/document/dyd-413-corrected-adoption-and-permission-wizard-ownership-assessment-28c18839576f)
supplies these conclusions; this fix performs no new LC diagnosis or tooling execution.

- [LC-1389](https://linear.app/bodnar-balazs/issue/LC-1389) exclusively owns removal of LC's
  testing-specific root section, test-TEMP paragraph and coverage-producer sentence. Dydo's
  `AGENTS.md` and `Scaffold/entry-point.md` remain byte-unchanged generic funnels with no testing
  policy, command or replacement pointer. Distribution must preserve that boundary; reusable
  testing prerequisites remain in scoped roles/docs, not always-loaded root instructions.
- [LC-1390](https://linear.app/bodnar-balazs/issue/LC-1390) is Backlog and uncommissioned. At the
  pinned LC source, Light feature runs bypass suite/Heavy admission and share checkout build
  outputs without checkout-specific exclusion. This establishes a missing exclusion guarantee,
  not a reproduced collision or attribution of historical failures. A per-worktree queue is a
  later design candidate, distinct from host-resource admission. Preserve coverage's dirty-source
  snapshot and checkout-local dependency provisioning; no isolation removal or overhead timing
  is justified by this source-only finding.
- The permission wizard mixes reusable backup, diff, verification, per-file rollback and
  hook-trust mechanics with LC identity, commands, paths, rules and hook hashes. Recommended
  future ownership is a reusable Dydo mechanism with project declarations supplying commands,
  paths and policy. This is an adoption recommendation, not proven portable support, a tooling
  port, a permission grant, a trust change or authority to execute on a host.
  [LC-1378](https://linear.app/bodnar-balazs/issue/LC-1378)'s POSIX proof/final-review holds remain;
  the wizard does not resolve [DYD-433](https://linear.app/bodnar-balazs/issue/DYD-433)'s actual-host
  approval prerequisite.

The 3.6 usage/prerequisite scope, supported adoption route above and pending downstream receipts
stand. All other human holds and LC-1240's unanswered exit-code choice remain. No queue or runner
implementation, permissions, hook trust, host state, root guidance or global configuration changes
are authorized by this documentation correction.

## Related

- [Testing strategy](../../../../guides/testing-strategy.md) - canonical policy and current facade.
- [Working-tree contract](../../../../guides/working-tree-contract.md) - delivery and evidence custody.
- [Linear workspace standard](../../../../reference/linear-workspace-standard.md) - exact gate contracts.
