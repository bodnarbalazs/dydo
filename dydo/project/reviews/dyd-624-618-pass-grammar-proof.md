---
area: project
type: reference
---

# DYD-624 / DYD-618 shared PASS grammar proof

Contract: accept optional candidate/base colons and use the merge parser in pr-ready.
Base: `0086bc8d05900442aca84eb46793791635c70ab3` on `feature/dydo-3-6-0`.
The writer verified the clean ticket branch, isolated worktree root and base ancestry before edits.

## Behavior and preservation

Only an optional `:` was inserted after each candidate/base field name in the existing regexes.
Choosing zero occurrences yields exactly the original expressions: every previously parsed
canonical `candidate <SHA>; base <SHA>` line remains parsed, including abbreviated and
backticked SHAs, punctuation, trailing prose, mixed-case hex and all three supported dashes.
The existing parser tests and the new four-way separator matrix exercise that preservation.

Pr-ready reads section boundaries and verdicts through `IndependentReview`. It requires a PASS
with a code/docs/merge rubric and parsed candidate and base fields. Its error gives the expected
semicolon-separated form. No commit resolution, HEAD comparison of the review candidate, or
base ancestry check was added. Merge's policy is unchanged, including its existing missing-base
warning. The Gherkin scenarios exercise pr-ready's command and a pinned merge dry run against a
fake GitHub boundary; they make no claim about hosted GitHub execution.

## Observed regression proof

Evidence directory: `/var/tmp/dyd624-618-writer` on the implementation host.

- `restore.log`: committed Python, pnpm and dotnet restores plus skill projections, exit 0.
- `red.log`: focused tests before implementation, exit 1; 15 failed, 46 passed, zero skipped.
  Colon fields parsed as null; malformed PASS returned readiness exit 0 instead of 1.
- `green.log`: affected merge/pr-ready selection with implementation, exit 0;
  587 passed, zero failed or skipped, including the five generated Gherkin cases.
- `reverted-red.log`: both production files restored to the base while retaining tests,
  exit 1; 18 failed, 48 passed, zero skipped. The five Gherkin cases were discovered too.
  Restoring `fix.patch` reinstated the implementation after this observation.

The statechart JRE and TLC SHA-256 values and Pele SHA-512 integrity matched
`Resources/Statechart/dependency-lock.json`. Installed tools were Python 3.14.4,
Node 26.13.0, SDK 10.0.303 and shared .NET runtime 10.0.14.

## Captain rerun

Use this checkout with its restored locked prerequisites. Create a unique evidence directory
under `/var/tmp`, set `TMPDIR` to it, and export `MSBUILDDISABLENODEREUSE=1`,
`DOTNET_CLI_USE_MSBUILD_SERVER=0`, and `UseSharedCompilation=false`.
Do not start heavy work while `/var/tmp/dydo-heavy-hold` exists.

Run each focused command under:

```bash
systemd-run --user --scope --slice=app-t3code-agents.slice \
  -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 240 \
  .static-gates/python/bin/python DynaDocs.Tests/coverage/run_tests.py -- \
  --filter 'FullyQualifiedName~DynaDocs.Tests.Merge|FullyQualifiedName~PrReady|FullyQualifiedName~SharedIndependentReviewPASSGrammar'
```

For the red witness, save the two production files, replace them with their base versions using
`git show BASE:path`, and run the same command with the narrower filter
`FullyQualifiedName~IndependentReviewTests|FullyQualifiedName~PrReadyCommandTests|FullyQualifiedName~SharedIndependentReviewPASSGrammar`.
Expect exit 1 and the failures above; restore the saved production files, then expect exit 0.
Only those two files are reverted; canonical tests stay enabled throughout.

Final gates use the pinned facade, whose coverage adapter produces and consumes native coverage.
Set `TLA2TOOLS=/var/tmp/dyd740-statechart/tla2tools.jar`,
`JAVA=/var/tmp/dyd740-statechart/java/jdk-21.0.8+9-jre/bin/java`, and
`STATECHART_PELE=/var/tmp/dyd740-statechart/pele/package/dist/pele.js` to the verified copies.
Run each gate through the host lock outside the cap:

```bash
timeout 7200 /var/tmp/dydo-gate-lock.sh \
  systemd-run --user --scope --slice=app-t3code-agents.slice \
  -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 3600 \
  .static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py gate static --stack dotnet

timeout 7200 /var/tmp/dydo-gate-lock.sh \
  systemd-run --user --scope --slice=app-t3code-agents.slice \
  -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 3600 \
  .static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py gate coverage --stack dotnet
```

Retain both facade `result.json` receipts and `results/adapters/dotnet-{static,coverage}.json`
with their raw evidence and tested commit. Gate outcomes accompany the writer's return rather
than being asserted in this pre-gate recipe. Mutation is **UNAVAILABLE**, per DYD-774.

## Related

- [Command reference](../../reference/dydo-commands.md#dydo-pr-ready)
- [Testing strategy](../../guides/testing-strategy.md)
