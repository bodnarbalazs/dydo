---
title: dydo dispatch --codex does not pass the tier-resolved model to the codex launch - dispatched sessions run codex's config default (gpt-5.5), ignoring the openai tier (Sol/Terra/Luna)
id: 277
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
found-by-agent: Adele
found-by-vendor: claude
found-by-model: claude
date: 2026-07-11
resolved-date: 2026-07-12
---

# dydo dispatch --codex does not pass the tier-resolved model to the codex launch - dispatched sessions run codex's config default (gpt-5.5), ignoring the openai tier (Sol/Terra/Luna)

Observed live 2026-07-11: dispatched codex sessions run gpt-5.5 (codex config default, 'high fast') instead of the dydo-configured openai tier model (gpt-5.6-terra for code-writer=standard). Root cause: Services/TerminalLauncher.CodexLaunchPosture/GetCodexCommand emit 'codex --sandbox X --ask-for-approval Y "<prompt>"' with NO -m/--model flag, so codex falls back to ~/.codex/config.toml model = 'gpt-5.5'. The openai tier mapping (models.tiers.openai strong=gpt-5.6-sol/standard=gpt-5.6-terra/light=gpt-5.6-luna) IS applied to the compiled subagent role tomls (.codex/agents/*.toml carry model=gpt-5.6-terra) but NOT to the dispatched Tier-1 session, which is what actually does the work. Consequence: the whole Sol/Terra/Luna config does not drive the workhorse sessions, cost/quality is on the wrong (older) model, and the DR-037 speed/quality measurement would be on gpt-5.5 not Terra. Fix: dispatch --codex --role R knows the role -> resolve role->tier->model (via models.roles + models.tiers.openai) and emit 'codex -m <resolved-model> --sandbox ... --ask-for-approval ... "<prompt>"'; mirror the claude side's tier resolution. Verify -m is the correct codex flag (codex --model / -m). Interim workaround: set model=gpt-5.6-terra in ~/.codex/config.toml (fleet-wide default, not per-role). Route to the codex-workhorse path BEFORE scaling the big orchestration so the fleet runs the intended tier. Found by balazs+Adele.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

RESOLVED 2026-07-11: landed fbaed15a. dydo dispatch --codex --role R now resolves role->tier->openai model (code-writer->gpt-5.6-terra via SyncCommand.ResolveModel) and emits -m on fresh launch AND crash-resume (all 3 platforms), unmapped-role falls back to gpt-5.5. SECURITY: single validated emission point (IsValidCodexModel char-whitelist rejects shell metachars/spaces/quotes/newlines), preflight CheckCodexModelValid fails fast, TOCTOU re-validated at launch. Codex Sam (2 rounds): round-1 passed 685 tests but Claude review FAILED it on a raw -m interpolation shell-injection blocker (x; rm -rf ~ # in dydo.json); round-2 closed it, Claude review PASS (injection verified closed on all paths, 4760/4760). The cross-vendor review caught an RCE-class hole before a security release - the loop working. Follow-up nits in 0283.