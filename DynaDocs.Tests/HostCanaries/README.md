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
fully matched read-only command approval is accepted for the derived resource read. The isolated-HOME
Claude leg is retired (DYD-200): Claude Code discovery is proven by direct observation instead,
recorded on the Issue (Linear comment "Claude Code host discovery", 2026-09-16, DYD-200 Canceled).
This recorder no longer runs that Claude leg on purpose - but a bare invocation of the command below
with no `--only` flag still executes it, and that leg calls the configured live model provider. That
is why the gate is documented and run as the two narrowed invocations below, each with an explicit
`--only`, and never as the bare command.

The gate is run as two separate invocations of the same command, once with `--only codex` and once
with `--only opencode`:

```powershell
node DynaDocs.Tests/HostCanaries/run-host-canaries.mjs `
  --candidate . `
  --scratch-root "$env:SystemDrive\dydo-canary-scratch" `
  --evidence "$env:SystemDrive\dydo-canary-evidence\DYD-91\<candidate-sha>-<run-id>\staging\host-canaries" `
  --opencode-archive "C:\path\to\opencode-windows-x64.zip" `
  --ripgrep "C:\path\to\rg.exe" `
  --only codex
```

then again with `--only opencode` in place of `--only codex`. `--only` takes exactly one of `claude`,
`codex`, `opencode`; any other value, including a comma list, is rejected before any leg starts. A
failed or partial run keeps its external manifest and captured output, but never counts as acceptance
evidence.
The disposable run is removed after post-run fingerprint and nested-Git checks; caller-owned scratch
and retained evidence roots are preserved.

The successful packet for these two runs together contains `manifest.json`, Codex inventory/live
records, OpenCode inventory/live records, and both loopback providers' complete request logs. The
manifest records the exact candidate SHA, versions, pinned hashes, commands, durations, assertions,
and hashes for every evidence artifact. Proxy evidence proves zero proxy-observed external attempts
and exact loopback traffic; it is not OS-level network confinement and makes no stronger claim.

On 2026-09-16 the pinned Ripgrep hash was changed from `673c96c3...` to `7c9b1279...`. Codex had
rotated its vendored bin directory, so the previously pinned binary no longer existed; the surviving
binary at `C:\Users\User\AppData\Local\OpenAI\Codex\bin\4fe45441001f7a41\rg.exe` reports the same
`ripgrep 15.2.0 (rev e89fff89ac)` build and was re-pinned to its hash. The check stays a hard
equality assert that fails closed; re-pinning it is a human/admiral decision made outside the
runner, not something the runner does for itself.

On 2026-09-21 the pinned Ripgrep hash was changed from `7c9b1279...` to `14231169...`. Codex had
rotated its vendored bin directory again, so `7c9b1279...` no longer existed on this machine; the
surviving binary at `C:\Users\User\.codex\packages\standalone\releases\0.155.1-x86_64-pc-windows-msvc\codex-path\rg.exe`
reports the same `ripgrep 15.2.0 (rev e89fff89ac)`, PCRE2 10.45 build -- identical version and
revision to the outgoing pin's recorded build, and byte-identical across Codex standalone releases
0.145.0, 0.154.0, 0.155.0 and 0.155.1 -- and was re-pinned to its hash,
`14231169855ec5205cf5a1b6f1db358ff4aed4247c86b69ce8aae647c77f6680`. A different candidate seen at
the same time, `%LOCALAPPDATA%\OpenAI\Codex\bin\73fd6465d9c76545\rg.exe` (hash `96331974...`),
reports `ripgrep 15.2.0` with no revision string and PCRE2 10.48 -- a different build -- and was not
eligible.

The check stays a hard equality assert that fails closed. A binary of the *same* version and
revision, taken from Codex's own vendored bin directory, may be re-pinned by a captain holding this
record -- the record above is what "holding the record" means: the outgoing and incoming hash, the
exact source path, and the reported version/revision proving the build did not change underneath
the pin. A binary reporting a *different* version or revision is not a re-pin; it is an admiral
decision, made outside the runner exactly as the 2026-09-16 note above already required.

## The guard hook and the candidate fingerprint

From DYD-500 on, the candidate's committed `.claude/settings.json` runs `dydo guard --host claude`
before every Claude Read. The Claude leg keeps that hook on: it passes no `disableAllHooks` and no settings
override, and never rewrites the settings file. The first guard call of the day writes the gitignored
`dydo/_system/.local/last-validation` into the checkout, and the run fingerprints every byte but the
top-level `.git`, so that write used to fail the run.

Decision (DYD-545): pre-warm the guard. When the Claude leg is selected (`--only claude` or a bare
run), `candidate-fingerprint.mjs` runs the same hook command once, through a shell, with a candidate
Read payload and the checkout as cwd, before the "before" fingerprint is taken. The guard's only
Read-time write is that daily validation, and it does not repeat within 24 hours, so the sessions'
own hook Reads leave the bytes unchanged. Any other write, including a future per-call guard write,
still fails the run. Two options were rejected: excluding `dydo/_system/.local/` from the
fingerprint, because an exclusion ignores writes, and disabling project hooks, because that weakens
the hook.

The Claude leg needs `dydo` on the PATH it inherits. The pre-warm resolves it first and fails with a
named error when it is missing, so the leg never runs with a broken hook. The manifest and
`claude-guard-prewarm.json` record the resolved path and version, plus the pre-warm's exit code and
stderr. A non-zero exit fails the run.

The Codex leg runs no project hook. On 2026-11-05, codex-cli 0.166.0 on Linux was started as
`app-server` with the leg's isolated `CODEX_HOME` config and a restricted PATH. Its `hooks/list` for
a materialized candidate returned no hooks. As a positive control, the same call with the checkout
marked as a trusted project listed `.codex/hooks.json`'s `dydo guard` PreToolUse hook as `project`
with trust status `untrusted`: Codex runs a project hook only for a trusted project whose hook hash
is trusted in `config.toml`, and the isolated home has neither. So the pre-warm rule does not apply
to Codex. The probe did not run the pinned Windows `codex.exe`, so recheck it there.

The expected skill count is derived, not pinned: the run counts the source commit's tracked
`skills/**/SKILL.md` from `git ls-tree`, requires it to be nonzero, and requires the loaded inventory
to match it.
