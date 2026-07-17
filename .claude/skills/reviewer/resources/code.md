# Reviewing Code

Target: one implemented slice (or a standalone change). The slice file is the contract the code
must be judged against.

## Method

1. **Read the slice file first** — what was supposed to be built, which files, what gates.
2. **Review the diff — and the code it lands in.** The git diff shows what changed; read enough of the surrounding code to judge the whole, not just the delta. Code that was bad before the change is still a finding when the change builds on it. Check against the coding standards, including stack-specific standards if present.
3. **Run the slice's gate commands** — verify green yourself; don't trust the report.
4. **Run `dydo check`** — errors must be clean; new warnings are findings, pre-existing ones get noted in the verdict.

## Checklist

- [ ] Code follows coding standards (general + stack-specific)
- [ ] Logic is correct and handles edge cases
- [ ] Tests exist, are meaningful, and would fail if the code were broken
- [ ] No security vulnerabilities introduced
- [ ] No unnecessary complexity — anti-slop applies to reviews too
- [ ] Changes match the covering slice — files outside its list and unrequested "improvements" are findings
- [ ] Plan deviations the worker reported: each is either justified or a finding
