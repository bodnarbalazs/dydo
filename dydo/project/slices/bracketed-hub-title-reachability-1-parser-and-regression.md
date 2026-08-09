---
title: Parse Bracketed Hub Labels and Prove Reachability
sprint: bracketed-hub-title-reachability
seq: 1
status: done
area: backend
type: context
---

# Slice 1 — Parse Bracketed Hub Labels and Prove Reachability

Teach link extraction to recognize preserved bracket text and lock the full command regression.

## Spec fragment

Preserve square brackets in generated hub labels while ensuring dydo extracts the complete Markdown link and therefore keeps the target reachable. Accept when both direct parsing and `fix` followed by `check` reproduce the repaired behavior and every gate passes.

## Implementation detail

Touch only `Services/LinkExtractor.cs`, `DynaDocs.Tests/Services/MarkdownParserTests.cs`, and `DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs` for implementation.

- First add `ExtractLinks_HandlesBalancedBracketsInDisplayText` using `- [Fix the [VERIFY] markers](./bracket-title.md)`. Assert exactly one Markdown link, complete display text, and `./bracket-title.md` target.
- Add `Fix_BracketedTitle_RemainsReachableAfterHubRegeneration`: initialize an isolated project, create `dydo/guides/bracket-title.md` with H1 `Fix the [VERIFY] markers` and a valid summary, run folder-scope fix, assert the generated hub preserves the exact bracketed label, then run check and assert it succeeds without an orphan warning for the document.
- Run the focused gate before production modification and require these new tests to fail for the expected missing-link/orphan reason.
- In `LinkExtractor`, replace only `MarkdownLinkRegex` with a pattern whose display group accepts ordinary non-bracket characters or balanced non-nested bracket groups. Require at least one display character and retain the existing non-empty destination capture and group numbering.
- Do not change hub generation: the existing bracket-preservation behavior is intentional.

## Out of scope for this slice

- Recursive Markdown grammar, nested destination parentheses, images, reference links, HTML, title rewriting, other commands, docs, packaging, or release changes.

## Gate

Run in order and require every command to pass:

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~MarkdownParserTests|FullyQualifiedName~FixCommandIntegrationTests"
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
dotnet run --project DynaDocs.csproj -- check dydo/project/sprints/bracketed-hub-title-reachability.md
dotnet run --project DynaDocs.csproj -- check dydo/project/slices/bracketed-hub-title-reachability-1-parser-and-regression.md
git diff --check -- Services/LinkExtractor.cs DynaDocs.Tests/Services/MarkdownParserTests.cs DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs dydo/project/sprints/bracketed-hub-title-reachability.md dydo/project/slices/bracketed-hub-title-reachability-1-parser-and-regression.md
```
