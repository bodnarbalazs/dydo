---
type: decision
status: accepted
date: 2026-03-07
area: project
---

# 002 — Template Update System

Include tags for project additions plus hash tracking for safe framework template overwrites during updates.

## Problem

When dydo is updated, framework files (templates, reference docs) may contain new features, flags, or workflow changes. Projects that use dydo have stale copies of these files. Currently there's no way to update them without manually diffing and merging — which is exactly what we had to do for the LC project.

Users also customize templates (e.g., adding project-specific verification steps). These customizations must survive framework updates. And they must live **in the template itself** — agents follow the steps they see in their mode file. Instructions buried only in coding-standards get ignored.

## Constraints

1. **Project customizations must survive updates** — gap_check.py in code-writer must not vanish
2. **Agents must see customizations inline** — they follow the steps in their mode file
3. **Same addition reusable across templates** — no file duplication if code-writer and reviewer need the same content
4. **Fully extensible** — any `{{include:whatever}}` works, not just predefined hooks
5. **Both lazy and proper paths must work** — users can edit templates directly OR use the additions system
6. **Simple** — no three-way merge, no section markers

## Decision

Two mechanisms working together: **include tags** for project additions + **hash tracking** for safe framework overwrites.

### How it works

Templates ship with `{{include:name}}` tags at natural extension points. These resolve to markdown files in `_system/template-additions/`. The `dydo template update` command overwrites templates from embedded resources (safe because additions live in separate files). Direct edits to templates are detected via hash comparison.

```
Template (framework-owned)              Additions (project-owned)
┌─────────────────────────────┐        ┌─────────────────────────────┐
│ ## Work                     │        │ _system/template-additions/ │
│                             │        │   extra-verify.md           │
│ 1. Understand               │        │   _README.md                │
│ 2. Implement                │        └─────────────────────────────┘
│ 3. Test                     │
│ 4. Verify — Run tests       │
│ {{include:extra-verify}}    │───> resolves to extra-verify.md content
│                             │     (empty string if file missing)
│ **If guard blocks you:**    │
│ ...                         │
└─────────────────────────────┘
```

### The two paths

**Proper path (recommended):** Create files in `template-additions/`. Templates stay stock. `dydo template update` works seamlessly.

**Dirty path (quick edits):** Edit the template directly — including adding `{{include:...}}` tags anywhere you want. Works immediately. On `dydo template update`, user-added include tags are detected, anchored by surrounding content, and re-inserted into the new template automatically.

### User-added include re-anchoring

Users can add `{{include:whatever}}` anywhere in a template. Include tags are always on their own line, so they sit between two chunks of framework content. Those chunks are the tag's **anchors**.

On `dydo template update`:
1. Compare the stored stock template (from hash/embedded) against the user's on-disk version
2. Find any `{{include:...}}` tags present on-disk but absent in stock — these are user-added
3. For each user-added tag, record the **line above** (upper anchor) and **line below** (lower anchor)
4. Write the new stock template
5. For each user-added tag, find its anchors in the new template and re-insert

Resolution rules:
- **Both anchors found** → insert between them
- **Only upper anchor found** → insert after it
- **Only lower anchor found** → insert before it
- **Neither anchor found** → cannot place — report to user, tag saved in `.unplaced` file

Anchors are matched by trimmed content (whitespace-insensitive). Empty/blank lines are skipped when finding anchors — the first non-blank line above and below is used.

This means both paths work with `dydo template update`:
- Shipped hooks in `template-additions/` → survive because templates are overwritten with new stock (which has the hooks)
- User-added hooks anywhere in the template → survive because they're re-anchored into the new template

### Include resolution

`{{include:extra-verify}}` → reads `_system/template-additions/extra-verify.md`

- Tag name = filename (minus `.md`)
- Missing file = tag resolves to empty string (no trace in output)
- Same tag in multiple templates = same file = shared content, zero duplication
- **Any `{{include:whatever}}` works** — not limited to shipped hooks
- Resolution happens alongside existing `{{AGENT_NAME}}`, `{{SOURCE_PATHS}}` etc.

