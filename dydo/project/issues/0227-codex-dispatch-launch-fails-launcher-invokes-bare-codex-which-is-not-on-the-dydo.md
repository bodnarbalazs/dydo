---
title: Codex dispatch launch fails: launcher invokes bare 'codex' which is not on the dydo-spawned terminal's PATH (resolves in the user's interactive shell but not the launched one, unlike 'claude'); needs robust resolution
id: 227
area: backend
type: issue
severity: high
status: in-flight
found-by: manual
found-by-agent: Adele
found-by-vendor: unknown
found-by-model: unknown
date: 2026-07-07
---

# Codex dispatch launch fails: launcher invokes bare 'codex' which is not on the dydo-spawned terminal's PATH (resolves in the user's interactive shell but not the launched one, unlike 'claude'); needs robust resolution

Codex dispatch currently emits a bare `codex` executable name; on the Windows packaged Codex install this resolves to a WindowsApps alias that fails with Access denied, so dispatched Codex agents never reach claim/inbox.

## Description

Inquisition finding: `TerminalLauncher.GetLaunchExecutable(host)` normalizes Codex to the literal string `codex`, and the platform launchers interpolate that directly into the shell command. The tests assert only that generated arguments contain `codex 'Adele --inbox'`; they do not verify the executable path is actually launchable.

Local evidence on this workstation:

- `codex --help` fails with `Program 'codex.exe' failed to run: Access is denied`.
- `Get-Command codex -All` resolves only to `C:\Program Files\WindowsApps\OpenAI.Codex_...\app\resources\codex.exe` and `codex`.

Consequence: `dydo dispatch --codex` can open a terminal that immediately fails before the target agent can claim identity or read its inbox.

Likely fix shape: resolve a launchable Codex CLI path per platform, with Windows handling packaged/MSIX aliases explicitly. Add tests that exercise executable resolution, not just command-string formatting.

## Reproduction

On the affected Windows packaged Codex install:

1. Run `dydo dispatch --codex --role code-writer --task codex-launch-smoke --brief "claim and report whoami"`.
2. Observe the launched terminal invokes bare `codex`.
3. The command fails with `Access is denied` before dydo claim/inbox handling.

Direct smoke:

```powershell
codex --help
Get-Command codex -All
```

## Resolution

(Filled when resolved)
