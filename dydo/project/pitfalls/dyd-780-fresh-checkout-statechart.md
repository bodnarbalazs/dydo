---
area: project
type: pitfall
---

# DYD-780: statechart acceptance in a fresh checkout

The scenario `Both hosts require user invocation` used to read gitignored host projections
from the checkout. On base `b6871d41f0ad3eb9bbb5182ed6de61c793fc58d5`, it fails before
checking invocation metadata when `.claude/skills` and `.agents/skills` are absent.
Running setup manually hides the defect in a developer checkout.

The scenario now copies the canonical `SKILL.md` and `agents/openai.yaml` into a temporary
canonical skill directory, invokes the real `setup-skills.mjs --root ... --skill statechart`,
and checks both generated projections with `realpathSync`. It reads both invocation controls
through each projection. A `finally` block removes the fixture on success and failure.
The Gherkin, metadata, setup implementation, thresholds and workflows are unchanged.

This supersedes the checkout-projection prerequisite for this scenario listed in
[DYD-415 verification](./dyd-415-statechart-verification.md). It does not replace that document's
other prerequisite or native-platform proof requirements.

## Observed proof

Writer: code-writer/model gpt-6-astra high via codex_proxy, Linux, 2026-11-06.
Checkout: `/home/balazs/.t3/worktrees/DynaDocs/t3-77b307d0`, branch
`DYD-780-fresh-checkout-statechart`, initially clean at the base above.
Node 26.13.0, pnpm 12.16.1, CPython 3.14.4. The captain provisioned locked pnpm dependencies
and dotnet tools. The writer installed `requirements.lock` into ignored `.static-gates/python`.
Raw logs and supporting scripts are retained in `/tmp/dyd780-writer/`.
Test source SHA-256: `77c68ea49b663a91c4a62d4669bf9c5fdeb0b26010df8277636a0c08c82a7dba`.

| Run | Exit | Evidence |
| --- | --- | --- |
| Original scenario | 1 | `original-red.log`: one failure, no skips, `ENOENT` at checkout `.claude/skills` |
| Fixed scenario | 0 | `fixed-green.log`: one pass, no skips |
| Reverted scenario | 1 | `revert-red.log`: same `ENOENT`, one failure, no skips |
| Restored scenario | 0 | `restored-green.log`: one pass, no skips |
| Injected failure after setup | 1 | `cleanup-failure.log`: projection realpath read deliberately throws |
| Cleanup after failure | 0 | `cleanup-after-failure.log`: fixture directory empty; checkout roots absent |
| Exact requested facade grammar | 2 | `force-run-grammar.log`: `Invalid request or artifact destination: invalid operation syntax` |
| Statechart coverage and suite | 0 | `coverage.log`: 455 tests, 447 passed, 0 failed, 8 existing skips; coverage PASSED |
| Statechart static | 0 | `static.log`: statechart static PASSED |

The coverage receipt is `DynaDocs.Tests/coverage/results/run-1793933302608-616e51e6/result.json`;
the static receipt is `DynaDocs.Tests/coverage/results/run-1793933683841-b323f998/result.json`.
Both run directories, adapter reports and raw assurance evidence are also preserved under
`/tmp/dyd780-writer/facade-results/`.
The admitted `gates.sh` run exited 0 and verified both checkout discovery roots remained absent.
The eight existing suite skips are seven Windows-only cases and the config-fuzz oracle,
which requires a JDK; the managed Java is a JRE. They remain missing proof for those cases,
not new skips or allowances. The targeted projection scenario passed without a skip.

The first locked grammar probe exhausted its 120-second queue timeout (exit 124) before
executing the facade. It is not a gate verdict; `gates.sh` retried it with the final gates.

## Repeatable captain proof

Run from this checkout with its provisioned dependencies. The following reproduction changes
only the owned test file temporarily and restores the candidate even if an assertion fails.
It never runs setup against the checkout. The source hash above identifies the tested bytes.