### Shipped hook points

Templates ship with these tags at natural extension points. These are starting points — users can add more via the dirty path.

**All mode templates** — after must-reads list:
```markdown
{{include:extra-must-reads}}
```

**code-writer** — after verify step 4:
```markdown
{{include:extra-verify}}
```

**reviewer** — after work step 3 (run tests):
```markdown
{{include:extra-review-steps}}
```

**reviewer** — end of checklist:
```markdown
{{include:extra-review-checklist}}
```

**code-writer, reviewer** — end of complete section:
```markdown
{{include:extra-complete-gate}}
```

**test-writer** — after test guidance section:
```markdown
{{include:extra-test-guidance}}
```

### Default example

`dydo init` creates `_system/template-additions/` with:
- `_README.md` — explains the system, lists shipped hooks
- `extra-verify.md.example` — inactive example (rename to `.md` to activate)

The `.example` extension means it won't be picked up. This makes the system discoverable without being intrusive.

## Changes Required

### 1. `Services/TemplateGenerator.cs` — include resolution

Add a `ResolveIncludes` method. Call it after `ReplacePlaceholders` in `GenerateModeFile` and `GenerateWorkflowFile`.

```csharp
private static string ResolveIncludes(string content, string? basePath = null)
{
    var additionsPath = GetTemplateAdditionsPath(basePath);

    return Regex.Replace(content, @"\{\{include:([a-zA-Z0-9_-]+)\}\}", match =>
    {
        var name = match.Groups[1].Value;
        if (additionsPath == null) return "";

        var filePath = Path.Combine(additionsPath, $"{name}.md");
        return File.Exists(filePath) ? File.ReadAllText(filePath).TrimEnd() : "";
    });
}

private static string? GetTemplateAdditionsPath(string? basePath = null)
{
    basePath ??= Environment.CurrentDirectory;

    var inside = Path.Combine(basePath, "_system", "template-additions");
    if (Directory.Exists(inside)) return inside;

    var fromRoot = Path.Combine(basePath, "dydo", "_system", "template-additions");
    if (Directory.Exists(fromRoot)) return fromRoot;

    return null;
}
```

In `GenerateModeFile` and `GenerateWorkflowFile`, after `ReplacePlaceholders`:

```csharp
var result = ReplacePlaceholders(template, placeholders);
result = ResolveIncludes(result, basePath);
result = Regex.Replace(result, @"\n{3,}", "\n\n"); // collapse excess blank lines
return result;
```

### 2. `Templates/*.template.md` — add include tags

Insert tags in the embedded resource templates:

**All mode templates** — after must-reads numbered list:
```markdown
3. [coding-standards.md](...) — Code conventions
{{include:extra-must-reads}}
```

**mode-code-writer.template.md** — after verify step:
```markdown
4. **Verify** — Run tests, ensure they pass
{{include:extra-verify}}
```

**mode-reviewer.template.md** — after step 3 and end of checklist:
```markdown
3. **Run tests** — Verify they pass
{{include:extra-review-steps}}
4. **Document findings** — Note issues clearly
```

```markdown
- [ ] Changes match the task requirements
{{include:extra-review-checklist}}
```

**mode-code-writer.template.md, mode-reviewer.template.md** — end of complete section:
```markdown
{{include:extra-complete-gate}}
```

**mode-test-writer.template.md** — after verify step and after test guidance:
```markdown
{{include:extra-verify}}
```
```markdown
{{include:extra-test-guidance}}
```

### 3. `Models/DydoConfig.cs` — hash tracking

```csharp
[JsonPropertyName("frameworkHashes")]
public Dictionary<string, string> FrameworkHashes { get; set; } = new();
```

### 4. `Commands/TemplateCommand.cs` — new command

```bash
dydo template update           # apply updates, re-anchor user includes
dydo template update --diff    # preview only, no writes
dydo template update --force   # overwrite even if re-anchoring fails (backs up first)
```

