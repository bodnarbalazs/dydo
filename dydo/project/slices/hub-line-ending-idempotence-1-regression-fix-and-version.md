---
title: Prove Hub Idempotence, Fix Generation, and Bump v2.2.7
sprint: hub-line-ending-idempotence
seq: 1
status: done
area: backend
type: context
---

# Slice 1 — Prove Hub Idempotence, Fix Generation, and Bump v2.2.7

Lock the no-op regeneration behavior, repair the generator's final newline, and prepare v2.2.7.

## Spec fragment

Make generated hubs internally consistent with the platform-native line ending and prove that an
otherwise-current hub is not rewritten by `dydo fix`'s hub handler. The Slice is accepted when the
regression demonstrates zero changes, no `Updated` output, and unchanged bytes; the generator uses
`Environment.NewLine`; the project and packed tool report 2.2.7; and every gate passes.

## Implementation detail

Touch only `DynaDocs.Tests/Commands/FixHubHandlerTests.cs`, `Services/HubGenerator.cs`, and
`DynaDocs.csproj` for implementation.

1. In `FixHubHandlerTests`, first add
   `RegenerateHubs_DoesNotRewriteHubWithPlatformLineEndings`:
   - Create `guides/example.md` under the test's isolated `_basePath` with valid frontmatter, H1,
     and summary content.
   - Scan the tree without a hub, obtain the guide document, and call
     `HubGenerator.GenerateHub("guides", ...)` for the seed content. Convert that seed with
     `.ReplaceLineEndings(Environment.NewLine)` before writing `guides/_index.md`; this represents
     the platform-native checkout state and turns the current mixed Windows output into CRLF.
   - Rescan so the handler sees the hub, then save `File.ReadAllBytes(indexPath)`.
   - Invoke `FixHubHandler.RegenerateHubs` inside `ConsoleCapture.Stdout`, retaining its returned
     integer in a local initialized outside the capture lambda.
   - Assert the return value is `0`, captured output does not contain `Updated`, and the post-run
     bytes equal the saved bytes.
2. Run the focused gate before changing production. On Windows, require the new regression to fail
   because the current generator's final hard-coded LF makes the handler report and rewrite the
   otherwise-current CRLF hub. Record that expected red result in the Slice result.
3. In `HubGenerator.GenerateHub`, change only:

   ```csharp
   return sb.ToString().TrimEnd() + Environment.NewLine;
   ```

   Do not alter `TrimEnd`, `StringBuilder` calls, generated text, or `FixHubHandler` comparison.
4. Change only the `DynaDocs.csproj` `<Version>` value from `2.2.6` to `2.2.7`.
5. Run every gate below. Record the focused/full/coverage results and the packed tool's version in
   the Slice before sending it to code review.

## Out of scope for this slice

- `.gitattributes`, Git configuration, existing generated hubs, and line-ending normalization
  outside the one test fixture.
- `Commands/FixHubHandler.cs`, comparison semantics, other `dydo fix` paths, and unrelated cleanup.
- `npm/package.json`, release-workflow edits, public registry publication, tag creation, or global
  tool replacement. Those irreversible release operations happen only after the Sprint audit.

## Gate

Run in order and require every command to pass. The two fixed release-candidate directories must
not exist before the package smoke; if either exists, stop and report the collision rather than
deleting or reusing it.

```powershell
py DynaDocs.Tests/coverage/run_tests.py -- --filter "FullyQualifiedName~FixHubHandlerTests"
py DynaDocs.Tests/coverage/run_tests.py
py DynaDocs.Tests/coverage/gap_check.py --force-run
dotnet build DynaDocs.csproj -c Release --nologo
if (Test-Path -LiteralPath C:\tmp\dydo-v227-hub-eol-package) { throw 'RC package path already exists.' }
if (Test-Path -LiteralPath C:\tmp\dydo-v227-hub-eol-tool) { throw 'RC tool path already exists.' }
dotnet pack DynaDocs.csproj -c Release -o C:\tmp\dydo-v227-hub-eol-package --no-restore
dotnet tool install --tool-path C:\tmp\dydo-v227-hub-eol-tool --add-source C:\tmp\dydo-v227-hub-eol-package dydo --version 2.2.7
$rcVersion = (& C:\tmp\dydo-v227-hub-eol-tool\dydo.exe version).Trim()
if ($rcVersion -ne 'dydo version 2.2.7') { throw "RC version mismatch: $rcVersion" }
dotnet run --project DynaDocs.csproj -- check dydo/project/sprints/hub-line-ending-idempotence.md
dotnet run --project DynaDocs.csproj -- check dydo/project/slices/hub-line-ending-idempotence-1-regression-fix-and-version.md
git diff --check -- Services/HubGenerator.cs DynaDocs.Tests/Commands/FixHubHandlerTests.cs DynaDocs.csproj dydo/project/sprints/hub-line-ending-idempotence.md dydo/project/slices/hub-line-ending-idempotence-1-regression-fix-and-version.md
```

## Result

Expected red: before the production change, the focused isolated regression failed with
`Expected: 0; Actual: 1`, proving the CRLF hub was reported as updated. After the change, the
focused runner passed 3/3 and the full isolated suite passed 2,539 tests with 10 live tests
skipped. Coverage passed 131/131 modules. The Release build, 2.2.7 package, and local tool install
passed; the packed tool reported `dydo version 2.2.7`. Both record checks completed with 0 errors
and their existing orphan-record warnings. The scoped whitespace check passed.

## Code review

**PASS** — No code or out-of-scope findings. Fresh-eyes review independently passed the focused
tests (3/3), full suite (2,539 passed, 10 skipped), coverage (131/131 modules), warning-free Release
build, clean package/install smoke, exact `dydo version 2.2.7` output, both Record checks, the
repository-wide check, and the scoped whitespace check.
