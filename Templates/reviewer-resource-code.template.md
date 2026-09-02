<!-- Adapted from mattpocock/skills code-review at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Reviewing Code

Target: the code one Linear Issue delivered, judged against that Issue's contract and, when one
governs the work, its reviewed Project plan.

## Method

1. **Pin the contract before the diff.** Outcome, scenarios, owned paths, base SHA, exact gates,
   governing plan at its SHA. Done when you can state what this change had to do without reading it.
2. **Read the hops.** `git log <base>..<candidate>` lists the specify, implement, harden and fix
   commits; read what each changed. A behaviour or test one hop had and a later hop dropped is a
   finding when the contract needed it. Done when every hop is accounted for.
3. **Read the diff, then the code it lands in.** `git diff <base>...HEAD` gives the delta; read
   enough of each file to judge the whole. Code that was already bad is a finding when this change
   builds on it. Done when every hunk is accounted for.
4. **Judge against the standards.** `dydo/guides/coding-standards.md` and any stack-specific
   standard bind, the anti-slop mandate included; a documented standard beats your taste.
5. **Weigh the smells.** Work the baseline below across the diff. Done when every smell has been
   asked and answered, not when the first one is found.
6. **Rerun the gates yourself.** The Issue's exact commands, plus `dydo check` when the change
   touches documentation or validation surfaces. An implementation report is a claim, not evidence.

## The smell baseline

A **smell** is a question, not a verdict. Name it as a possibility ("possible Feature Envy"), quote
the hunk, answer it — and it becomes a finding only once you can state the concrete consequence
here: a reader misled, one logical change forced to scatter, a seam no test can reach. A documented
standard overrides the baseline, anything the tooling already enforces is skipped, and smell in code
this change does not touch belongs to the invoker rather than to this verdict.

Work the twelve smells in `dydo/guides/coding-standards.md`, each as a question against the diff.

## Checklist

- [ ] Candidate matches the governing commit, the owned paths, and the requested outcome
- [ ] Every scenario stands as the specifier committed it, and every scenario passes
- [ ] Nothing a hop had that the contract needed was lost by a later hop
- [ ] Logic holds at the edges: boundaries validated, no fallback masking an impossible state
- [ ] Tests name the behaviour and would fail if this code broke
- [ ] Standards hold, and every smell was asked and answered
- [ ] Nothing beyond the contract — an unrelated improvement is scope creep and a finding
- [ ] Every deviation the implementation reported is justified or raised
- [ ] Gates rerun by you, with their output

## Verdict

Fill the review block the reviewer skill defines, each finding in the shape it gives, and return
nothing else. PASS means no findings: a note is a finding, and a finding is a FAIL.