```bash
mkdir -p /tmp/dyd780-captain
cp skills/engineering/statechart/scripts/test/acceptance.test.ts /tmp/dyd780-captain/candidate.ts
trap 'cp /tmp/dyd780-captain/candidate.ts skills/engineering/statechart/scripts/test/acceptance.test.ts' EXIT
cap() {
  systemd-run --user --scope --slice=app-t3code-agents.slice \
    -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 120 "$@"
}
scenario() {
  cap node --disable-warning=ExperimentalWarning --test \
    '--test-name-pattern=^Both hosts require user invocation$' \
    skills/engineering/statechart/scripts/test/acceptance.test.ts
}
cap bash -c 'test ! -e .claude/skills && test ! -L .claude/skills && test ! -e .agents/skills && test ! -L .agents/skills' || exit 1
git show b6871d41f0ad3eb9bbb5182ed6de61c793fc58d5:skills/engineering/statechart/scripts/test/acceptance.test.ts > skills/engineering/statechart/scripts/test/acceptance.test.ts
scenario > /tmp/dyd780-captain/original-red.log 2>&1; test "$?" -eq 1 || exit 1
cp /tmp/dyd780-captain/candidate.ts skills/engineering/statechart/scripts/test/acceptance.test.ts
scenario > /tmp/dyd780-captain/fixed-green.log 2>&1; test "$?" -eq 0 || exit 1
git show b6871d41f0ad3eb9bbb5182ed6de61c793fc58d5:skills/engineering/statechart/scripts/test/acceptance.test.ts > skills/engineering/statechart/scripts/test/acceptance.test.ts
scenario > /tmp/dyd780-captain/revert-red.log 2>&1; test "$?" -eq 1 || exit 1
cp /tmp/dyd780-captain/candidate.ts skills/engineering/statechart/scripts/test/acceptance.test.ts
scenario > /tmp/dyd780-captain/restored-green.log 2>&1; test "$?" -eq 0 || exit 1
cap bash -c 'test ! -e .claude/skills && test ! -L .claude/skills && test ! -e .agents/skills && test ! -L .agents/skills' || exit 1
trap - EXIT
```

Inspect both red logs for the missing checkout projection, and both green logs for one pass,
zero failures and zero skips. Exit codes alone would not distinguish the intended reproduction
from an unrelated failure.

For the full statechart coverage producer (which also runs the entire statechart suite) and
applicable static assurance, use the same managed prerequisites as the writer. No process
environment inspection or global installation is needed:

```bash
deps=/home/balazs/.local/share/dydo/statechart/deps/3ad231072bddc04588d0f2cf1b61a3e62fc38b4ff400ccea5c3b5d41409575ab/linux-x64
export JAVA="$deps/java/content/jdk-21.0.8+9-jre/bin/java"
export TLA2TOOLS="$deps/tlc/tla2tools.jar"
export STATECHART_PELE="$deps/pele/content/package/dist/pele.js"
export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false
gate() {
  systemd-run --user --scope --slice=app-t3code-agents.slice \
    -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 1800 \
    /var/tmp/dydo-gate-lock.sh .static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py "$@"
}
gate --force-run --stack statechart
gate gate coverage --stack statechart
gate gate static --stack statechart
```

The pinned facade only accepts `--force-run` by itself. Combining it with `--stack` is
unsupported; the captain must reconcile that exact-command requirement. The two supported
commands exercise the statechart coverage/test and static adapters used by `--force-run`.
Neither an unavailable command nor an inherited skip is claimed as a pass.
The captain's post-merge full `gap_check.py --force-run`, independent reviews and DYD-622's
native Windows proof remain separate obligations.

## Related

- [Skills are the source](../decisions/049-skills-are-the-source-retire-the-compiler.md)
- [Statechart invocation](../decisions/062-statechart-invocation-manages-prerequisites.md)
- [Testing strategy](../../guides/testing-strategy.md)
