---
title: Fix Explicit-File Scope
seq: 10
status: done
gate-result: audit PASS (2026-08-08; commit e96001ca; 2,524 tests; 131/131 coverage)
area: project
type: context
---

# Fix Explicit-File Scope

Repair the command-boundary defect that makes an explicit Markdown file look like a directory.

## 1. Specification

**Intent** — Make `dydo fix <file.md>` honor the documented file argument on every supported
platform. The command currently accepts a file and passes it to `Directory.GetFiles`, so Windows
fails before `FIXED:` and before any write; directory-scoped `dydo fix` is healthy.

**In scope**

- Classify an explicit Markdown file separately from a directory before scanning.
- Scan the complete containing docs tree as the resolution corpus.
- Apply naming, wikilink mutation, and manual-fix reporting only to the selected file.
- Resolve the selected file's wikilinks against the complete corpus without writing corpus-only
  documents.
- Re-target the selected path after a successful kebab-case rename so later phases still act on
  the renamed file.
- Anchor scan-exclude restoration to the selected file's containing project.
- Add handler and command regressions for corpus resolution, post-rename continuation, and
  target-only mutation.

**Out of scope**

- `dydo check`, parsing, validation rules, or file-write primitives.
- Changes to directory-scoped fix behavior.
- Hub or meta-file regeneration during explicit-file invocation. Those remain directory-scope
  operations.
- Packaging, global installation, publication, or Pokercept edits.

**Acceptance criteria**

- `dydo fix <existing-file.md>` never passes that file to a `Directory.*` enumeration API and
  exits 0 when the file needs no fixes.
- A standalone Markdown file succeeds without a dydo project.
- A file inside a dydo project is resolved within the full docs root, not merely its parent
  folder.
- A file under a legacy `docs` tree is resolved against that complete containing legacy root,
  including cross-subtree wikilinks.
- A selected Markdown file beside `dydo.json` but outside the configured docs root uses its own
  parent directory as the corpus and is fixed; project discovery must not silently drop it.
- A non-kebab selected file is renamed, then its cross-subtree wikilink is converted in the later
  wikilink phase.
- A non-kebab sibling is not renamed or edited, and the corpus-only lookup document remains
  byte-identical.
- A directory invocation retains the existing full pipeline, including scan-exclude repair,
  hub regeneration, and meta-file creation.
- Focused tests, the full repository runner, and the forced 131-module coverage gap gate pass.

**Questions & answers**

- **Is this a machine-wide write failure?** No. Installed 2.2.3 returned 2 for a disposable file
  but 0 for its containing directory and completed the write-capable pipeline. The exception is
  the first `DocScanner.ScanDirectory(filePath)` call.
- **What does explicit file scope mean?** Only the selected document is a mutation target.
  Folder-derived artifacts remain directory operations.
- **How does target-only mutation preserve wikilink behavior?** The command scans the complete
  docs root into a resolution corpus and selects the target separately. The handler loops over
  targets but resolves names against the corpus.
- **What happens after a rename?** The command computes the possible destination first and adopts
  it only when the singleton rename succeeds. A conflict retains the original scope.
- **Should `DocScanner.ScanDirectory` accept files?** No. The command boundary translates a file
  into a corpus directory plus an exact-file selector.
- **How are configured and legacy roots discovered for an explicit file?** Walk ancestors from
  the file's parent toward the filesystem root. At each ancestor, call
  `PathUtils.FindDocsFolder(ancestor)` and adopt the first returned candidate that contains the
  selected file. This finds configured roots through config walk-up and finds legacy `docs`
  roots when the walk reaches the directory that owns `docs/`.
- **What if every discovered candidate is non-containing or no candidate exists?** Use the
  selected file's immediate parent as `CorpusRoot`; an explicit file beside a docs tree remains
  fixable without silently entering that unrelated tree.
- **Can selection be empty?** Not silently. After each corpus scan, explicit-file scope must
  select exactly one document. Zero or multiple matches return an actionable tool error before
  any fix/config write; a post-rename zero match is likewise a tool error.

## 2. Prior art

- `Commands/CheckCommand.cs` separates docs-root discovery from report scope.
- `Commands/CheckDocValidator.cs` scans a complete corpus and separately filters documents acted
  upon; this is the adopted shape.
- `Commands/FixCommand.cs` at release commit `36c866e8` and current HEAD accepts `File.Exists`
  before calling `DocScanner.ScanDirectory` with the same path.
- `Commands/FixFileHandler.cs` currently uses one list both for the mutation loop and for
  `LinkResolver.FindFileByName`; its API must expose those two responsibilities.
