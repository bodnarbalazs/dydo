---
id: 184
area: backend
type: issue
severity: medium
status: open
found-by: manual
date: 2026-05-19
---

# dydo check link validator flags valid relative cross-folder markdown links as broken (dydo graph resolver works)

dydo check's link validator and dydo graph's resolver disagree on relative cross-folder markdown links (../sibling/file.md, ../../foo/bar.md). check flags them broken; graph resolves them correctly. 55-error false-positive wall observed on decisions folder; folder-meta _*.md files especially affected.

## Description

`dydo check`'s link validator flags relative markdown links of the form `[label](../sibling-folder/file.md)` (or `../../foo/bar.md`) as **"Broken link"** even when the target file exists on disk. The same links resolve correctly under `dydo graph`, so the two consumers are using different (and divergent) resolution paths.

## Evidence (LC project, 2026-05-19)

Reporter ran `dydo check dydo/project/decisions/` and got **55 errors of this exact shape** — every flagged target is a real, on-disk file. Examples:

- `_decisions.md:41` → `../pitfalls/_index.md` (exists)
- `006-coverage-tooling-split.md:143` → `../../guides/testing-strategy.md`
- `010-admin-panel-architecture.md:117` → `../beta-v1-scope.md`
- `026-dmca-posture-...md:71` → `../future-features/automatic-dmca-counter-notice.md`
- `031-api-error-modal-...md:22,42,49` → `../../reference/api-client.md`

Manual `ls` confirmed four supposedly-broken targets exist: `dydo/reference/api-client.md`, `dydo/reference/writing-docs.md`, `dydo/project/issues/_index.md`, `dydo/project/future-features/_index.md`.

A separate agent (Kate) independently ran `dydo graph` against the same link her `_backlog.md` draft used (`../decisions/035-backlog-doc-category.md`) — **graph resolved it fine.** So the link resolver works for `dydo graph` but the link checker in `dydo check` is using different (and wrong) resolution.

**Folder-meta files (`_*.md`) appear especially affected** per Kate's observation — worth investigating whether the validator's path-normalization treats `_*.md` differently or whether they just happen to contain more cross-folder links.

## Distinct from prior reports

This is **not** the LC-side issue about `dydo/** → src/**` cross-tree links (decision 021's design-system narrative-vs-code split). That issue is about links crossing OUT of the docs tree. **This bug is `dydo/** → dydo/**`** — cross-folder *within* the docs tree. Same family of "link resolver gets confused," different surface.

## Workaround in active use

Replace `[label](../sibling/file.md)` with backtick code refs (`` `dydo/sibling/file.md` ``). Renders `dydo check`-clean; loses click-through but preserves grep/path-mention. In LC this is in use in `_backlog.md`, Emma's `querykey-hygiene-factory-and-lint.md`, and the reporter's Decision 035 (where they left the markdown links, contributing 3 to the noise count).

## Why it matters

Anyone running `dydo check` on the decisions folder gets a 55-error wall they have to learn to ignore. Eats the signal of `dydo check` for that folder. Forces every new cross-folder doc reference into the backtick-code-ref workaround, which loses click-through navigation in editors. Newcomers running `dydo check` for the first time will reasonably conclude the docs tree is broken.

## Suggested investigation order

1. **Diff the resolver in `dydo check`'s link validator against the one in `dydo graph`.** They appear to disagree. Find the divergence.
2. **Confirm or refute the folder-meta (`_*.md`) suspicion.** Are these files getting a different base path for relative resolution?
3. **Check whether `dydo/** → dydo/**` is the only failing surface,** or whether the same resolver also mishandles other in-tree relative patterns (e.g., `./sibling.md`, anchor-only `#section`, links from `_index.md` files).
4. **Test-coverage gap audit.** What tests exercise cross-folder relative links in `dydo check`? Likely none, or none with the failing shape.

## Related context

- `Rules/` — the link-validator rule lives here. Likely candidates: a `LinkRule` / `BrokenLinkRule` implementation.
- `dydo graph` codepath — wherever it resolves links is the working reference implementation.
- Prior precedent for `dydo check` rules being wrong: resolved issues #0159 (`Frontmatter.ValidTypes`), #0160 (`SummaryRule` template skip-block missing), #0163 (`DocScanner` recursing into worktrees), #0164 (skip-pattern duplication across rules).

## Severity rationale

Medium. Not a correctness or security bug — but `dydo check` is the project's primary doc-health signal, and a 55-error false-positive wall on a single folder makes it useless for that folder. Combined with the backtick-workaround spreading through the docs tree, the bug actively degrades the doc UX over time.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)