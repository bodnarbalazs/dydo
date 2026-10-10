---
area: project
type: reference
---

# File Coverage Heatmap

Auto-generated heatmap of inquisition coverage per source file over a 365-day lookback. Each file gets a score reflecting how recently and how heavily it has been touched by inquisition activity; the table groups files by folder and flags gaps (zero-score) and stale (decayed) entries to inform what to investigate next.

Generated: 2026-04-08 21:03 UTC
Lookback: 365 days

## Summary

- **Total files:** 240
- **Covered (7+):** 3
- **Low (1-6):** 42
- **Gaps (0):** 195
- **Stale (decayed):** 17

## Files

### ./ (0/1 covered, avg 0.0)

- [gap] `Program.cs` - score 0
### Commands/ (1/56 covered, avg 0.4)

- [gap] `AgentCommand.cs` - score 0
- [gap] `AgentListHandler.cs` - score 0
- [gap] `AgentManagementHandlers.cs` - score 0
- [gap] `AgentTreeHandler.cs` - score 0
- [gap] `AuditCommand.cs` - score 0
- [gap] `CheckAgentValidator.cs` - score 0
- [gap] `CheckCommand.cs` - score 0
- [gap] `CheckDocValidator.cs` - score 0
- [gap] `CleanCommand.cs` - score 0
- [gap] `CompleteCommand.cs` - score 0
- [gap] `CompletionsCommand.cs` - score 0
- [gap] `DispatchCommand.cs` - score 0
- [gap] `FixCommand.cs` - score 0
- [gap] `FixFileHandler.cs` - score 0
- [gap] `FixHubHandler.cs` - score 0
- [gap] `GraphCommand.cs` - score 0
- [gap] `GraphDisplayHandler.cs` - score 0
- [gap] `GuardLiftCommand.cs` - score 0
- [gap] `InboxCommand.cs` - score 0
- [gap] `IndexCommand.cs` - score 0
- [gap] `InitCommand.cs` - score 0
- [gap] `IssueCommand.cs` - score 0
- [gap] `IssueCreateHandler.cs` - score 0
- [gap] `IssueListHandler.cs` - score 0
- [gap] `IssueResolveHandler.cs` - score 0
- [gap] `MessageCommand.cs` - score 0
- [gap] `QueueCommand.cs` - score 0
- [gap] `ReviewCommand.cs` - score 0
- [gap] `smoke-comp-a.txt` - score 0
- [gap] `smoke-comp-b.txt` - score 0
- [gap] `smoke-final-a.txt` - score 0
- [gap] `smoke-final-b.txt` - score 0
- [gap] `smoke-final2-a.txt` - score 0
- [gap] `smoke-final2-b.txt` - score 0
- [gap] `smoke-final3-a.txt` - score 0
- [gap] `smoke-final3-b.txt` - score 0
- [gap] `smoke-final4-a.txt` - score 0
- [gap] `smoke-final4-b.txt` - score 0
- [gap] `smoke-test-v15.txt` - score 0
- [gap] `TaskApproveHandler.cs` - score 0
- [gap] `TaskCommand.cs` - score 0
- [gap] `TaskCompactHandler.cs` - score 0
- [gap] `TaskListHandler.cs` - score 0
- [gap] `TaskReviewHandler.cs` - score 0
- [gap] `TemplateCommand.cs` - score 0
- [gap] `ValidateCommand.cs` - score 0
- [gap] `WatchdogCommand.cs` - score 0
- [gap] `WorkspaceCommand.cs` - score 0
- [gap] `WorktreeCommand.cs` - score 0
- [low] `AgentLifecycleHandlers.cs` - score 5
- [low] `InquisitionCommand.cs` - score 1
- [low] `RolesCommand.cs` - score 1
- [low] `TaskCreateHandler.cs` - score 1
- [low] `WaitCommand.cs` - score 2
- [low] `WhoamiCommand.cs` - score 2
- [covered] `GuardCommand.cs` - score 12
### Models/ (0/51 covered, avg 0.5)