Logic for each framework-owned template file:
1. Read embedded (new) version and on-disk version
2. If on-disk matches embedded → already current, skip
3. Compare on-disk hash to stored hash in `frameworkHashes`
4. If hash matches → no user edits → overwrite with new embedded, update hash
5. If hash mismatches → user edited the file:
   a. Retrieve the old stock content (embedded resource for the version stored in hash, or reconstruct from hash match)
   b. Diff old stock vs on-disk → extract user-added `{{include:...}}` tags with their anchors
   c. Write new embedded template
   d. Re-anchor each user-added tag into the new template
   e. If all tags placed → success, update hash
   f. If some tags unplaceable → write `.unplaced` file listing them with context, warn user
6. `--diff`: show what would change (including re-anchor placements), don't write
7. `--force`: on unplaceable tags, write the template anyway (backup first), save unplaced tags to `.unplaced`
8. First run (no stored hash): compare on-disk to embedded. Identical → store hash. Different → treat as user-edited, follow step 5.

For non-template framework files (about-dynadocs.md, etc.): simpler flow — hash match → overwrite. Hash mismatch → skip with warning unless `--force` (these don't have include tags).

**Note on old stock retrieval:** The old stock is the embedded template from the *currently installed* dydo version before the update. Since `dydo template update` runs *after* installing the new dydo, we need the old stock. Two options: (a) store the full stock content alongside the hash, or (b) store only the hash — if hash matches on-disk, on-disk IS the old stock; if not, the diff between old embedded and on-disk reveals user additions. Option (b) is simpler: the stored hash tells us whether the file was edited. If edited, we can diff the *previous* embedded (which we ship as a resource keyed by version, or simply accept that the hash alone suffices — the user's file IS the reference, and we extract include tags from it directly against the new template).

Framework-owned file list (hardcoded):

```csharp
public static readonly string[] FrameworkOwnedFiles =
[
    "_system/templates/agent-workflow.template.md",
    "_system/templates/mode-code-writer.template.md",
    "_system/templates/mode-reviewer.template.md",
    "_system/templates/mode-co-thinker.template.md",
    "_system/templates/mode-inquisitor.template.md",
    "_system/templates/mode-planner.template.md",
    "_system/templates/mode-docs-writer.template.md",
    "_system/templates/mode-test-writer.template.md",
    "_system/templates/mode-orchestrator.template.md",
    "_system/templates/mode-judge.template.md",
    "reference/about-dynadocs.md",
    "reference/dydo-commands.md",
    "reference/writing-docs.md",
    "guides/how-to-use-docs.md",
    "_assets/dydo-diagram.svg"
];
```

### 5. `Services/FolderScaffolder.cs` — scaffold additions folder + store hashes

In `Scaffold()`, after `CopyBuiltInTemplates(basePath)`:

```csharp
ScaffoldTemplateAdditions(basePath);
StoreInitialFrameworkHashes(basePath, config);
```

`ScaffoldTemplateAdditions` creates:
- `_system/template-additions/_README.md` (from embedded resource)
- `_system/template-additions/extra-verify.md.example` (from embedded resource)

`StoreInitialFrameworkHashes` computes SHA256 of each framework-owned file just written and stores in dydo.json.

### 6. `Services/IncludeReanchor.cs` — new service for re-anchoring logic

Extracts user-added include tags from the on-disk template (by comparing against stock) and re-inserts them into the new template.

```csharp
public static class IncludeReanchor
{
    public record IncludeTag(string Tag, string? UpperAnchor, string? LowerAnchor);
    public record ReanchorResult(string Content, List<string> Placed, List<string> Unplaced);

    /// <summary>
    /// Extract {{include:...}} tags from userContent that are not in stockContent.
    /// For each, record the nearest non-blank line above and below as anchors.
    /// </summary>
    public static List<IncludeTag> ExtractUserIncludes(string stockContent, string userContent);

    /// <summary>
    /// Insert user-added include tags into newContent using their anchors.
    /// Returns the merged content and lists of placed/unplaced tags.
    /// </summary>
    public static ReanchorResult Reanchor(string newContent, List<IncludeTag> userIncludes);
}
```

Anchor matching is trimmed and whitespace-insensitive. Empty lines are skipped when finding anchors.

### 7. Embedded resources — new files

- `Templates/template-additions-readme.md` — content for `_README.md`
- `Templates/extra-verify.example.md` — content for the example file

### 7. Documentation updates

**`reference/dydo-commands.md`** — add `dydo template update` command section with options.

**`reference/about-dynadocs.md`** — add a "Template Additions" subsection under "Customize the templates" explaining the include system and both paths.

## Edge Cases

### Include resolution (agent claim time)

| Scenario | Behavior |
|----------|----------|
| No `template-additions/` folder | Include tags resolve to empty string |
| Addition file is empty | Tag resolves to empty string |
| Missing file | Tag resolves to empty string (no trace in output) |
| Trailing newlines in addition file | `TrimEnd()` before injection |
| Multiple `{{include:same}}` in one file | All resolve to same content |
| `{{include:no spaces!}}` (invalid chars) | Regex rejects — tag left as-is (visible signal) |

### Template update (dydo template update)

| Scenario | Behavior |
|----------|----------|
| Clean project (no user edits) | All framework files overwritten, hashes updated |
| User added `{{include:custom}}` to template | Tag extracted, re-anchored into new template |
| User added include — both anchors found in new template | Tag inserted between anchors |
| User added include — only upper anchor found | Tag inserted after upper anchor |
| User added include — only lower anchor found | Tag inserted before lower anchor |
| User added include — neither anchor found | Tag reported as unplaced, saved to `.unplaced` file |
| User added multiple includes to same template | Each re-anchored independently |
| User added same include tag to multiple templates | Re-anchored in each template independently |
| User made non-include edits (rewrote text) | Those edits are lost on update (only include tags are preserved) |
| Pre-additions project (no hashes) | Compare on-disk to embedded. Identical → store hash. Different → extract includes, re-anchor |
| `--force` with unplaceable tags | Writes template anyway, backs up old, saves unplaced to `.unplaced` |
| `--diff` flag | Shows preview of all changes including re-anchor placements, no writes |
| `dydo init --join` | Doesn't overwrite existing `template-additions/` |
| Binary framework file (svg) | Hash comparison only, no include logic |
| Anchor line appears multiple times in new template | Use first occurrence (anchors are contextual — typically unique) |
| User added include between two blank lines | Skip blanks, use nearest non-blank lines as anchors |

## Test Plan

### Unit: `TemplateGeneratorTests.cs` — include resolution

**Core resolution:**
- `ResolveIncludes_ResolvesExistingFile` — tag replaced with file content
- `ResolveIncludes_MissingFile_ResolvesToEmpty` — tag disappears cleanly
- `ResolveIncludes_NoAdditionsFolder_ResolvesToEmpty` — graceful when folder doesn't exist
- `ResolveIncludes_MultipleTagsSameFile_AllResolved` — same tag twice in one template
- `ResolveIncludes_MultipleDifferentTags_EachResolved` — different tags, different files
- `ResolveIncludes_EmptyFile_ResolvesToEmpty` — addition file exists but is empty
- `ResolveIncludes_InvalidTagChars_LeftAsIs` — `{{include:no spaces!}}` not matched
- `ResolveIncludes_TrimsTrailingNewlines` — no excess whitespace from file content
- `ResolveIncludes_ExcessiveBlankLinesCollapsed` — 3+ newlines → 2

**Integration with generation:**
- `GenerateModeFile_WithAddition_IncludesContent` — addition file content appears in generated mode file
- `GenerateModeFile_WithoutAddition_NoLeftoverTags` — no `{{include:...}}` in output
- `GenerateModeFile_AdditionAndPlaceholders_BothResolved` — `{{AGENT_NAME}}` and `{{include:...}}` both work
- `GenerateWorkflowFile_WithAddition_IncludesContent` — same for workflow files

**Tag name formats:**
- `ResolveIncludes_SupportsHyphens` — `{{include:my-custom-step}}`
- `ResolveIncludes_SupportsUnderscores` — `{{include:my_custom_step}}`
- `ResolveIncludes_SupportsNumbers` — `{{include:step2-verify}}`
- `ResolveIncludes_CaseSensitive` — `{{include:Extra-Verify}}` → `Extra-Verify.md`

### Unit: `IncludeReanchorTests.cs` — the re-anchoring engine

**Extraction:**
- `ExtractUserIncludes_NoUserTags_ReturnsEmpty` — stock and user identical
- `ExtractUserIncludes_OneUserTag_ExtractsWithAnchors` — user added one include, both anchors captured
- `ExtractUserIncludes_MultipleUserTags_ExtractsAll` — user added several includes at different spots
- `ExtractUserIncludes_ShippedTagsIgnored` — tags present in stock are not extracted (they're framework hooks)
- `ExtractUserIncludes_TagBetweenBlankLines_SkipsBlanksForAnchors` — anchors are nearest non-blank lines
- `ExtractUserIncludes_TagAtTopOfFile_UpperAnchorNull` — no content above → null upper anchor
- `ExtractUserIncludes_TagAtBottomOfFile_LowerAnchorNull` — no content below → null lower anchor

**Re-anchoring — both anchors found:**
- `Reanchor_BothAnchorsFound_InsertsCorrectly` — tag placed between matching lines
- `Reanchor_BothAnchorsFound_PreservesBlankLineSeparation` — clean formatting around inserted tag
- `Reanchor_MultipleUserTags_AllPlaced` — several tags, all anchored correctly
- `Reanchor_AnchorMatchIsTrimmed` — leading/trailing whitespace differences don't break matching

**Re-anchoring — partial anchors:**
- `Reanchor_OnlyUpperAnchor_InsertsAfterIt` — lower anchor missing in new template
- `Reanchor_OnlyLowerAnchor_InsertsBeforeIt` — upper anchor missing in new template

**Re-anchoring — no anchors:**
- `Reanchor_NeitherAnchorFound_ReportsUnplaced` — tag in unplaced list, not in output
- `Reanchor_MixOfPlacedAndUnplaced_CorrectLists` — some tags anchor, some don't

**Re-anchoring — tricky cases:**
- `Reanchor_AnchorAppearsMultipleTimes_UsesFirstOccurrence` — deterministic placement
- `Reanchor_UserTagAdjacentToShippedTag_BothSurvive` — user tag next to framework tag, both in output
- `Reanchor_NewTemplateHasNewSections_AnchorsStillMatch` — framework added content elsewhere, existing anchors still found
- `Reanchor_AnchorContentReworded_AnchorNotFound` — framework changed the anchor line → tag unplaced (correct)
- `Reanchor_MultipleTagsBetweenSameAnchors_AllInserted` — two tags sharing anchor lines
- `Reanchor_EmptyNewTemplate_AllUnplaced` — degenerate case

### Unit: `TemplateUpdateTests.cs` — hash and update logic

**Hash computation:**
- `ComputeHash_ConsistentForSameContent` — deterministic
- `ComputeHash_DifferentContent_DifferentHash`

**Direct edit detection:**
- `IsDirectlyEdited_HashMatches_ReturnsFalse` — file unchanged
- `IsDirectlyEdited_HashMismatch_ReturnsTrue` — user edited it
- `IsDirectlyEdited_NoStoredHash_ContentMatchesEmbedded_ReturnsFalse` — first-run clean
- `IsDirectlyEdited_NoStoredHash_ContentDiffers_ReturnsTrue` — first-run customized

**Update with re-anchoring:**
- `UpdateFile_CleanFile_Overwrites` — hash matches, file overwritten
- `UpdateFile_UserAddedIncludes_ReanchorsIntoNew` — includes extracted and placed in new template
- `UpdateFile_UserAddedIncludes_AllPlaced_Success` — all tags anchored, hash updated
- `UpdateFile_UserAddedIncludes_SomeUnplaced_WarnsAndWritesUnplacedFile` — partial success
- `UpdateFile_UserMadeNonIncludeEdits_OnlyIncludesPreserved` — text edits lost, includes kept
- `UpdateFile_AlreadyUpToDate_NoOp` — on-disk matches embedded
- `UpdateFile_StoresUpdatedHash` — hash updated after successful write
- `UpdateFile_Force_WithUnplaced_WritesAnywayWithBackup` — backup created, unplaced saved
- `UpdateFile_BinaryFile_ComparesBytes` — SVG: hash only, no include logic

### Integration: `TemplateOverrideTests.cs` — additions in scaffolding

- `Init_CreatesTemplateAdditionsFolder` — folder exists after init
- `Init_CreatesReadmeInAdditions` — `_README.md` present
- `Init_CreatesExampleFile` — `extra-verify.md.example` present
- `Init_StoresFrameworkHashes` — dydo.json has hashes for all framework files
- `AgentClaim_WithAdditionFile_IncludesInModeFile` — create `extra-verify.md`, claim agent, mode file contains it
- `AgentClaim_WithoutAdditionFile_CleanOutput` — no addition files, no leftover tags
- `AgentClaim_SharedAddition_AppearsInMultipleModes` — same tag in code-writer and reviewer templates, one file, both generated modes have the content
- `Join_DoesNotOverwriteExistingAdditions` — additions folder preserved on join

### Integration: `TemplateCommandTests.cs` — the command itself

- `TemplateUpdate_CleanProject_UpdatesAllFiles` — all framework files overwritten
- `TemplateUpdate_UserAddedInclude_Reanchored` — user added `{{include:custom}}`, it appears in updated template
- `TemplateUpdate_UserAddedInclude_AnchorMoved_StillPlaced` — framework reorganized but anchor lines unchanged
- `TemplateUpdate_UserAddedInclude_AnchorRemoved_ReportedUnplaced` — anchor gone, user warned
- `TemplateUpdate_Force_BackupCreated` — `.backup/` contains old version
- `TemplateUpdate_Diff_ShowsReanchorPlacements` — `--diff` shows where tags will land
- `TemplateUpdate_AlreadyCurrent_ReportsNoChanges` — everything up to date
- `TemplateUpdate_PreAdditionsProject_ExtractsAndReanchors` — no hashes, user includes still found and placed
- `TemplateUpdate_PreservesTemplateAdditions` — additions folder untouched
- `TemplateUpdate_UpdatesConfigHashes` — dydo.json updated after success
- `TemplateUpdate_NonTemplateFrameworkFiles_AlsoUpdated` — about-dynadocs.md etc. updated
- `TemplateUpdate_PartialUpdate_MixedResults` — some files clean, some with includes, some unplaceable

### E2E: `CliEndToEndTests.cs`

- `TemplateUpdate_EndToEnd_ShippedHooks` — init, create addition file, update, verify addition content in regenerated mode files
- `TemplateUpdate_EndToEnd_UserAddedInclude` — init, add custom `{{include:my-step}}` to template between known lines, update, verify tag survives in updated template
- `TemplateUpdate_EndToEnd_MultiVersion` — init at v1, add custom include, update to v2 (new framework content), verify include re-anchored, update to v3 (more changes), verify include still re-anchored

## Scope

- `Services/TemplateGenerator.cs` — ~30 lines (include resolution + helper)
- `Services/IncludeReanchor.cs` — ~120 lines (extraction + re-anchoring logic)
- `Commands/TemplateCommand.cs` — ~180 lines (command + update orchestration)
- `Models/DydoConfig.cs` — ~3 lines (hash dictionary)
- `Services/FolderScaffolder.cs` — ~30 lines (scaffold additions, store initial hashes)
- `Templates/*.template.md` — 4 tag insertions across templates
- `Templates/` — 2 new embedded resources (README + example)
- Docs updates — dydo-commands.md, about-dynadocs.md

~360 lines of production code across 6 files. ~750 lines of tests.
