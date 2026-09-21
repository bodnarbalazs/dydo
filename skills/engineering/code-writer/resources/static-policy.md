# Static policy

The bar Phase 3 measures the candidate against. Use the project's testing guide for its runner and
per-stack mechanisms.

The one-level bar is line coverage ≥80% and branch ≥60% per module; HCRAP = CC² × (1 − cov)³ +
cognitive ≤20 and cognitive ≤20 per method; at most seven parameters outside constructors; no dead
code, nested ternaries, clones of at least 15 lines and 100 tokens, or dependency cycles. Every
non-trivial module has a test file. Exclude only unmaintained generated, vendored or minified code;
a maintained file gets no suppression. Record any missing stack mechanism in the testing guide and
route it.

Mutation is a separate project gate, not a coverage tier. Run the project's changed-code mutation
command and acceptance-example checks; a missing mechanism or report is a gap, never a green run.
