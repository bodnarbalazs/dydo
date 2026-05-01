---
id: 145
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-05-01
resolved-date: 2026-05-01
---

# PowerShell-routed dydo wait requires approval every re-arm — breaks unattended wait flow

Open medium-severity bug: `dydo wait` invoked through Claude Code's PowerShell tool prompts the human for approval on every re-arm because `.claude/settings.local.json` ships only the `Bash(dydo:*)` allow entry — the `dydo init claude` template (`Commands/InitCommand.cs`) never seeded the PowerShell equivalent. Fix is to seed both shell entries in init, update the example config, and add regression tests; user has approved the surgical v1.4.x patch path.

## Description

When an agent invokes `dydo wait` (or any `dydo` command) via Claude Code's **PowerShell** tool, Claude Code prompts the human for approval on every invocation. The same command via the **Bash** tool auto-allows.

The friction is fatal for unattended sessions: every `dydo wait` re-arm — which now happens routinely after each soft inbox notice (v1.4 wait paradigm) — wakes a human approval prompt, breaking long-running orchestrators and watchdog flows.

**Root cause:** the project's `.claude/settings.local.json` `permissions.allow` list contains `Bash(dydo:*)` but no `PowerShell(...)` equivalent. The `dydo init claude` template (`Commands/InitCommand.cs:318`) hardcodes only the Bash form, so every initialized project reproduces the gap.

The dydo guard itself is **not** the source of the asymmetry — guard pipeline routing for PowerShell was added in `07c7000` and is symmetric with Bash. The issue lives entirely in Claude Code's permission allow-list, which dydo seeds at init time.

## Reproduction

1. In a project initialized with `dydo init claude`, observe `.claude/settings.local.json` contains `Bash(dydo:*)` but no `PowerShell(...)` entry.
2. From an agent session, run `dydo wait` via the **Bash** tool in background — no prompt; auto-approved by the matcher.
3. Run the same `dydo wait` via the **PowerShell** tool in background — Claude Code shows:
   ```
   PowerShell command
     dydo wait
   This command requires approval
     1. Yes
     2. Yes, and don't ask again for: dydo wait *
     3. No
   ```
4. The prompt fires on every re-arm; option 2 is per-session and doesn't persist across sessions.

## Resolution

**Path (a) — surgical, v1.4.x patch.** Confirmed by user 2026-05-01.

### Changes

1. **`Commands/InitCommand.cs`** — replace the single `DydoAllowEntry` constant with an array containing both `Bash(dydo:*)` and `PowerShell(dydo:*)`; update `ConfigureAllowList` to iterate and dedupe each.
2. **`.claude/settings.local.json`** (checked-in copy) — add `PowerShell(dydo:*)` so the maintainer's working tree stops prompting.
3. **`dydo/reference/configuration.md:96-99`** — add `PowerShell(dydo:*)` to the example allow list.
4. **`DynaDocs.Tests/Integration/InitCommandTests.cs`** — four new tests mirroring the Bash assertions at lines 272-336:
   - `Init_Claude_AddsPowerShellDydoAllowEntry`
   - `Init_Claude_PowerShellAllowMergesWithExistingEntries`
   - `Init_Claude_DoesNotDuplicatePowerShellAllowEntry`
   - `Init_Claude_BothShellEntriesWhenAllowArrayMissing`

### Why not path (b)

A guard-side auto-allow (emit `permissionDecision:"allow"` for `dydo *` shell commands, parallel to the existing `WorktreeAllowJson` mechanism) would be cleaner long-term but is out of scope for this patch: larger blast radius, requires careful subcommand scoping (avoid recursive `dydo guard` auto-allow), and the brief explicitly forbids guard matcher changes without separate approval. Worth revisiting in a dedicated decision doc.

### Acceptance

- Fresh `dydo init claude` produces an allow list with both shell entries.
- PowerShell-routed `dydo wait` no longer triggers the approval prompt.
- Re-init / join is idempotent on both entries.
- Existing Bash auto-allow regression-tested by current suite.

### Notes

Investigation notes: `dydo/agents/Dexter/notes-investigate-pwsh-wait-approval.md`.
