# Reviewing Tests

Target: test changes — new suites, coverage work, or the test side of a slice.

## Method

1. **Judge the assertions, not the count** — a test passes review only if it would FAIL when
   the behavior it names breaks. Mentally invert the code; if the test still passes, it's a
   finding.
2. **Check what's NOT tested** — error paths, boundaries, the regression the slice was for.
3. **Tests are code** — coding standards apply: no slop, no copy-paste sprawl, clear naming
   that states the behavior under test.

## Checklist

- [ ] Each test fails if its named behavior breaks (no assertion-free or tautological tests)
- [ ] Tests prove real behavior — a test that exists only to satisfy the coverage gate is a finding
- [ ] Error paths and boundaries covered, not just happy paths
- [ ] Bug fixes carry a test that reproduces the bug
- [ ] No flakiness vectors: real waits bounded, no order dependence, isolated state
- [ ] Test names state behavior; standards hold
