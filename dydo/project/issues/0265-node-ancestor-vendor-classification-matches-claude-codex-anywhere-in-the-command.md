---
title: Node-ancestor vendor classification matches 'claude'/'codex' anywhere in the command line - misclassifies unrelated node ancestors, fail-closed ownership refusal
id: 265
area: backend
type: issue
severity: low
status: open
found-by: inquisition
found-by-agent: Leo
found-by-vendor: claude
found-by-model: unknown
date: 2026-07-09
---

# Node-ancestor vendor classification matches 'claude'/'codex' anywhere in the command line - misclassifies unrelated node ancestors, fail-closed ownership refusal

ClassifyNodeCommandLine's unanchored token regexes match the bare vendor name in any path segment or argument (e.g. a working dir C:\dev\claude-tools or --out codex-dist), so an unrelated node ancestor is classified as an agent host, spuriously refusing ownership (whoami/role/release resolve null). Availability-only - over-classification can deny but never grant.

## Description

`ClassifyNodeCommandLine` (`Services/ProcessUtils.Ancestry.cs:270-289`) matches the bare vendor token anywhere in a Windows node ancestor's command line:

```
ClaudeCmdlineRegex = @"(?<![A-Za-z])claude(?![A-Za-z])"
CodexCmdlineRegex  = @"(?<![A-Za-z])codex(?![A-Za-z])"
```

These are unanchored token matches over the full command line, applied after only the `[\\/]bin[\\/]dydo` launcher exemption. So a path segment or argument such as a working directory `C:\dev\claude-tools\build.js` or `node build.js --out codex-dist` classifies an unrelated node process as an agent host. Empirically verified with the actual .NET regexes: `node C:\dev\claude-tools\build.js` → ClaudeHost; `node build.js --out codex-dist` → CodexHost (`claudia.js` correctly Transparent).

**Consequence (fail-closed availability, not privilege escalation):**
- In `NoForeignHostNearerThanClaimedHost`, over-classification refuses ownership for a legitimate caller whose ancestry contains such a node process — `dydo whoami`/role/release resolve to null (`IsOwnedByNearestHostCaller` returns false).
- In `FindClaudeAncestor`/`FindCodexAncestor`, it returns the wrong PID as the host ancestor, which then fails the `ancestor == claimedPid` check.

Over-classification can only DENY, never GRANT (grant requires PID equality with the claimed host), so this is availability-only. It triggers only when such a node process is a genuine nearer ancestor of the dydo call — uncommon in the real chain — hence low. Flagged because it sits on the same identity seam this campaign hardened, and the launcher exemption regex only whitelists `bin/dydo`, not arbitrary node cmdlines containing the vendor name.

**Fix note:** the regex cannot be trivially anchored — the intended real match (`...\.claude\...\@anthropic-ai\claude-code\cli.js`) names the vendor only in path segments. The existing test theory (`ProcessUtilsTests.cs:354-369`) covers token boundaries but has no unrelated-path-with-bare-token case.

Found by the v2.0.6 campaign inquisition (security lens); adversarially verified.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)