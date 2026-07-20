---
title: ns-7 Converter Hardening
blocked-by: ns-6-depth-limit-append
due:
needs-human: false
priority: Medium
sprint: notion-stabilization
status: backlog
work-type: feature
area: backend
type: context
---

# ns-7 Converter Hardening

Port the ecosystem's converter lessons (MIT sources only — martian/notion-to-md; see [notion-oss-survey.md](../../reference/notion-oss-survey.md), md→blocks + Notion→md sections) into `NotionBlockConverter`. Today it handles code-language normalization and 2000-char run splitting; the remaining hard-limit and fidelity gaps are below.

**Already done (plan-gate verified — do NOT redo):** surrogate-safe 2000-char splitting exists (`Sync/Notion/Dtos/NotionRichText.cs:34-41`); most language aliases exist in the converter's static map (`sh/zsh/pwsh/ps1/yml/dockerfile/tex/golang/cs`, and `bash` is natively accepted). The converter emits plain unannotated runs, so annotation-boundary concerns don't apply this sprint.

## Task

1. **Per-block rich_text cap:** enforce ≤100 rich_text items per block; overflow continues in a following sibling paragraph block (never truncate).
2. **Language aliases:** verify the existing `LanguageAliases` dictionary (`NotionBlockConverter.cs:135`) against the survey's list and add only the genuinely missing entries (`js`/`ts` already exist per the plan gate; `node` is the known gap — re-verify at implementation time). Keep the static dictionary — no resource-file indirection.
3. **Tables:** `table_width` from the widest row; pad short rows with empty cells; header flag from the markdown separator row.
4. **Blockquotes:** first paragraph into the quote's own rich_text, remaining content as children (avoid Notion's "Empty quote" rendering; children require ns-6's DTO support).
5. **Headings:** clamp H4–H6 to `heading_3`.
6. **Read-side `[!missing]` markers:** when converting Notion blocks → markdown, render any unsupported block type as `> [!missing] <block_type>` instead of dropping it silently (this also stabilizes ns-8's hashing — dropped-content diffs become visible, deterministic text).

## Files

- `Sync/Notion/NotionBlockConverter.cs`
- Tests: `DynaDocs.Tests/Sync/Notion/` converter tests (extend the existing file)

## Success criteria

- New tests per item above (long paragraph overflows to sibling; each newly added alias maps; ragged table pads; quote renders non-empty; H5 → heading_3; unsupported block → visible marker that round-trips deterministically).
- Full ratchet green.
