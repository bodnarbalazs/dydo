# Host skill canaries

This recorder proves that a clean, committed DynaDocs candidate is discovered through each host's
native skill mechanism. It materializes the exact candidate commit in an isolated checkout, runs
`setup-skills.mjs` twice, and writes durable JSON/JSONL evidence under an external retained path.
The caller must provide a physically clean scratch root outside Temp and every DynaDocs checkout;
the recorder rejects parent instructions, host skill roots, host configuration, and Git roots before
it creates a disposable run.

OpenCode is pinned to the official Windows portable v1.18.30 archive and never contacts an external
model provider. The recorder verifies the archive, executable, and supplied Ripgrep hashes; gives
the child isolated home, XDG, application-data, cache, state, and temporary directories; and serves
a fail-closed OpenAI-compatible loopback provider. The fixed `--title` is part of the gate: an extra
title-generation request fails the run. `OPENCODE_DISABLE_AUTOUPDATE=1`, `OPENCODE_PURE=1`, and
`npm_config_offline=true` are mandatory; the deny-proxy transcript must remain empty.

Codex runs with an empty isolated `CODEX_HOME` and a fail-closed loopback Responses provider; it does
not copy authentication or inherit API-key variables. Plugin startup is disabled, and exactly one
fully matched read-only command approval is accepted for the derived resource read. Claude's live
proof calls its configured model provider. Run the complete gate only when the repository owner has
approved that disclosure and the approval will be retained in the whole-gate staging packet.

```powershell
node DynaDocs.Tests/HostCanaries/run-host-canaries.mjs `
  --candidate . `
  --scratch-root "$env:LOCALAPPDATA\DynaDocs\host-canaries" `
  --evidence "$env:LOCALAPPDATA\DynaDocs\host-canary-evidence\DYD-91\<candidate-sha>-<run-id>\staging\host-canaries" `
  --opencode-archive "C:\path\to\opencode-windows-x64.zip" `
  --ripgrep "C:\path\to\rg.exe"
```

For local harness development, `--only claude`, `--only codex`, or `--only opencode` narrows the run.
That option is not a substitute for the complete final gate. A failed or partial run keeps its
external manifest and captured output, but never counts as acceptance evidence. The disposable run
is removed after post-run fingerprint and nested-Git checks; caller-owned scratch and retained
evidence roots are preserved.

The successful packet contains `manifest.json`, the two Claude streams, Codex inventory/live records,
OpenCode inventory/live records, and both loopback providers' complete request logs. The
manifest records the exact candidate SHA, versions, pinned hashes, commands, durations, assertions,
and hashes for every evidence artifact. Proxy evidence proves zero proxy-observed external attempts
and exact loopback traffic; it is not OS-level network confinement and makes no stronger claim.
