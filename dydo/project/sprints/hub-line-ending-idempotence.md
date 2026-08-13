---
title: Hub Line-Ending Idempotence and v2.2.7
seq: 14
status: done
gate-result: audit PASS (2026-08-13)
area: backend
type: context
---

# Hub Line-Ending Idempotence and v2.2.7

Stop `dydo fix` from rewriting current Windows hubs solely because generation mixes CRLF and LF.

## 1. Specification

**Intent** — Make generated hubs use one platform-native line-ending convention so `dydo fix`
is idempotent against hubs that Git checked out with Windows CRLF. Lock the reported no-op rewrite
at the `FixHubHandler` seam and advance the executable/package version to 2.2.7 for the requested
patch release.

**In scope**

- A regression that starts from a hub normalized to `Environment.NewLine`, runs hub regeneration,
  and proves the file is not rewritten or reported as `Updated`.
- The smallest generator correction: finish `GenerateHub` with `Environment.NewLine`, matching its
  existing `StringBuilder.AppendLine` calls.
- Advance `DynaDocs.csproj` from 2.2.6 to 2.2.7 and prove the packed tool reports 2.2.7.

**Out of scope**

- `.gitattributes`, Git configuration, repository-wide line-ending normalization, or committing
  regenerated `_index.md` files.
- Normalizing content inside `FixHubHandler` before comparison; real byte-level content changes
  must continue to trigger a rewrite.
- Changes to `npm/package.json` or `.github/workflows/release.yml`; the existing tag workflow sets
  the npm and NuGet package versions from the tag.
- Any `dydo fix` behavior unrelated to generated-hub line endings.

**Acceptance criteria**

1. `HubGenerator.GenerateHub` ends with exactly one `Environment.NewLine` and introduces no mixed
   line endings on Windows.
2. Given an otherwise-current hub whose content uses `Environment.NewLine`,
   `FixHubHandler.RegenerateHubs` returns zero, emits no `Updated` line, and leaves the file bytes
   unchanged.
3. A real generated-hub content difference still follows the existing raw comparison and rewrite
   path; no comparison normalization is added.
4. `DynaDocs.csproj` declares version 2.2.7, and a locally packed tool reports
   `dydo version 2.2.7`.
5. The focused test, full isolated suite, forced coverage gate, source-built Record checks, Release
   build/package smoke, and bounded diff check all pass.

**Questions & answers**

- Which line-ending convention should generated hubs use? The current platform's
  `Environment.NewLine`, because every existing `AppendLine` in `GenerateHub` already uses it.
- Should `FixHubHandler` compare normalized strings? No. That would broaden behavior and could
  suppress legitimate byte-level changes; the generator must emit internally consistent content.
- Should existing hubs or `.gitattributes` be normalized? No. The defect is generation-time mixed
  output, and this patch must not absorb unrelated generated-file churn.
- Which release version is next? 2.2.7, following the current 2.2.6 project version and tag.
- Should npm metadata be edited locally? No. `.github/workflows/release.yml` runs `npm version` from
  the pushed tag before publishing; the native binaries need the explicit project-version bump.
- When may `v2.2.7` be created? Only after the Slice passes review and the merged Sprint passes its
  audit. Publication is an irreversible post-audit operation, not part of implementation.

## 2. Prior art

- `Services/HubGenerator.cs` constructs the complete hub with `StringBuilder.AppendLine`, which is
  platform-native, but currently appends a hard-coded final `"\n"`. On Windows that creates the
  observed CRLF/LF mixture.
- `Commands/FixHubHandler.cs` intentionally compares `existingHub.Content != newContent` and writes
  only on inequality. Keep this raw comparison; once generation is consistent, an unchanged CRLF
  hub is equal on Windows.
- `DynaDocs.Tests/Commands/FixHubHandlerTests.cs` already owns direct regeneration tests and an
  isolated temporary docs root. `DynaDocs.Tests/ConsoleCapture.cs` is the established safe helper
  for asserting process-global console output.
- `DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs` has broader second-run idempotence
  coverage, but the direct handler suite is the smaller seam and can assert byte preservation plus
  the exact update count/output.
- `.github/workflows/release.yml` derives NuGet and npm package versions from `v*` tags, while its
  native `dotnet publish` consumes `DynaDocs.csproj`; therefore the project version must advance and
  `npm/package.json` must remain untouched.

Rejected alternatives: pinning Markdown to LF in `.gitattributes` would change repository policy
without repairing mixed output, and normalizing both strings in `FixHubHandler` would mask the
generator defect rather than removing it.