- [gap] `AgentsConfig.cs` - score 0
- [gap] `AuditEvent.cs` - score 0
- [gap] `AuditSession.cs` - score 0
- [gap] `CompactionResult.cs` - score 0
- [gap] `DispatchConfig.cs` - score 0
- [gap] `DispatchMarker.cs` - score 0
- [gap] `DispatchOptions.cs` - score 0
- [gap] `DocFile.cs` - score 0
- [gap] `DydoConfig.cs` - score 0
- [gap] `FileCoverageEntry.cs` - score 0
- [gap] `FileCoverageOptions.cs` - score 0
- [gap] `FileCoverageReport.cs` - score 0
- [gap] `FolderCoverage.cs` - score 0
- [gap] `Frontmatter.cs` - score 0
- [gap] `GuardLiftMarker.cs` - score 0
- [gap] `HookInputExtensions.cs` - score 0
- [gap] `IssueFoundBy.cs` - score 0
- [gap] `IssueSeverity.cs` - score 0
- [gap] `IssueStatus.cs` - score 0
- [gap] `LinkInfo.cs` - score 0
- [gap] `LinkType.cs` - score 0
- [gap] `NudgeConfig.cs` - score 0
- [gap] `PathsConfig.cs` - score 0
- [gap] `PresetAgentNames.cs` - score 0
- [gap] `ProjectSnapshot.cs` - score 0
- [gap] `ReplyPendingMarker.cs` - score 0
- [gap] `SnapshotBaseline.cs` - score 0
- [gap] `SnapshotDelta.cs` - score 0
- [gap] `SnapshotRef.cs` - score 0
- [gap] `StructureConfig.cs` - score 0
- [gap] `TaskFile.cs` - score 0
- [gap] `TasksConfig.cs` - score 0
- [gap] `TaskStatus.cs` - score 0
- [gap] `ToolInputData.cs` - score 0
- [gap] `ValidationIssue.cs` - score 0
- [gap] `ValidationResult.cs` - score 0
- [gap] `Violation.cs` - score 0
- [gap] `ViolationSeverity.cs` - score 0
- [gap] `WaitMarker.cs` - score 0
- [low] `AgentSession.cs` - score 2
- [low] `AgentState.cs` - score 5
- [low] `AgentStatus.cs` - score 3
- [low] `ConditionalMustRead.cs` - score 1
- [low] `ConditionalMustReadCondition.cs` - score 1
- [low] `HookInput.cs` - score 1
- [low] `InboxItem.cs` - score 1
- [low] `QueueActiveEntry.cs` - score 1
- [low] `QueueEntry.cs` - score 1
- [low] `QueueResult.cs` - score 1
- [low] `RoleConstraint.cs` - score 3
- [low] `RoleDefinition.cs` - score 3
### Rules/ (0/12 covered, avg 0.0)

- [gap] `BrokenLinksRule.cs` - score 0
- [gap] `FolderMetaFilesRule.cs` - score 0
- [gap] `FrontmatterRule.cs` - score 0
- [gap] `HubFilesRule.cs` - score 0
- [gap] `IRule.cs` - score 0
- [gap] `NamingRule.cs` - score 0
- [gap] `OffLimitsRule.cs` - score 0
- [gap] `OrphanDocsRule.cs` - score 0
- [gap] `RelativeLinksRule.cs` - score 0
- [gap] `RuleBase.cs` - score 0
- [gap] `SummaryRule.cs` - score 0
- [gap] `UncustomizedDocsRule.cs` - score 0
### Serialization/ (0/1 covered, avg 1.0)

- [low] `DydoJsonContext.cs` - score 1
### Services/ (2/72 covered, avg 0.9)

