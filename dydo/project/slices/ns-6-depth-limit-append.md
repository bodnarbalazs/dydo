---
title: ns-6 Nested Block Converter and Depth-Limited Append
blocked-by:
due:
needs-human: false
priority: High
sprint: notion-stabilization
status: done
work-type: feature
area: backend
type: context
---

# ns-6 Nested Block Converter and Depth-Limited Append

**Honest baseline (plan-gate verified):** `Sync/Notion/NotionBlockConverter.cs` is a deliberately lossy line-based converter — each non-blank line becomes one flat block; indented list items don't parse as nested lists. `Sync/Notion/Dtos/NotionBlock.cs` has **no children field**, so the DTO model cannot express nesting at all, and `INotionClient.AppendBlockChildren` returns void, so created block ids are unavailable. Consequence: nested markdown structure in spine bodies flattens (silent fidelity loss), and the Notion API's 2-levels-per-request limit is unhandled because we never produce nesting in the first place.

This slice rebuilds block conversion to be structure-aware and makes deep structure land correctly: parse on the Markdig AST, express children in the DTOs, cut payloads at depth 2, and append deferred descendants iteratively using the block ids the API returns (algorithm per [notion-oss-survey.md](../../reference/notion-oss-survey.md), API-limits section).

**Scope boundary:** inline rich text stays plain runs — no bold/italic/link annotation support this sprint. Block *structure* only.

## Task

1. **Converter restructure:** re-implement markdown→blocks in `NotionBlockConverter` over the Markdig AST (Markdig is already a project dependency — `Services/MarkdownParser.cs` builds a pipeline). Preserve every currently-supported mapping (headings, paragraphs, code fences with language normalization, flat bullets, the existing 2000-char run splitting in `Sync/Notion/Dtos/NotionRichText.cs`) — existing converter tests must keep passing or be consciously retargeted with the reviewer's eyes on each change. Add: nested bullet/numbered lists as `children` hierarchies.
2. **DTOs:** add an optional `children` array to the block DTO (`Sync/Notion/Dtos/NotionBlock.cs`) wired through the source-generated JSON context (`Serialization/NotionJsonContext.cs`).
3. **Id-returning appends:** change `INotionClient.AppendBlockChildren` (and `NotionClient`) to return the created blocks' ids (the API response contains them; extend the response DTO as needed). Update `FakeNotionClient` to mint deterministic ids.
4. **Depth cutting + iterative append:** a pass over converter output produces (a) a payload nested at most 2 deep and (b) ordered deferred continuations (parent-path → children). After each create/append, resolve parent-paths to real block ids from the response and issue follow-up appends, reapplying the cut recursively (BFS by depth). Compose with the existing 100-block chunking (`NotionSyncAdapter.cs:134-147` and the client's append chunking): chunk within every level.
5. Keep the cutting pass in one place so any future block writer inherits it.

## Files

- `Sync/Notion/NotionBlockConverter.cs`, `Sync/Notion/Dtos/NotionBlock.cs` (+ append response DTO), `Serialization/NotionJsonContext.cs`
- `Sync/Notion/NotionClient.cs`, `INotionClient.cs`, `Sync/Notion/NotionSyncAdapter.cs`
- Tests: `DynaDocs.Tests/Sync/Notion/` converter, client, adapter suites + `FakeNotionClient`

## Success criteria

- Existing converter behaviors covered by tests still pass (or are consciously retargeted, called out in the review).
- New tests: 2-deep list converts to nested children in one payload; 4-deep list produces an initial ≤2-deep payload plus follow-up appends targeting the correct returned parent ids, order preserved; 250 flat blocks still chunk 100/100/50; deep+wide composes (chunked at every level).
- A payload-inspecting fake asserts no emitted payload ever exceeds depth 2.
- Full ratchet green (commands in the sprint root Specification).
