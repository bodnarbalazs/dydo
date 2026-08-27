# Reviewing Code

Target: one implemented Linear Issue. An atomic Issue may be the reviewed contract itself; coordinated
work is also governed by the reviewed Git Project plan linked from its Linear Project.

## Method

1. **Read the reviewed intent first** — resolve the Issue, its governing commit, and any linked
   Project plan. Verify the intended outcome, owned files, dependencies, acceptance criteria, and gates.
2. **Review the diff — and the code it lands in.** The git diff shows what changed; read enough of the
   surrounding code to judge the whole, not just the delta. Code that was bad before the change is
   still a finding when the change builds on it. Check the general and stack-specific coding standards.
3. **Run the Issue's gate commands** — verify green yourself; do not trust the implementation report.
4. **Run `dydo check`** when the change touches documentation or project-wide validation surfaces.
   Errors must be clean; new warnings are findings and existing warnings are called out in the verdict.
5. **Return an independent verdict** — PASS means no findings. Record the verdict and exact evidence
   on the Linear Issue before human harmonization; durable knowledge belongs in dydo/Git, not only in
   the review comment.

## Checklist

- [ ] Reviewed intent resolved from the Issue and, when applicable, its linked Project plan
- [ ] Governing commit and reviewed contract match the implementation under review
- [ ] Code follows coding standards (general + stack-specific)
- [ ] Logic is correct and handles edge cases
- [ ] Tests exist, are meaningful, and would fail if the code were broken
- [ ] No security vulnerabilities introduced
- [ ] No unnecessary complexity — anti-slop applies to reviews too
- [ ] Changes match the Issue's owned paths and requested outcome; unrelated improvements are findings
- [ ] Reported plan or Issue deviations are each justified or raised as findings
- [ ] Verdict is strict and includes reproducible gate evidence
