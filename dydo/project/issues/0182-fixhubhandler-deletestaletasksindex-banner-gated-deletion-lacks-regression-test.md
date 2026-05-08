---
id: 182
area: general
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-08
---

# FixHubHandler.DeleteStaleTasksIndex banner-gated deletion lacks regression test pin

Commands/FixHubHandler.cs:138-150 deletes project/tasks/_index.md only when the file content contains HubGenerator.AutoGenComment (banner-gated). The logic is correct by inspection but has no direct test pin. HubGeneratorTests.cs:153 only asserts the file is not regenerated, which is weaker than 'stale auto-gen file is deleted on next dydo fix.' Add a banner-gated deletion regression test.

## Description

Surfaced by Dexter's pre-tag v1.4.7 audit (Finding #7) and confirmed by Emma's judge ruling.

## Coverage gap

Commands/FixHubHandler.cs:138-150 (DeleteStaleTasksIndex):

```csharp
private static int DeleteStaleTasksIndex(string basePath)
{
    var path = Path.Combine(basePath, "project", "tasks", "_index.md");
    if (!File.Exists(path)) return 0;

    var content = File.ReadAllText(path);
    if (!content.Contains(HubGenerator.AutoGenComment)) return 0;

    File.Delete(path);
    ConsoleOutput.WriteSuccess(...);
    return 1;
}
```

The banner-gated deletion is the only thing standing between hand-written tasks/_index.md files and clobbering. Search for DeleteStaleTasksIndex or StaleTasksIndex in DynaDocs.Tests/ returns no matches — no direct regression pin.

HubGeneratorTests.cs:153 asserts the file is *not regenerated*, which is a weaker invariant than the deletion contract.

## Why low severity

- Function logic is small and easy to verify by inspection.
- Banner check is the entire mechanism — there is one branch.
- Fix is mechanical: add a unit/integration test for the deletion + the hand-written-file preservation case.

## Suggested test (v1.4.8)

Two cases:
1. project/tasks/_index.md with HubGenerator.AutoGenComment present → file deleted, return 1, success log.
2. project/tasks/_index.md without the banner (hand-written) → file preserved, return 0, no log.

## Pre-tag posture

Not a v1.4.7 blocker. Code is correct by inspection. This issue tracks the missing regression pin for v1.4.8.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)