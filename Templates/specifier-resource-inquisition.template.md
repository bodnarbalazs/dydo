# Specify an Inquisition

The human has confirmed its scope and cost. Pin the integrated feature SHA and plan, then make the
sweep and its proofs bounded. It files findings; it has no PASS/FAIL verdict.

1. Name parts and cross-cutting lenses, one bounded brief per read-only inquisitor. Include the
   governing acceptance and prior reviews. Done when every assigned surface has one owner.
2. Fix the hypothesis shape: suspected failure, seam, input/state, expected observation and the test
   that would refute it. A proof-only implementer writes only the test and returns `confirmed` with
   its red-test SHA, `not reproduced`, or `inconclusive` with the deciding observation.
3. Plan an `inquisition/<slug>` branch from the feature SHA, never merged, with child proof branches.
   The Captain deduplicates confirmed problems into Bugs under the Project, using the feature as
   base and linking each reproduction commit. Done when every hypothesis has a verdict and every
   confirmed problem has its Bug.
4. Name the evidence for `docs-writer`: scope, parts/lenses, findings, hypotheses/verdicts and Bugs.
   The record goes in `dydo/project/inquisitions/`; after it and the Bugs exist the Captain closes
   the Issue and deletes the branch. The sweep and proofs use `In Progress`, not chain-hop statuses.