- `DynaDocs.Tests/Commands/FixFileHandlerTests.cs` is the unit-test seam for target-versus-corpus
  behavior. `DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs` is the end-to-end seam.
- Rejected: write, timestamp, temp, or antivirus changes. Directory success disproves that path.
- Rejected: fixing all siblings when a file is selected. Narrow invocation must not widen writes.

## 3. Design

`FixCommand` replaces the resolved string with a private immutable scope containing `CorpusRoot`
and nullable `FilePath`. A directory argument produces `(directory, null)`. A file argument uses
its full path as `FilePath` and calls a private `FindContainingCorpusRoot` helper. Starting at the
file's parent, the helper walks `DirectoryInfo.Parent` to the filesystem root. At each ancestor it
calls `PathUtils.FindDocsFolder(ancestor.FullName)`; it returns the first non-null candidate for
which `CheckDocValidator.IsUnderScope(FilePath, candidate)` is true. Non-containing candidates
are ignored and the walk continues. If the loop finds none, return the file's immediate parent.
This is deliberately command-local: no project-wide change to existing `FindDocsFolder` callers
or legacy discovery semantics. No argument keeps current docs-root discovery.

Every pipeline scan retains the full `resolutionCorpus` from
`DocScanner.ScanDirectory(CorpusRoot)`. A private selector returns that full list in directory
scope or the exact selected document in file scope. Naming and manual reporting receive only the
selected list. For explicit-file scope, require exactly one selected document immediately after
every scan; otherwise print an actionable selection/corpus error and return `ToolError` before
continuing. Before naming, compute the selected file's kebab destination; if one rename succeeds,
update `FilePath`, rescan the corpus, and require exactly one match at the destination.

Change `FixFileHandler.FixWikilinks` to require `docsToFix` and `resolutionCorpus`. Its outer loop
uses only `docsToFix`; `LinkResolver.FindFileByName` receives `resolutionCorpus`. Directory scope
passes the same full list twice, preserving existing behavior. Unit coverage proves a corpus-only
document enables resolution without being written. Integration coverage proves the renamed
target reaches this later phase and resolves across docs subtrees while a sibling stays intact.

`RestoreScanExcludeInvariants` receives `CorpusRoot` for config lookup. Both folder handlers run
only when `FilePath` is null. No scanner, hub-handler, parser, or service abstraction changes.

Hazards: use full normalized paths for exact selection; never retarget after a rename conflict;
never pass `docsToFix` as the lookup corpus in file scope; and preserve directory behavior.
Rollback is a plain revert of the four implementation/test files.

## 4. Slice map

| # | slice file | files touched (disjoint) | deps | gate |
|---|---|---|---|---|
| 1 | `fix-file-scope-1-command-routing` | `Commands/FixCommand.cs`; `Commands/FixFileHandler.cs`; `DynaDocs.Tests/Commands/FixFileHandlerTests.cs`; `DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs` | — | focused runner, full runner, forced gap check, source-built smoke |

## 5. Ordering & isolation

One slice forms one lane in the current worktree. The lane owns only the four files in the slice
map and preserves every pre-existing dirty/untracked file. No parallel worktree or merge ordering
is useful.

## 6. Watch-outs

- Do not add write abstractions or exception catches; the crash precedes all writes.
- Do not broaden `DocScanner`; only the handler's two-list seam is required.
- Do not mutate or report manual fixes for corpus-only documents.
- Do not adopt a discovered docs root until exact-or-descendant containment is proven.
- Do not stop the ancestor walk on a null or non-containing candidate; a legacy root may only be
  discoverable from a higher ancestor.
- Do not turn an empty explicit-file selection into `Fixed 0 issues`; fail before mutation.
- Preserve rename-conflict exit behavior.
- No reset, stash, blanket formatting, broad staging, or index regeneration.

## Plan review

**Round 1: FAIL** (2026-08-08, fresh-eyes reviewer).

- Finding: selected-only scanning destroyed the full wikilink lookup corpus.
  Remediation: the plan now retains a full corpus and adds an explicit two-list handler API.
- Finding: the rename test did not prove later phases reselected the destination.
  Remediation: the integration test now requires post-rename cross-subtree wikilink conversion.
- Finding: the slice omitted the exact coverage command.
  Remediation: `py DynaDocs.Tests/coverage/gap_check.py --force-run` is a mandatory gate.
- Finding: the slice Markdown failed `NotionBodyFixedPointTests`.
  Remediation: the slice was rewritten without the drifting nested ordered-list shape and the
  full runner is rerun before round 2.

**Remediation verification (2026-08-08):** focused baseline 31 passed; full isolated runner
2,519 passed and 10 live tests skipped; forced coverage gap 131/131 modules passed; both plan
records pass `dydo check` with zero errors. The source-built smoke remains the implementation
gate by design: current production still reproduces the bug this sprint will fix.

