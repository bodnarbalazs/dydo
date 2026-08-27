# Reviewing Documentation

Target: documentation changes delivered by one Linear Issue, judged against that Issue's reviewed
intent and any governing Git Project plan.

## Method

1. **Resolve the contract** — read the Issue, governing commit, and linked Project plan when one
   governs the work. Confirm the intended audience, owned paths, acceptance criteria, and gates.
2. **Verify against [writing-docs.md](../../../dydo/reference/writing-docs.md)** — frontmatter, naming,
   linking, summary, and related-section conventions.
3. **Verify claims against reality** — every command, path, behavior, and code reference must exist and
   behave as described. Doc drift is the signature failure: prose describing machinery that changed or
   died.
4. **Run the Issue's documentation gates, including `dydo check`** — verify them independently on the
   touched tree.
5. **Return a strict verdict** — record PASS or actionable findings on the Linear Issue. Verify that
   any reusable decision, invariant, pitfall, or explanation was assimilated into durable dydo/Git
   knowledge rather than left only in execution evidence; missing assimilation is a finding.

## Checklist

- [ ] Reviewed intent, governing commit, and applicable Project plan verified
- [ ] Conventions hold (frontmatter, naming, hub membership, links)
- [ ] Every command, path, and claim verified against current code — no drift
- [ ] Written for the reader named by its folder (understand/ vs guides/ vs reference/)
- [ ] Says one thing once — no duplication with an existing document
- [ ] Issue-owned paths and acceptance criteria are satisfied without unrelated edits
- [ ] `dydo check` and all Issue-specific gates are clean
- [ ] Durable knowledge is assimilated into Git; Linear retains execution status and evidence
