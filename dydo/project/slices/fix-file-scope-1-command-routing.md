---
title: Route Explicit Files Through File Scope
sprint: fix-file-scope
seq: 1
status: done
area: backend
type: context
---

# Slice 1 — Route Explicit Files Through File Scope

Use a complete lookup corpus while restricting all file-level writes to one selected document.

## Spec fragment

Make `dydo fix <file.md>` complete without sending the file to directory enumeration. The
selected file alone receives naming, wikilink, and reporting work. Wikilinks still resolve
against the complete containing docs corpus. Directory invocations retain current behavior.

This slice is accepted when a standalone file succeeds; an in-project selected file is renamed
and then converts a cross-subtree wikilink; corpus-only documents remain unchanged; an unrelated
non-kebab sibling remains unrenamed; a selected file beside `dydo.json` is not lost to docs-root
discovery; a selected file in a legacy `docs/guides` tree resolves through `docs/reference`; and
every gate below passes.

## Implementation detail

Touch only `Commands/FixCommand.cs`, `Commands/FixFileHandler.cs`,
`DynaDocs.Tests/Commands/FixFileHandlerTests.cs`, and
`DynaDocs.Tests/Integration/FixCommandIntegrationTests.cs`.

### Scope resolution in FixCommand

- Replace `ResolvePath`'s nullable string with a private immutable scope containing `CorpusRoot`
  and nullable `FilePath`.
- Existing directory: full directory path as `CorpusRoot`, null `FilePath`.
- Existing file: full file path as `FilePath`; resolve `CorpusRoot` through a private
  `FindContainingCorpusRoot(FilePath)` helper.
- In that helper, save the file's full immediate parent as the fallback. Walk a `DirectoryInfo`
  cursor from that parent through every `.Parent` until null. At each cursor, call
  `PathUtils.FindDocsFolder(cursor.FullName)`. If it returns a candidate and
  `CheckDocValidator.IsUnderScope(FilePath, candidate)` is true, return that candidate.
- Continue past both null candidates and non-containing candidates. This continuation is what
  makes legacy `<root>/docs` discoverable from a selected file under `<root>/docs/guides`:
  discovery succeeds when the cursor reaches `<root>`.
- If the ancestor walk is exhausted, return the saved immediate parent. Do not modify
  `PathUtils.FindDocsFolder` or other commands.
- No argument: discovered docs path as `CorpusRoot`, null `FilePath`.
- Missing explicit target or failed discovery: retain the existing tool error and exit 2.

### Corpus and mutation targets in FixCommand

- Each scan stores the complete result of `scanner.ScanDirectory(scope.CorpusRoot)` as
  `resolutionCorpus`.
- Add an exact-path selector. Directory scope returns the complete corpus; file scope returns the
  one document whose normalized full path equals `FilePath` using the project's case-insensitive
  comparison convention.
- In explicit-file scope, require the selector to return exactly one document after the initial
  scan and every rescan. A zero or multiple result prints an actionable error naming `FilePath`
  and `CorpusRoot`, returns `ExitCodes.ToolError`, and performs no subsequent config or file
  mutation. Never translate empty selection into `Fixed 0 issues automatically.`
- Pass only selected documents to naming, wikilink mutation, and manual reporting.
- Before naming, calculate the selected file's kebab destination. When exactly one rename
  succeeds, update `FilePath` to that destination before rescanning and reselecting. A valid name
  or conflict retains the original path.
- Pass `CorpusRoot` to `FindConfigFile(startPath)` and `LoadConfig(startPath)` inside
  `RestoreScanExcludeInvariants`.
- Run `FixHubHandler.RegenerateHubs` and `CreateMissingMetaFiles` only in directory scope.

### Wikilink handler seam

- Change `FixFileHandler.FixWikilinks` to require `docsToFix` and `resolutionCorpus`.
- Iterate only `docsToFix`. Pass `resolutionCorpus` to `LinkResolver.FindFileByName`.
- Folder scope passes its complete corpus as both arguments, preserving behavior.
- Update every direct handler call in `FixFileHandlerTests` for the new signature.
- Add a handler test with one mutation target and a separate resolution document. Assert the
  target converts its wikilink and the resolution document's bytes remain unchanged.

### Integration regressions

- `Fix_ExplicitStandaloneFile_Succeeds`: without project initialization, create a valid
  kebab-case Markdown file directly under isolated `TestDir`; invoke its absolute path; assert
  exit 0, `Fixed 0 issues automatically.`, and no `Error:`.
- `Fix_ExplicitFile_OnlyFixesSelectedFileAfterRename`: initialize a project. Create
  `dydo/guides/Selected File.md` containing `[[resolution-target]]`,
  `dydo/guides/Unrelated File.md` containing the same wikilink, and valid
  `dydo/reference/resolution-target.md`. Capture the unrelated and lookup files' exact contents.
  Invoke the selected file's absolute path.
- In the second test, assert `selected-file.md` exists and the original path is gone. Its content
  must contain `[resolution-target](../reference/resolution-target.md)` and no
  `[[resolution-target]]`. Assert the unrelated and lookup contents equal their captured values,
  `Unrelated File.md` still exists, `unrelated-file.md` does not, output reports only the selected
  rename, and output reports exactly one conversion.
- `Fix_ExplicitFileBesideProjectConfig_UsesParentCorpus`: initialize a project, then create valid
  `Project Note.md` directly beside `dydo.json` (outside `dydo/`). Invoke that absolute file path.
  Assert exit 0, `project-note.md` exists beside `dydo.json`, the original path is gone, output
  reports its rename, and output does not say `Fixed 0 issues automatically.` This is the
  containment regression: `PathUtils.FindDocsFolder(TestDir)` returns `TestDir/dydo`, but the
  command must reject that candidate as non-containing and scan `TestDir` instead.
- `Fix_ExplicitFileInLegacyDocs_UsesContainingRoot`: do not initialize a project or create
  `dydo.json`. Create valid `docs/index.md`, non-kebab
  `docs/guides/Legacy Selected.md` containing `[[legacy-target]]`, and valid
  `docs/reference/legacy-target.md`. Capture the lookup file's contents and invoke the selected
  file's absolute path. Assert exit 0, rename to `docs/guides/legacy-selected.md`, removal of the
  original path, conversion to `[legacy-target](../reference/legacy-target.md)`, absence of the
  wikilink, exactly one conversion in output, and byte-identical lookup content. This test must
  fail if corpus discovery stops at the immediate parent.

Do not make tests Windows-conditional. Preserve output text, manual aggregation, directory
behavior, and exit-code rules apart from removing the file-path crash.

## Out of scope for this slice

- `Services/DocScanner.cs`, `Commands/FixHubHandler.cs`, parsers, rules, docs, packaging, release,
  global installation, and Pokercept.
- Hub/meta regeneration during file scope.
- Production changes outside the two command files named above.

## Gate

Run these commands in order and require every command to pass.

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~FixCommandIntegrationTests|FullyQualifiedName~FixFileHandlerTests"
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
$smokePath = (Resolve-Path 'dydo/reference/dydo-glossary.md').Path
$beforeHash = (Get-FileHash -LiteralPath $smokePath -Algorithm SHA256).Hash
dotnet run --project DynaDocs.csproj -- fix $smokePath
if ($LASTEXITCODE -ne 0) { throw "source-built file-scope smoke exited $LASTEXITCODE" }
$afterHash = (Get-FileHash -LiteralPath $smokePath -Algorithm SHA256).Hash
if ($afterHash -ne $beforeHash) { throw 'file-scope smoke changed an already-clean target' }
```

The smoke exits 0 and preserves its clean target byte-for-byte.
