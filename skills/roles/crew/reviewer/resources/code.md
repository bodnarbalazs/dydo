<!-- Adapted from mattpocock/skills code-review and tdd at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Reviewing Code

Target: the code and tests one Issue delivered, judged against its contract and, when one governs,
its reviewed Project plan, on four axes judged alone, so a clean axis never masks a failed one.

## Method

1. **Pin the contract.** The Issue description as of the brief's Linear `updatedAt`: outcome,
   scenarios, owned paths, base SHA, exact gates; governing standards and plan at its SHA. Done when you can state what the change had to do without reading it.
2. **Read the hops.** `git log <base>..<candidate>` lists the `implement`, `fix` and `merge`
   commits. The implement hop is judged for doing what the contract says; a fix hop for closing the
   finding that sent it back and nothing else. Done when you can say per hop what it changed and
   why.
3. **Read the diff, then the code it lands in.** `git diff <base>...<candidate>` gives the delta;
   read enough of each file to judge the whole. Done when every hunk is accounted for.
4. **Work the four axes below, each entire**, every item verified against the source or a finding.

## Contract

- The candidate matches the pinned contract, the owned paths and the requested outcome
- Every scenario the contract calls for stands unweakened, and every scenario passes
- The `IMPLEMENTED` line the brief carries holds its red proof line: a Bug's reproduction failing
  with the fix reverted, a Feature's tests failing with the code stashed; rerun it only on a doubt
  you state
- Every behaviour, edge case and risk the contract names is claimed by a scenario or a test; a bug
  fix carries the test that reproduces the bug
- Nothing the implement hop had that the contract needed was dropped by a later hop
- Nothing beyond the contract: an unrelated improvement is scope creep and a finding
- Every deviation the implementation reported is justified or raised

## Standards

- `dydo/guides/coding-standards.md`, read from the repository root, and any stack-specific standard
  bind, the anti-slop mandate included, with the `codebase-design` lens on every seam the diff
  touches; a documented standard beats your taste, and a rule the tooling enforces is closed
- The code the candidate touched is no worse than it was found, and smaller, simpler, standard or
  deeper wherever the change warranted it; an abstraction or optimisation ahead of a need is a
  finding
- The twelve smells in the standards, each a question against the diff, the hunk quoted, a finding
  only with its concrete consequence named; every smell answered, not the first one found
- Code that was already bad is a finding when this change builds on it
- Each test is a contract: one claim, named by case and expectation, at a seam a caller observes,
  that some breach turns red; a test with no such breach is a finding however green it runs
- Shapes that pass by construction: an expected value recomputed the code's way; a mock inside the
  unit; a suite where no test was ever red; an assertion on a prompt file's wording; a metric moved
  with nothing claimed; an unbounded wait or a dependence on order

## Gates

- At a hop, the writer's run: the full suite and the static gate of every stack the change touched,
  once each, green beside the cheap checks the Issue names
- The HCRAP row of that static gate, read for every method the change touched; one over the bar is
  a finding
- The project's changed-code mutation gate, in its `--since <base>` form, run by you when the
  project has one; a surviving or uncovered mutant is a finding
- At a gate — the Issue's final gates, a merge, the landing — the whole set below on that exact
  candidate, read from its gate record or run by you:
  - The Issue's gate-scale commands and the full suites, all green
  - Coverage, HCRAP and the one-level static policy in the project's testing guide
  - Mutation on the changed files, no survivor; one example value changed per scenario, none left green
  - `dydo check` when the change touches documentation or validation surfaces

## Security and likely bugs

- Every boundary the diff touches validates what crosses it, and the vulnerabilities
  coding-standards §5 names are asked against every such hunk; secrets stay out of source and logs
- Logic holds at the edges (empty, null, first, last, off-by-one), no fallback masks an impossible
  state, and each error path is handled on purpose
- Ordering, concurrency and resource lifetime, where the diff introduces them

## Verdict

Each finding in the FAIL form carries its axis in the `wrong:` slot, as `wrong: <axis>: <fact>`, the
axis being `contract`, `standards`, `gates` or `security`.
