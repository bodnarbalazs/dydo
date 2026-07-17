# Reviewing Documentation

Target: documentation changes — dydo docs, READMEs, reference pages.

## Method

1. **Verify against [writing-docs.md](../../../dydo/reference/writing-docs.md)** — frontmatter, naming, linking, summary conventions.
2. **Verify claims against reality** — every command, path, and code reference in the doc must exist and behave as described. Doc drift is the signature failure: prose describing machinery that changed or died.
3. **Run `dydo check`** on the touched tree — links and structure must be clean.

## Checklist

- [ ] Conventions hold (frontmatter, naming, hub membership, links)
- [ ] Every command/path/claim verified against the current code — no drift
- [ ] Written for the reader named by its folder (understand/ vs guides/ vs reference/)
- [ ] Says one thing once — no duplication with an existing doc
- [ ] `dydo check` clean