**Round 2: FAIL** (2026-08-08, fresh-eyes reviewer).

All four round-1 findings are closed, and the reviewer independently reproduced the green gates:
31 focused tests passed; the full isolated runner passed 2,519 tests with 10 live skips; forced
coverage passed 131/131 modules; and both plan records passed `dydo check` with zero errors.

- **Finding: file-to-corpus resolution silently loses a project-contained file outside the docs
  root.** The design and slice say every explicit file adopts
  `PathUtils.FindDocsFolder(parentDirectory)` whenever it returns a value, falling back to the
  parent only when discovery returns null (Design, lines 87-90; slice lines 35-37). But
  `PathUtils.FindDocsFolder` returns the configured dydo root for any start path beneath the
  project (`Utils/PathUtils.Discovery.cs`, lines 61-73); it does not verify that the selected file
  is under that root. For `dydo fix README.md` at a configured project root, `CorpusRoot` therefore
  becomes `<project>/dydo`, the exact-path selector returns no document, and the command exits as a
  silent no-op instead of fixing or rejecting the explicit file. Close the specification by
  choosing the contract for an existing Markdown file outside a discovered docs root, add the
  corresponding containment branch before adopting that root, and require an integration test
  with a file beside `dydo.json` that proves the chosen mutation-or-error outcome. Apply the same
  rule to legacy docs discovery so "complete containing docs tree" is mechanically true.

**Round 2 remediation:** a discovered configured or legacy docs root is now adopted only when
`CheckDocValidator.IsUnderScope(selectedFile, candidateRoot)` proves containment; otherwise the
file's parent becomes its corpus. Explicit-file selection must equal one after every scan or the
command returns a tool error before writes. A new integration regression selects a non-kebab
Markdown file beside `dydo.json`, proves it is renamed, and proves the invocation cannot silently
report zero fixes. Status remains `plan-review` pending round 3.

Status remains `plan-review`; implementation is not green-lit.

**Round 3: FAIL** (2026-08-08, fresh-eyes reviewer).

The configured-project containment case from round 2 is now closed and mechanically covered.
Independent gates are green: 31 focused tests passed; the full isolated runner passed 2,519 tests
with 10 live skips; forced coverage passed 131/131 modules; and both plan records passed
`dydo check` with zero errors.

- **Finding: the promised legacy-docs containment behavior cannot be implemented by the specified
  lookup.** The root and slice say that calling `PathUtils.FindDocsFolder(parentDirectory)` and
  applying a containment check handles configured and legacy roots identically (Design, lines
  96-103; slice lines 36-41). Config discovery walks upward, but the helper's legacy branch only
  checks `<startPath>/docs` and its immediate children (`Utils/PathUtils.Discovery.cs`, lines
  61-92); it does not walk ancestors. For `<root>/docs/guides/Selected File.md`, starting at
  `<root>/docs/guides` returns null, so the plan falls back to that immediate folder rather than
  the complete `<root>/docs` corpus. A cross-subtree wikilink to `<root>/docs/reference/...` then
  cannot resolve, contradicting the explicit legacy promise in the Q&A (lines 72-75) and the
  complete-corpus contract. Specify an ancestor search (or explicitly remove legacy layouts from
  scope) instead of a single parent lookup. If legacy support remains in scope, require an
  integration regression with a selected file under `docs/guides` and its wikilink target under
  `docs/reference`, proving discovery chooses the containing legacy root and conversion succeeds.

**Round 3 remediation:** file scope now performs a command-local ancestor walk. Each ancestor's
`FindDocsFolder` result is only accepted after containment; the walk continues past null and
non-containing candidates and falls back to the immediate parent only after exhaustion. A new
legacy integration regression selects a non-kebab file under `docs/guides`, resolves its
wikilink through `docs/reference`, and proves post-rename conversion uses the complete legacy
root. Status remains `plan-review` pending round 4.

Status remains `plan-review`; implementation is not green-lit.

**Round 4: PASS** (2026-08-08, fresh-eyes reviewer).

The command-local ancestor walk now handles configured roots, non-containing discovered roots,
standalone files, and legacy roots mechanically, with integration cases that distinguish each
branch. All earlier findings remain closed. The specification has no open questions; the slice is
self-contained, atomic, file-disjoint, and explicit about ordering, isolation, hazards, rollback,
and exact gates. Independent validation passed: 31/31 focused tests; 2,519 full-suite tests with
10 live skips; forced coverage 131/131 modules; and zero `dydo check` errors in both plan records.

Plan review is complete. Status is `active`; implementation is green-lit.
