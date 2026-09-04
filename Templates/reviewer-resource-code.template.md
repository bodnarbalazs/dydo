<!-- Adapted from mattpocock/skills code-review and tdd at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Reviewing Code

Target: the code and tests one Issue delivered, judged against its contract and, when one governs,
its reviewed Project plan, on four axes judged alone, so a clean axis never masks a failed one.

## Method

1. **Pin the contract.** Outcome, scenarios, owned paths, base SHA, exact gates, tier, governing plan
   at its SHA. Done when you can state what the change had to do without reading it.
2. **Read the hops.** `git log <base>..<candidate>` lists the specify, implement, harden and fix
   commits. The implement hop is judged for doing what the contract says; the harden hop for
   changing only what was warranted, and everything that was. Done when you can say per hop what it
   changed and why.
3. **Read the diff, then the code it lands in.** `git diff <base>...<candidate>` gives the delta;
   read enough of each file to judge the whole. Done when every hunk is accounted for.
4. **Work the four axes below, each entire**, every item verified against the source or a finding.

## Contract

- The candidate matches the governing commit, the owned paths and the requested outcome
- Every scenario stands as the specifier committed it, and every scenario passes
- Every behaviour, edge case and risk the contract names is claimed by a scenario or a test; a bug
  fix carries the test that reproduces the bug
- Nothing the implement hop had that the contract needed was dropped by a later hop
- Nothing beyond the contract: an unrelated improvement is scope creep and a finding
- Every deviation the implementation reported is justified or raised

## Standards

- [coding-standards.md](../../../../dydo/guides/coding-standards.md) and any stack-specific standard
  bind, the anti-slop mandate included, with the `codebase-design` lens on every seam the diff
  touches; a documented standard beats your taste, and a rule the tooling enforces is closed
- The harden hop changed only what was warranted, and everything that was: smaller, simpler,
  standard or deeper, with a candidate already good left as it was; an abstraction or optimisation
  ahead of a need is a finding
- The twelve smells in the standards, each a question against the diff, the hunk quoted, a finding
  only with its concrete consequence named; every smell answered, not the first one found
- Code that was already bad is a finding when this change builds on it
- Each test is a contract: one claim, named by case and expectation, at a seam a caller observes,
  that some breach turns red; a test with no such breach is a finding however green it runs
- Shapes that pass by construction: an expected value recomputed the code's way; a mock inside the
  unit; a suite where no test was ever red; an assertion on a prompt file's wording; a metric moved
  with nothing claimed; an unbounded wait or a dependence on order

## Gates rerun

- The Issue's exact commands
- Coverage and CRAP against the tier the spec names
- Mutation on the changed files, no survivor; one example value changed per scenario, none left green
- `dydo check` when the change touches documentation or validation surfaces

## Security and likely bugs

- Every boundary the diff touches validates what crosses it, and the vulnerabilities
  coding-standards §5 names are asked against every such hunk; secrets stay out of source and logs
- Logic holds at the edges (empty, null, first, last, off-by-one), no fallback masks an impossible
  state, and each error path is handled on purpose
- Ordering, concurrency and resource lifetime, where the diff introduces them

## Verdict

Each finding in the review block carries its axis: `contract`, `standards`, `gates` or `security`.
