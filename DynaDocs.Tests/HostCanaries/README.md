# Host skill canaries

This recorder proves that a clean, committed DynaDocs candidate is discovered through each host's
native skill mechanism. It materializes the exact candidate commit in an isolated checkout, runs
`setup-skills.mjs` twice, and writes durable JSON/JSONL evidence under the requested evidence path.

OpenCode is pinned to the official Windows portable v1.18.30 archive and never contacts an external
model provider. The recorder verifies the archive, executable, and supplied Ripgrep hashes; gives
the child isolated home, XDG, application-data, cache, state, and temporary directories; and serves
a fail-closed OpenAI-compatible loopback provider. The fixed `--title` is part of the gate: an extra
title-generation request fails the run.

Claude and Codex live proofs call their configured model providers. Run the complete gate only when
the repository owner has approved that disclosure.

```powershell
node DynaDocs.Tests/HostCanaries/run-host-canaries.mjs `
  --candidate . `
  --evidence dydo/_system/.local/DYD-91-host-canaries `
  --opencode-archive C:\path\to\opencode-windows-x64.zip `
  --ripgrep C:\path\to\rg.exe
```

For local harness development, `--only claude`, `--only codex`, or `--only opencode` narrows the run.
That option is not a substitute for the complete final gate. A failed or partial run keeps its
manifest and captured output, but never counts as acceptance evidence.

The successful packet contains `manifest.json`, the two Claude streams, Codex inventory/prompt/live
records, OpenCode inventory/live records, and the loopback provider's complete request log. The
manifest records the exact candidate SHA, versions, pinned hashes, commands, durations, assertions,
and hashes for every evidence artifact.