- [gap] `AnchorExtractor.cs` - score 0
- [gap] `AssemblyAttributes.cs` - score 0
- [gap] `AuditService.cs` - score 0
- [gap] `AuditVisualizationService.cs` - score 0
- [gap] `BashCommandAnalyzer.cs` - score 0
- [gap] `CompletionProvider.cs` - score 0
- [gap] `ConfigFactory.cs` - score 0
- [gap] `ConfigFileLocator.cs` - score 0
- [gap] `ConfigService.cs` - score 0
- [gap] `DocGraph.cs` - score 0
- [gap] `DocLinkResolver.cs` - score 0
- [gap] `DocScanner.cs` - score 0
- [gap] `FileCoverageService.cs` - score 0
- [gap] `FolderScaffolder.cs` - score 0
- [gap] `FrontmatterExtractor.cs` - score 0
- [gap] `GuardLiftService.cs` - score 0
- [gap] `HubGenerator.cs` - score 0
- [gap] `IAuditService.cs` - score 0
- [gap] `IBashCommandAnalyzer.cs` - score 0
- [gap] `IConfigService.cs` - score 0
- [gap] `IDocGraph.cs` - score 0
- [gap] `IDocScanner.cs` - score 0
- [gap] `IFileCoverageService.cs` - score 0
- [gap] `IFolderScaffolder.cs` - score 0
- [gap] `IIndexGenerator.cs` - score 0
- [gap] `ILinkResolver.cs` - score 0
- [gap] `IMarkdownParser.cs` - score 0
- [gap] `InboxItemParser.cs` - score 0
- [gap] `IncludeReanchor.cs` - score 0
- [gap] `IndexGenerator.cs` - score 0
- [gap] `IOffLimitsService.cs` - score 0
- [gap] `IProcessStarter.cs` - score 0
- [gap] `ISnapshotService.cs` - score 0
- [gap] `ITerminalDetector.cs` - score 0
- [gap] `IValidationService.cs` - score 0
- [gap] `LinkExtractor.cs` - score 0
- [gap] `LinkResolver.cs` - score 0
- [gap] `LinuxTerminalLauncher.cs` - score 0
- [gap] `MacTerminalLauncher.cs` - score 0
- [gap] `MarkdownParser.cs` - score 0
- [gap] `MessageFinder.cs` - score 0
- [gap] `ProcessUtils.Ancestry.cs` - score 0
- [gap] `ProcessUtils.CommandLine.cs` - score 0
- [gap] `QueueService.cs` - score 0
- [gap] `ShellCompletionInstaller.cs` - score 0
- [gap] `SnapshotCompactionService.cs` - score 0
- [gap] `SnapshotService.cs` - score 0
- [gap] `TemplateGenerator.cs` - score 0
- [gap] `WatchdogService.cs` - score 0
- [low] `AgentClaimValidator.cs` - score 1
- [low] `AgentCrudOperations.cs` - score 3
- [low] `AgentSelector.cs` - score 2
- [low] `AgentSessionManager.cs` - score 1
- [low] `AgentStateStore.cs` - score 2
- [low] `IAgentRegistry.cs` - score 2
- [low] `InboxMetadataReader.cs` - score 2
- [low] `InboxService.cs` - score 3
- [low] `IRoleDefinitionService.cs` - score 1
- [low] `MessageService.cs` - score 1
- [low] `MustReadTracker.cs` - score 3
- [low] `OffLimitsService.cs` - score 1
- [low] `PathPermissionChecker.cs` - score 3
- [low] `ProcessUtils.cs` - score 3
- [low] `RoleConstraintEvaluator.cs` - score 4
- [low] `RoleDefinitionService.cs` - score 3
- [low] `TerminalLauncher.cs` - score 1
- [low] `ValidationService.cs` - score 3
- [low] `WindowsTerminalLauncher.cs` - score 3
- [low] `WorkspaceArchiver.cs` - score 3
- [low] `WorkspaceCleaner.cs` - score 3
- [covered] `AgentRegistry.cs` - score 12
- [covered] `DispatchService.cs` - score 8
### Templates/ (0/39 covered, avg 0.0)

- [gap] `_changelog.template.md` - score 0
- [gap] `_decisions.template.md` - score 0
- [gap] `_guides.template.md` - score 0
- [gap] `_issues.template.md` - score 0
- [gap] `_pitfalls.template.md` - score 0
- [gap] `_project.template.md` - score 0
- [gap] `_reference.template.md` - score 0
- [gap] `_tasks.template.md` - score 0
- [gap] `_understand.template.md` - score 0
- [gap] `about-dynadocs.template.md` - score 0
- [gap] `about.template.md` - score 0
- [gap] `agent-states.template.md` - score 0
- [gap] `agent-workflow.template.md` - score 0
- [gap] `architecture.template.md` - score 0
- [gap] `changelog.template.md` - score 0
- [gap] `coding-standards.template.md` - score 0
- [gap] `decision.template.md` - score 0
- [gap] `dydo-commands.template.md` - score 0
- [gap] `extra-verify.example.md` - score 0
- [gap] `files-off-limits.template.md` - score 0
- [gap] `glossary.template.md` - score 0
- [gap] `how-to-merge-worktrees.template.md` - score 0
- [gap] `how-to-review-worktree-merges.template.md` - score 0
- [gap] `how-to-use-docs.template.md` - score 0
- [gap] `index.template.md` - score 0
- [gap] `mode-co-thinker.template.md` - score 0
- [gap] `mode-code-writer.template.md` - score 0
- [gap] `mode-docs-writer.template.md` - score 0
- [gap] `mode-inquisitor.template.md` - score 0
- [gap] `mode-judge.template.md` - score 0
- [gap] `mode-orchestrator.template.md` - score 0
- [gap] `mode-planner.template.md` - score 0
- [gap] `mode-reviewer.template.md` - score 0
- [gap] `mode-test-writer.template.md` - score 0
- [gap] `pitfall.template.md` - score 0
- [gap] `template-additions-readme.md` - score 0
- [gap] `welcome.template.md` - score 0
- [gap] `writing-docs.template.md` - score 0

  ### Templates/Assets/ (0/1 covered, avg 0.0)

  - [gap] `dydo-diagram.svg` - score 0
### Utils/ (0/8 covered, avg 0.8)

- [gap] `ConsoleOutput.cs` - score 0
- [gap] `ExitCodes.cs` - score 0
- [gap] `FileLock.cs` - score 0
- [gap] `FileReadRetry.cs` - score 0
- [gap] `FrontmatterParser.cs` - score 0
- [gap] `PathUtils.Discovery.cs` - score 0
- [low] `GlobMatcher.cs` - score 3
- [low] `PathUtils.cs` - score 3
