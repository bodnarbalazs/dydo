---
area: general
type: changelog
date: 2026-04-15
---

# Task: fix-dydo-v1.3-usage-bugs

Fixed three dydo CLI bugs reported from LC v1.3 migration (see dydo/agents/Adele/brief-fix-dydo-v1.3-usage-bugs.md for full plan). Bug 1: FixFileHandler.FixNaming now returns (renamed, conflicts), pre-checks File.Exists, catches IOException, and continues on collision; FixCommand consumes conflicts into manual-fix list and returns ExitCodes.ValidationErrors when any conflict occurs. Bug 2: AgentRegistry constructor and three RolesCommand subcommands now walk to project root via PathUtils.FindProjectRoot() before falling back to cwd. Bug 3: IndexGenerator.Generate now emits 'area: general, type: hub' frontmatter. Tests: 4 new (2 FixFileHandler, 1 IndexGenerator, 1 PathUtilsDiscovery). 3690/3690 pass, gap_check 100%. One plan deviation: replaced stderr-capture AgentRegistry test with a PathUtilsDiscovery primitive test — Console.SetError is process-wide and captured stderr from concurrent tests, causing flaky failure under parallel load. The discovery-primitive test covers the same root-cause behavior without cross-test pollution.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed three dydo CLI bugs reported from LC v1.3 migration (see dydo/agents/Adele/brief-fix-dydo-v1.3-usage-bugs.md for full plan). Bug 1: FixFileHandler.FixNaming now returns (renamed, conflicts), pre-checks File.Exists, catches IOException, and continues on collision; FixCommand consumes conflicts into manual-fix list and returns ExitCodes.ValidationErrors when any conflict occurs. Bug 2: AgentRegistry constructor and three RolesCommand subcommands now walk to project root via PathUtils.FindProjectRoot() before falling back to cwd. Bug 3: IndexGenerator.Generate now emits 'area: general, type: hub' frontmatter. Tests: 4 new (2 FixFileHandler, 1 IndexGenerator, 1 PathUtilsDiscovery). 3690/3690 pass, gap_check 100%. One plan deviation: replaced stderr-capture AgentRegistry test with a PathUtilsDiscovery primitive test — Console.SetError is process-wide and captured stderr from concurrent tests, causing flaky failure under parallel load. The discovery-primitive test covers the same root-cause behavior without cross-test pollution.

## Code Review

- Reviewed by: Brian
- Date: 2026-04-14 18:09
- Result: PASSED
- Notes: LGTM. Three bugs fixed surgically per plan. FixFileHandler now returns (renamed, conflicts) with pre-check + IOException catch, FixCommand consumes conflicts and returns ValidationErrors. AgentRegistry and RolesCommand reuse PathUtils.FindProjectRoot() with cwd fallback. IndexGenerator emits 'area: general, type: hub' frontmatter. PathUtilsDiscovery substitution for the stderr-capture test is justified (process-wide Console.SetError was flaky under parallel load) and covers the same root primitive. 3690/3690 pass, gap_check 100%.

Awaiting human approval.

## Approval

- Approved: 2026-04-15 16:19
