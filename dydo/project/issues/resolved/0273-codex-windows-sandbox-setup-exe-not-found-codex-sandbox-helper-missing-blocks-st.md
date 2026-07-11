---
title: codex-windows-sandbox-setup.exe not found - codex sandbox helper missing blocks Start-Process/rg/sandbox-mode commands in dispatched codex sessions
id: 273
area: backend
type: issue
severity: high
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
resolved-date: 2026-07-11
---

# codex-windows-sandbox-setup.exe not found - codex sandbox helper missing blocks Start-Process/rg/sandbox-mode commands in dispatched codex sessions

c1-8 acceptance smoke (2026-07-11, HEAD build): a dispatched codex session under the guard repeatedly failed sandbox-dependent commands with 'windows sandbox: orchestrator_helper_launch_failed: setup refresh failed to launch helper: helper=codex-windows-sandbox-setup.exe ... error=program not found' (log ~/.codex/.sandbox/sandbox.<date>.log). Blocked: Start-Process (the agent's background wait), rg, and any sandbox-mode command. This is codex-SIDE (the sandbox helper exe is absent from this codex install), not dydo - but it BLOCKS codex from doing real workhorse commands, so it is a hard prerequisite for the codex-as-workhorse goal. Also seen in Noah's 2026-07-09 MCP exploration (same helper-not-found). It is the concrete thing c1-4's DefaultSandboxPrerequisite pass-through must eventually probe (issue-adjacent). Fix directions: (1) determine whether the helper ships with the codex CLI and is installable/repairable (codex reinstall, a codex setup command, or a config pointing at the elevated-sandbox path - config shows [windows] sandbox='elevated'); (2) dydo's DefaultSandboxPrerequisite probe should detect the missing helper and fail-fast at DISPATCH with the fix instruction rather than letting the codex session discover it mid-run; (3) document as a codex-host prerequisite. Investigate whether [windows] sandbox setting or an admin-approved first-run resolves it. Found by balazs+Adele in c1-8.

## Description

**Root cause (web research 2026-07-11): KNOWN upstream codex CLI bug, not dydo.** The Windows
sandbox setup helper fails to launch due to a bin-junction / PATH / entry-point resolution defect
where codex cannot find its own bundled `codex-windows-sandbox-setup.exe` under the active
standalone package's `codex-resources` dir, falling back to a bare filename Windows can't resolve.
Filed repeatedly upstream: openai/codex #30829 (clean-install bin junction — the one Noah hit),
#28457 (0.140 standalone launcher can't resolve helpers), #29418, #27125, #28278. Regression across
0.132–0.144.x. **Why it matters for us:** the Windows sandbox IS codex's auto-approval mechanism —
`--sandbox workspace-write` (our 0253 posture) auto-runs in-workspace commands via the sandbox and
only prompts on boundary-crossing. No working sandbox ⇒ no "auto mode" ⇒ codex either fails
sandbox commands or falls back to approving everything. So 0273 is the literal blocker to the
codex-as-workhorse auto mode balazs wants.

**Fix options (host-side, balazs), easiest first:**
1. **Elevated setup with admin approval** — codex's elevated sandbox needs a one-time
   admin-provisioned setup. Run `codex` in a repo and approve the admin/UAC prompt when the sandbox
   setup fires. Often the whole fix (the helper couldn't launch elevated).
2. **Unelevated fallback** — set `[windows] sandbox = "unelevated"` (currently `elevated`). Weaker
   isolation (restricted token + ACL boundaries, no admin setup) but sidesteps the missing elevated
   helper. The dydo guard remains the project-boundary layer regardless.
3. **Reinstall/update codex CLI** — the bin-junction that breaks helper discovery is often re-linked
   by a clean reinstall; a newer codex may carry the upstream fix.

**dydo-side follow-up (not the fix, but hardening):** c1-4's `DefaultSandboxPrerequisite` is a
pass-through today — it should actually probe for the helper/sandbox readiness and fail-fast at
DISPATCH with this fix instruction, rather than letting the codex session discover it mid-run.
Refs: developers.openai.com/codex/windows (elevated vs unelevated), openai/codex #30829.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-11 by a dispatched CODEX agent (Leo) fixing its own sandbox - the first real codex work task, done in auto mode under the guard. Root cause on this machine: bin-junction layout bug - codex 0.144.1 standalone launched from the Programs\OpenAI\Codex\bin junction, but the version-matched helpers (codex-windows-sandbox-setup.exe, codex-command-runner.exe) existed ONLY under ~/.codex/packages/standalone/releases/0.144.1-.../codex-resources and the desktop-app bin, NOT next to the standalone codex.exe, and PATH lacked codex-resources -> bare-filename helper lookup failed 'program not found' (then command-runner failed CreateProcessWithLogonW=2). Fix (host-level, no repo/dydo edits): hardlinked both 0.144.1 helpers into the active release bin (releases/0.144.1-.../bin) AND codex-path dir (already on session PATH); added .../current/codex-resources to User PATH for future launches. Kept [windows] sandbox=elevated; elevated setup then processed workspace+temp write roots with errors=[]. VERIFIED: rg, Start-Process, and a workspace Set-Content write all run WITHOUT per-action approval (auto mode); an outside-workspace write was DENIED by the sandbox (real isolation, not bypass) and only succeeded with an explicit escalated+approved probe. balazs approvals were only for the one-time host-repair diagnosis + the intentional boundary probe. This is the documented codex-host setup fix. Upstream bug refs remain openai/codex #30829/#28457. Follow-up: c1-4 DefaultSandboxPrerequisite should probe helper readiness + fail-fast at dispatch (tracked in the issue body).