## 3. Design

Add one direct regression to `FixHubHandlerTests`. Create a normal `guides` document, scan it,
generate the expected hub, normalize that seed content with
`ReplaceLineEndings(Environment.NewLine)`, write it as `guides/_index.md`, and rescan. Capture the
bytes, run `RegenerateHubs` through `ConsoleCapture.Stdout`, and assert a zero result, no `Updated`
output, and byte-for-byte equality afterward. This test fails before the production change on
Windows—the reported environment—because regeneration produces mixed CRLF/LF content; on Unix it
continues to prove the same platform-native idempotence invariant.

After proving the regression first, change only the final expression in
`HubGenerator.GenerateHub` from `+ "\n"` to `+ Environment.NewLine`. Keep `TrimEnd`, all generated
text, and the raw handler comparison unchanged. Advance only the `DynaDocs.csproj` `Version`
property to 2.2.7, then run the full gates and local packed-tool version smoke.

The implementation rollback is three files: the handler regression, the generator's final newline,
and the project version. The main hazard is accidentally asserting timestamps rather than writes;
the regression uses the returned change count, captured output, and bytes so filesystem timestamp
granularity cannot make it flaky.

## 4. Slice map

| # | slice file | files touched (disjoint) | deps | gate |
|---|---|---|---|---|
| 1 | `hub-line-ending-idempotence-1-regression-fix-and-version.md` | `DynaDocs.Tests/Commands/FixHubHandlerTests.cs`, `Services/HubGenerator.cs`, `DynaDocs.csproj` | — | focused `FixHubHandlerTests`; full isolated suite; forced coverage; Release build, pack, and version smoke; source-built Record checks |

## 5. Ordering & isolation

Run one serial in-tree lane. The regression, one-line implementation, and patch-version bump form
one atomic release candidate and share repository-wide test, coverage, and package gates. Preserve
all pre-existing dirty and untracked files; stage only the Slice-owned implementation files and the
Sprint/Slice Records after review, never with a blanket add.

After the Slice review and merged Sprint audit pass, the top-level release flow may commit the
audited files, push `master`, create immutable tag `v2.2.7` at that exact commit, and push the tag.
Do not create or move the tag before the audit verdict.

## 6. Watch-outs

- The reproducer is intentionally Windows-sensitive before the fix. Do not weaken it merely because
  Unix already uses LF for both `AppendLine` and `Environment.NewLine`.
- Assert the returned fixed count, absence of `Updated`, and file bytes; checking content alone after
  Git normalization does not prove the handler skipped the write.
- Do not replace the raw comparison with `ReplaceLineEndings`, case folding, or trimmed comparison.
- Do not run `dydo fix` over this repository or stage generated hubs as part of this Sprint.
- Do not edit `npm/package.json`; the release workflow owns its publication-time version.
- A pushed release tag is immutable. If a source correction is required after publication begins,
  leave `v2.2.7` intact and prepare a new patch version.

## Plan review

**PASS** (2026-08-13, fresh-eyes reviewer).

The production and test seams are accurately described: `HubGenerator.GenerateHub` uses
platform-native `AppendLine` calls but appends a final literal LF, while `FixHubHandler` performs
the raw content comparison and writes only on inequality. The proposed regression exercises that
exact seam and proves the fixed-count, console-output, and byte-preservation contract. The current
project/package metadata and release workflow also support the specified 2.2.7 version path.

The specification has no open questions. Its single Slice is self-contained, atomic, mechanically
executable, file-disjoint, explicit about ordering, isolation, rollback, release hazards, and exact
gates, and requires no implementation-time design decisions. Independent baseline validation
passed: 2,538 tests with 10 live tests skipped; coverage passed 131/131 modules; both plan Records
passed `dydo check` with zero errors (only expected orphan warnings while untracked); the release
candidate paths are currently absent; and the bounded plan diff is clean.

Plan review is complete. Status is `active`; implementation is green-lit.

## Merged-Sprint audit

**PASS** (2026-08-13). No findings.

The complete release-candidate diff is acceptance-complete, seam-clean, covered, standards-clean,
and version-consistent. Independent gates passed: focused tests 3/3; full isolated suite 2,539
passed with 10 expected live skips; forced coverage 131/131 modules; warning-free Release build;
fresh pack and isolated tool install; exact `dydo version 2.2.7` output; zero-error Sprint and Slice
Record checks; and clean scoped whitespace checks. The raw hub comparison remains intact, real hub
content changes retain integration coverage, no `v2.2.7` tag existed during audit, and no
out-of-scope implementation files changed.
