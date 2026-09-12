# DYD-164 captain packet — released after specification and review, 2026-09-11

Commission: **specification and its independent review only.** The admiral lifted DYD-164's native
blocker for that alone on 2026-09-11 05:30 local. Production on `gap_check.py`, the facade tests,
the feature files and the workflows waits for DYD-130 (DYD-96's merge) to be Done, because DYD-96
owns those paths until then. Merge DYD-166 keeps its own blocker.

Captain: issue-captain/model: requested claude-opus-5 (effective identity unavailable).

## State at release

| | |
|---|---|
| Branch | `DYD-164-single-execution`, pushed |
| Base | `c4b2f1d16f82bc28a7c342f7cfef6f8093c5bb4d` (= `origin/feature/dydo-3-consolidation` at commission) |
| Resume SHA | `0e2e0a46ae32cb890d0860f1746cb0bc99fa0a71` |
| Worktree | `C:/Users/User/Desktop/Projects/DynaDocs/.worktrees/dyd164` — removed at release |
| Status | `Todo`, unassigned, `blockedBy` DYD-130 re-wired |
| Deliverable | `dydo/agents/workspace/dyd164-portable-wip/specification.md` and `owned-paths.json` (git-ignored path, force-added) |

## Hops

| SHA | Hop | Outcome |
|---|---|---|
| `192dbf07` | specify | FAIL, round 1 |
| `8595fa46` | specify (corrective) | FAIL, round 2 |
| `0e2e0a46` | specify (fold) | released; round-2 findings folded in verbatim, no third review |

Two review rounds is the human's standing limit: after the second round the findings are folded in
as written and the Issue releases with that recorded. Both reviews were fresh, independent and
binding; neither reviewer authored the candidate it judged.

Review evidence, outside the repository, in
`C:/Users/User/AppData/Local/Temp/claude/C--Users-User-Desktop-Projects-DynaDocs/b2ab7e9b-a13d-4f9c-94e8-c50fb5a069de/scratchpad/`:
`dyd164-review-round1.md` and `dyd164-review-round2.md`. Both FAIL blocks are on the Linear record
in full.

## The specified contract, in one paragraph

Under an invocation that selects both the `test` and the `coverage` row of one stack — today only
`--force-run` — a stack whose coverage row declares where its instrumented run records the suite's
own exit launches no test process; the facade derives that test row from the coverage adapter's
already-published report. No coverage adapter changes: the verdict is already recorded at
`collectors.<c>.facts.child_exit`, with an untagged `{"gate": "functional", "child_exit": N}` finding
at `collectors.<c>.findings`. One new **optional** manifest key `suiteVerdict {exit, failure}` on
coverage rows only. No new result key: `argv: []` plus `reason` carry the derivation and are the
reviewer's mechanical one-execution check. Fail-closed: prepare-time unavailable or invalid coverage
means the test row runs plainly and no second execution ever happens; every runtime failure is test
row `invalid` 2 with a named reason; interruption is 130; an `invalid` coverage row never has its
report opened. `all`, `test`, `gate static|coverage|mutation`, `capabilities`, `--help`, public argv,
the result schema and the 0/1/2/130 meanings are unchanged. `.github/workflows/ci.yml` is an empty
hop; `.github/workflows/release.yml` gains two install steps, per F5 below.

## What the admiral must decide or absorb

1. **Ownership amendment — the one decision that gates the implement hop.**
   `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`, limited to two lines inserted after `:28`: the
   regression guard pinning the two `release.yml` install steps this Issue adds. The spec states the
   request and does not assume the grant; plan step 7 is conditional on it. If declined, the two
   install steps become a standing invariant guarded only by DYD-166's one-time merge check, and
   because the validation job is permanently red on `ubuntu-latest` (F1) a later removal of either
   step would not surface in job status. That residual is recorded under F5.
2. **F5 — the release workflow.** Post-DYD-130 the validation job installs neither the Python nor the
   Node coverage toolchain, so left alone this Issue would delete release validation's only Python
   and Node suite executions and publish a false `failed` 1 for a Node suite that never ran. The spec
   decides, inside the Owned path, to add `pip install -r DynaDocs.Tests/coverage/requirements.lock`
   and `npm ci` (`working-directory: DynaDocs.Tests/coverage`) after `release.yml:93`. The honest
   alternative, if the admiral would rather the workflow stay untouched, is to drop the `python` and
   `node` `suiteVerdict` declarations until a Linux route exists — at the cost of those two suites
   still running twice on a Windows G, which is most of what this Issue exists to remove.
   **The workflow edit and the deferral must land in the same merge, or DYD-164 must not merge.**
3. **F1 — pre-existing, not this Issue's.** The release `validation` job cannot pass on
   `ubuntu-latest` on this base: the C# campaign requires Windows and CPython 3.12.14, and
   `gate mutation` is unavailable pending DYD-103. Measured, not assumed: Actions run `34557358149`
   on `c4b2f1d1` failed at `Run coverage gate` with `Aggregate: 2`. Gate 8 is therefore a
   before/after comparison on the same candidate, not a green job. Someone owns making that job
   passable; DYD-164 does not.
4. **F2 — description clarification.** This Issue's Outcome says a failing test leaves the coverage
   row "invalid 2 as today". Today it is `failed` 1; only a campaign that could not measure is 2. The
   spec keeps today's behaviour and states the real per-case table. The Issue text is what is wrong.
5. **F4 — a follow-up Issue.** `javascript_coverage.cjs:164` cannot distinguish a failing suite from
   c8 itself failing to start, and `gate_adapter.py:412` turns any child `1` into a `functional`
   finding. The misattribution predates DYD-164, which only makes it visible on a test row. It is not
   fixable inside DYD-164's Owned paths: the adapter clause permits a change only if the verdict
   cannot be derived from what the adapters record, and here it can — what is wrong is the adapter's
   classification of its own launch failure. Gate 8's condition 2 keeps it from being signed off as
   expected behaviour in the meantime.
6. **F3 — recorded residue.** `test_csharp_metrics.py` runs inside the .NET campaign and inside the
   `python` test row. Seconds of work, worth an Issue only if someone measures it as material.

Non-binding observation from both reviewers, DYD-96's to clear:
`DynaDocs.Tests/Features/testing-facade.feature:247` at `112ec76c` still says static and coverage are
unavailable pending DYD-96 while the manifest configures both.

## Adoptable reconciliation text for other captains

All three paragraphs are in `specification.md`, section *Reconciliations — adoptable verbatim*:

- **DYD-96** adoption spec line 21 (`--force-run` means test/static/coverage, never mutation) and
  line 152 (G-final: the exact candidate's `--force-run` returns 0) — both unchanged and unweakened;
  the paragraphs say why, in text DYD-96's captain can adopt as written.
- **DYD-103** spec gate 9 (line 496 of its on-disk spec, not 630 — citation corrected here) —
  unchanged, and DYD-103 needs no re-pin on DYD-164's account. DYD-164 touches neither the freshness
  behaviour nor the 0/1/2/130 mapping DYD-103 consumes.

## How the next captain resumes

1. Wait for DYD-130 to be Done; the blocker is wired.
2. Take the branch at the resume SHA and cut a fresh worktree from it, or rebase it onto the
   post-DYD-130 feature head.
3. Run plan step 2, **Re-pin**: diff every cited line in the spec against the post-DYD-130 head and
   record "re-pinned to `<SHA>`" on the Issue, or stop and return with any moved contract named. The
   spec is written against `origin/codex/DYD-96-assurance-adoption` = `112ec76c`, the pre-merge
   shape; the merge may move lines.
4. Carry the ownership amendment above to the admiral before plan step 7.
5. Then the ordinary chain: implementer red before green, hardener, fresh whole-change review, and
   Merge DYD-166 after DYD-130 and before DYD-131.

The spec's plan declares its empty hops. Nothing in production was touched by this commission.
