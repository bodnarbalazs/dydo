---
area: general
type: changelog
date: 2026-03-08
---

# Task: agent-messaging

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\WaitMarker.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WaitCommand.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WaitCommandTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Commands\MessageCommand.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\MessageIntegrationTests.cs — Created
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
C:\Users\User\Desktop\Projects\DynaDocs\Models\InboxItem.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\AgentState.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InboxCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandSmokeTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\HelpCommandTests.cs — Modified


## Review Summary

Implemented agent messaging: dydo message/msg command for sending messages between agents, dydo wait command for blocking until a message arrives, guard notification that blocks tool calls when unread messages exist (with automatic clearing on read), inbox show differentiation between dispatch and message items. Added 32 new tests (all pass), updated 4 templates. One plan deviation: added unread-message blocking to the glob/grep and bash guard paths (plan only mentioned the non-bash path). Edge case noted by user handled: dydo wait checks for existing messages before entering poll loop.

## Code Review (2026-03-08 01:05)

- Reviewed by: Adele
- Result: FAILED
- Issues: Implementation is excellent: MessageCommand, WaitCommand, guard integration, inbox differentiation, 32 tests all passing. One issue that must be fixed: CommandDocConsistencyTests.BuildRootCommand() is missing MessageCommand and WaitCommand. This means the meta-tests (Tests 1-7) have a blind spot for the new messaging commands - docs, help text, templates, and examples for message/wait are not verified for consistency. Add both commands to BuildRootCommand() in CommandDocConsistencyTests.cs (lines 19-38).

Requires rework.

## Approval

- Approved: 2026-03-08 20:25
