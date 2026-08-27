# Reviewing Tests

Target: test changes delivered by a Linear Issue — new suites, coverage work, or the test side of an
implementation contract.

## Method

1. **Resolve the reviewed intent** — read the Issue, its governing commit, and any linked Project plan.
   Identify the behaviors and risks the tests must prove.
2. **Judge the assertions, not the count** — a test passes review only if it would FAIL when the behavior
   it names breaks. Mentally invert the implementation; if the test still passes, it is a finding.
3. **Check what is not tested** — error paths, boundaries, regressions, and Project seams named by the
   contract.
4. **Treat tests as code** — coding standards apply: no unnecessary abstraction, no copy-paste sprawl,
   and names that state the behavior under test.
5. **Run the Issue's exact gates independently** — record the commands and outcomes with the review
   verdict on the Linear Issue.

## Checklist

- [ ] Reviewed Issue intent and applicable Project-plan risks are covered
- [ ] Each test fails if its named behavior breaks (no assertion-free or tautological tests)
- [ ] Tests prove real behavior — a test that exists only to satisfy a coverage metric is a finding
- [ ] Error paths, boundaries, and integration seams are covered, not only happy paths
- [ ] Bug fixes carry a test that reproduces the bug
- [ ] No flakiness vectors: real waits bounded, no order dependence, isolated state
- [ ] Test names state behavior and coding standards hold
- [ ] Exact gates were rerun by the reviewer and evidence was recorded on the Issue
