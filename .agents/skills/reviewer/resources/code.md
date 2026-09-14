<!-- Adapted from mattpocock/skills code-review at 6654f6b60cd9d5be8b54c6fafe44346dabeb3b76 (MIT). -->

# Reviewing Code

Target: the code one Linear Issue delivered, judged against that Issue's contract and, when one
governs the work, its reviewed Project plan.

## Method

1. **Pin the contract before the diff.** Outcome, owned paths, base SHA, acceptance criteria, exact
   gates, governing plan. Done when you can state what this change had to do without reading it.
2. **Read the diff, then the code it lands in.** `git diff <base>...HEAD` gives the delta; read
   enough of each file to judge the whole. Code that was already bad is a finding when this change
   builds on it. Done when every hunk is accounted for.
3. **Judge against the standards.** `dydo/guides/coding-standards.md` and any stack-specific
   standard bind, the anti-slop mandate included; a documented standard beats your taste.
4. **Weigh the smells.** Work the baseline below across the diff. Done when every smell has been
   asked and answered, not when the first one is found.
5. **Rerun the gates yourself.** The Issue's exact commands, plus `dydo check` when the change
   touches documentation or validation surfaces. An implementation report is a claim, not evidence.

## The smell baseline

A **smell** is a question, not a verdict. Name it as a possibility ("possible Feature Envy"), quote
the hunk, answer it — and it becomes a finding only once you can state the concrete consequence
here: a reader misled, one logical change forced to scatter, a seam no test can reach. A documented
standard overrides the baseline, anything the tooling already enforces is skipped, and smell in code
this change does not touch belongs to the invoker rather than to this verdict.

- **Mysterious Name** — hides what it does or holds. → rename; no honest name means a murky design.
- **Duplicated Code** — one logic shape in two hunks or files. → extract it, call it from both.
- **Feature Envy** — a method using another object's data more than its own. → move it there.
- **Data Clumps** — the same fields always travelling together. → bundle them into one type.
- **Primitive Obsession** — a primitive standing in for a domain concept. → give it its own type.
- **Repeated Switches** — the same cascade on one type, twice. → polymorphism or a shared map.
- **Shotgun Surgery** — one change forcing scattered edits. → gather what changes together.
- **Divergent Change** — one file edited for unrelated reasons. → split it by reason.
- **Speculative Generality** — abstraction for needs the contract does not have. → delete it.
- **Message Chains** — long `a.b().c().d()` walks the caller depends on. → hide the walk.
- **Middle Man** — a unit that mostly delegates onward. → cut it; call the target direct.
- **Refused Bequest** — a subclass ignoring most of what it inherits. → compose instead.

## Checklist

- [ ] Candidate matches the governing commit, the owned paths, and the requested outcome
- [ ] Logic holds at the edges: boundaries validated, no fallback masking an impossible state
- [ ] Tests name the behaviour and would fail if this code broke
- [ ] Standards hold, and every smell was asked and answered
- [ ] Nothing beyond the contract — an unrelated improvement is scope creep and a finding
- [ ] Every deviation the implementation reported is justified or raised
- [ ] Gates rerun by you, with their output

## Verdict

Fill the review block the reviewer skill defines, each finding in the shape it gives, and return
nothing else. PASS means no findings: a note is a finding, and a finding is a FAIL.
