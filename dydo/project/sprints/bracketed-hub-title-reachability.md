---
title: Bracketed Hub Title Reachability
seq: 12
status: done
gate-result: implementation gates PASS; ship authorized (2026-08-09)
area: backend
type: context
---

# Bracketed Hub Title Reachability

Keep bracket-titled documents reachable after automatic hub regeneration.

## 1. Specification

**Intent** — Keep documents whose H1 titles contain square brackets reachable after `dydo fix` regenerates their folder hubs. Preserve the title text in the generated link label and correct dydo's own link extraction instead of rewriting bracketed display text.

**In scope**

- Recognize generated Markdown links whose display text contains balanced square brackets.
- Prove the parser seam directly and the complete `fix` then `check` workflow.

**Out of scope**

- General Markdown parser replacement, destination-parenthesis changes, title rewriting, documentation, packaging, and release work.

**Acceptance criteria**

- `dydo fix` retains `Fix the [VERIFY] markers` in the generated hub entry.
- Link extraction returns the complete display text and target from that entry.
- A following `dydo check` does not report the bracket-titled document as orphaned.
- Focused, full isolated, and forced coverage gates pass.

**Questions & answers**

- Rewrite brackets or fix parsing? Fix parsing; an existing regression intentionally preserves standalone bracket text.
- Broaden all Markdown parsing? No; extend only the current link-label grammar needed for balanced bracket text.

## 2. Prior art

`Services/HubGenerator.cs` already routes titles through `EscapeLinkLiterals`, and `DynaDocs.Tests/Services/HubGeneratorTests.cs` intentionally preserves `Fix the [VERIFY] markers in migration`. `Services/LinkExtractor.cs` still uses a flat label regex that stops at the first closing bracket. Official dydo 2.2.4 reproduced the resulting false orphan warning.

## 3. Design

Extend the existing generated Markdown-link regex to accept one or more ordinary characters or balanced bracket groups inside a display label. Keep destination parsing, frontmatter/code/H1 exclusions, and wikilink extraction unchanged. Add a parser test plus a command integration regression that creates the reported title, fixes the docs tree, checks it, and asserts no orphan warning.

Rollback is the single production regex line plus its two regression tests. The main hazard is accidentally changing wikilink or inline-code behavior; existing focused parser and integration suites guard those seams.

## 4. Slice map

| # | slice file | files touched | deps | gate |
|---|---|---|---|---|
| 1 | `bracketed-hub-title-reachability-1-parser-and-regression.md` | `Services/LinkExtractor.cs`, `DynaDocs.Tests/Services/MarkdownParserTests.cs`, `DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs` | — | focused parser/fix tests, full isolated suite, forced coverage |

## 5. Ordering & isolation

Run one serial in-tree lane. The production and test files form one atomic behavior seam and share repository-wide gates.

## 6. Watch-outs

- Do not solve this by altering H1 content or generated display text.
- Do not interpret `[[wikilink]]` as a Markdown link.
- The integration assertion must inspect `check` output, not merely the generated hub bytes.
