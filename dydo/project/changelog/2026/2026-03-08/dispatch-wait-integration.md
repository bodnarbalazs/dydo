---
area: general
type: changelog
date: 2026-03-08
---

# Task: dispatch-wait-integration

Add `--wait`/`--no-wait` as required flags on `dydo dispatch`. `--wait` creates a wait marker and enters a poll loop (combining dispatch + wait). `--no-wait` returns immediately with a release hint when appropriate. Add double-dispatch protection, wait marker infrastructure for release blocking, channel isolation in `dydo wait`, and `--cancel` support. Update all templates and docs.

## Progress

- [x] Plan written: `dydo/agents/Olivia/plan-dispatch-wait-integration.md`
- [ ] Step 1: Wait marker infrastructure (AgentRegistry)
- [ ] Step 2: Release blocking on markers
- [ ] Step 3: Double-dispatch protection
- [ ] Step 4: `--wait` / `--no-wait` flags on dispatch
- [ ] Step 5: `--wait` poll loop behavior
- [ ] Step 6: `--no-wait` release hint
- [ ] Step 7: Wait command `--cancel`
- [ ] Step 8: Channel isolation in general wait
- [ ] Step 9: Update existing tests
- [ ] Step 10: New test files
- [ ] Step 11: Documentation updates

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\WaitMarker.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WaitCommand.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WaitCommandTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IAgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\DispatchCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorkflowTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\agent-workflow.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-code-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-planner.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\coding-standards.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-docs-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-tester.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-co-thinker.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-interviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\about-dynadocs.template.md — Modified


## Review Summary

(Pending)

## Approval

- Approved: 2026-03-08 20:25
