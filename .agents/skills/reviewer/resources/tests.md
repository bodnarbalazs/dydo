<!-- Anti-pattern vocabulary adapted from mattpocock/skills tdd at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Reviewing Tests

Target: test changes delivered by a Linear Issue — new suites, coverage work, or the test side of an
implementation contract.

Read every test as a contract: one claim, at one named seam, that some breach would break. Judging the
contract is the work; the count of tests and the coverage number are not evidence of one.

## Method

1. **Resolve the reviewed intent.** Read the Issue, its governing commit and any linked Project plan,
   and name the behaviours, edge cases and risks these tests owe before reading a single assertion.
2. **Invert every test.** Break the behaviour a test names — in your head, or in the working tree when
   the doubt is real — and ask whether this test goes red. A test with no breach that would fail it is
   a finding, however green it runs.
3. **Name the seam.** A seam is the public boundary where behaviour is observable without reaching
   inside, and assertions belong on what a caller observes there. A test reaching past it pins today's
   internals and breaks on tomorrow's refactor while proving nothing.
4. **Hunt what is unclaimed.** Error paths, boundaries, regressions and the seams the contract names
   earn the same scrutiny as the happy path. A bug fix without a test that reproduces the bug FAILs.
5. **Rerun the Issue's exact gates yourself.** Record the commands and results verbatim, and
   investigate an unexpected failure until you can name its cause; a gate you did not run is not
   evidence.

## Judgement calls

Each pattern below is a finding wherever it appears, and one finding decides the verdict.

- **Tautological.** The expected value comes from an independent source of truth: a known-good literal,
  a worked example, the spec. A test that recomputes it the way the code does, restates its own setup,
  or asserts nothing at all passes by construction and proves only that the code ran.
- **Horizontal slicing.** Tests arrive one at a time, each answering what the last one taught, and each
  has failed once for its own reason. A batch written ahead of the behaviour pins an imagined shape;
  the tell is a suite where no test has ever been red.
- **Mocks past the boundary.** Mocks belong at system boundaries — network, clock, filesystem, external
  process. A mocked collaborator inside the unit under test freezes the design rather than the
  behaviour, and survives the breach it claims to catch.
- **Frozen prose.** Tests over an agent-facing document prove structure, invocation metadata and role
  boundaries. An assertion on its wording turns a sentence into an API; wording is reviewed, never
  asserted.
- **Coverage theatre.** A test whose job is to move a metric, claiming nothing a caller relies on.
- **Flaky by construction.** Waits bounded, no order dependence, isolated state; anything else is a
  finding now rather than an intermittent failure later.

## Checklist

- [ ] Every behaviour, edge case and risk the reviewed intent names is claimed by some test
- [ ] Each test fails under a breach you can state, at a seam you can name
- [ ] Error paths, boundaries and regressions are covered, not only happy paths
- [ ] A bug fix carries a test that reproduces the bug
- [ ] Every judgement call above was applied to every test in the diff
- [ ] Tests read as code a maintainer keeps: names state scenario and expectation, no unnecessary
      abstraction, no copy-paste sprawl
- [ ] The Issue's exact gates were rerun here and their results recorded

Carry the verdict and every finding in the review block.
