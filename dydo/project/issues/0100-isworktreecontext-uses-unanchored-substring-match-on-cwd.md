---
id: 100
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-04-18
---

# IsWorktreeContext uses unanchored substring match on CWD

## Description

`Commands/GuardCommand.cs:80-86`:

```csharp
internal static bool IsWorktreeContext()
{
    if (IsWorktreeContextOverride != null)
        return IsWorktreeContextOverride();
    var cwd = Directory.GetCurrentDirectory().Replace('\\', '/');
    return cwd.Contains(WorktreePathMarker);
}
```

`WorktreePathMarker = "dydo/_system/.local/worktrees/"`. The check is a plain `Contains`. Any directory whose path includes that substring anywhere — for example a sibling directory named `worktrees-notes/` underneath `dydo/_system/.local/`, a backup tree carrying the marker in its name, or a user project that imitates the layout — would be classified as a worktree context and receive the auto-approve emission.

A safer check would either anchor against `PathUtils.GetMainProjectRoot(Environment.CurrentDirectory)` (already used in `ResolveWorktreePath`) or probe for one of the worktree marker files (`.worktree`, `.worktree-path`, `.worktree-base`, `.worktree-root`).

Probability of triggering in real-world DynaDocs use is near zero — but the function is the single gate guarding an auto-approval signal, so unanchored matching is worth tightening.

Filed by inquisition `auto-accept-edits-behavior` (2026-04-18 — Frank), finding 2.

## Reproduction

Create a directory whose absolute path contains the literal substring `dydo/_system/.local/worktrees/` but is not actually a worktree (e.g., `…/some-project/dydo/_system/.local/worktrees-notes/scratch/`). `cd` into it, run the guard with stdin matching a Read or Write call. The guard emits the worktree allow JSON.

## Resolution

(Filled when resolved)