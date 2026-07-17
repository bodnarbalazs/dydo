using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Models;

namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for the init command.
/// </summary>
[Collection("Integration")]
public class InitCommandTests : IntegrationTestBase
{
    #region Init None

    [Fact]
    public async Task Init_None_CreatesBasicStructure()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileExists("dydo.json");
        AssertFileExists("CLAUDE.md");
        AssertDirectoryExists("dydo");
        AssertDirectoryExists("dydo/understand");
        AssertDirectoryExists("dydo/guides");
        AssertDirectoryExists("dydo/reference");
        AssertDirectoryExists("dydo/project");
        AssertDirectoryExists("dydo/agents");
    }

    [Fact]
    public async Task Init_None_CreatesFoundationDocs()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileExists("dydo/index.md");
        AssertFileExists("dydo/welcome.md");
        AssertFileExists("dydo/files-off-limits.md");
        AssertFileExists("dydo/understand/about.md");
        AssertFileExists("dydo/understand/architecture.md");
        AssertFileExists("dydo/guides/coding-standards.md");
        AssertFileExists("dydo/guides/how-to-use-docs.md");
        AssertFileExists("dydo/reference/writing-docs.md");
    }

    [Fact]
    public async Task Init_None_CreatesEmptyAgentsWorkspaceRoot()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();

        // The 26-agent roster was removed (DR-041): init creates the empty, gitignored workspace
        // root but no per-agent workspaces / workflow files.
        AssertDirectoryExists("dydo/agents");
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/sample")));
    }

    [Fact]
    public async Task Init_None_CreatesCorrectConfig()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileContains("dydo.json", "\"version\": 1");
        // New inits no longer emit an agents/pool/assignments section (DR-041).
        Assert.DoesNotContain("\"agents\"", ReadFile("dydo.json"));
    }

    [Fact]
    public async Task Init_None_UpdatesGitignore()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileExists(".gitignore");
        AssertFileContains(".gitignore", "dydo/agents/");
        AssertFileContains(".gitignore", "dydo/_system/.local/");
    }

    [Fact]
    public async Task Init_None_ScaffoldsSystemLocalDir()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertDirectoryExists("dydo/_system/.local");
    }

    [Fact]
    public async Task Init_None_CreatesProjectSubfolderDocs()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();

        // Tasks folder — D4: no auto-generated _index.md; meta file still present.
        AssertFileExists("dydo/project/tasks/_tasks.md");

        // Decisions folder - hub and meta
        AssertFileExists("dydo/project/decisions/_index.md");
        AssertFileExists("dydo/project/decisions/_decisions.md");

        // Changelog folder - hub and meta
        AssertFileExists("dydo/project/changelog/_index.md");
        AssertFileExists("dydo/project/changelog/_changelog.md");

        // Pitfalls folder - hub and meta
        AssertFileExists("dydo/project/pitfalls/_index.md");
        AssertFileExists("dydo/project/pitfalls/_pitfalls.md");
    }

    [Fact]
    public async Task Init_None_MetaFilesHaveMeaningfulContent()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();

        // Tasks meta should describe task lifecycle
        AssertFileContains("dydo/project/tasks/_tasks.md", "Task Lifecycle");
        AssertFileContains("dydo/project/tasks/_tasks.md", "backlog");
        AssertFileContains("dydo/project/tasks/_tasks.md", "in-review");

        // Decisions meta should describe decision record format
        AssertFileContains("dydo/project/decisions/_decisions.md", "Decision Records");
        AssertFileContains("dydo/project/decisions/_decisions.md", "proposed");
        AssertFileContains("dydo/project/decisions/_decisions.md", "accepted");

        // Changelog meta should describe folder structure
        AssertFileContains("dydo/project/changelog/_changelog.md", "Chronological record");
        AssertFileContains("dydo/project/changelog/_changelog.md", "Files Changed");

        // Pitfalls meta should describe naming conventions
        AssertFileContains("dydo/project/pitfalls/_pitfalls.md", "Known gotchas");
        AssertFileContains("dydo/project/pitfalls/_pitfalls.md", "Symptoms");
    }

    #endregion

    #region Init Claude

    [Fact]
    public async Task Init_Claude_CreatesHooksConfig()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        AssertFileExists(".claude/settings.local.json");
        AssertFileContains(".claude/settings.local.json", "PreToolUse");
        AssertFileContains(".claude/settings.local.json", "dydo guard");
    }

    [Fact]
    public async Task Init_Codex_CreatesProjectAndHooksWithoutClaudeSettings()
    {
        var result = await InitProjectAsync("codex");

        result.AssertSuccess();
        AssertFileExists("dydo.json");
        AssertFileExists("dydo/index.md");
        AssertFileExists("AGENTS.md");
        AssertFileExists(".codex/hooks.json");
        AssertFileContains("AGENTS.md", "dydo/index.md");
        AssertFileContains("dydo.json", "\"codex\": true");
        result.AssertStdoutContains("AGENTS.md -> dydo/index.md");
        AssertFileNotExists(".claude/settings.local.json");
    }

    [Fact]
    public async Task Init_Codex_HooksContainPreToolUseDydoGuard()
    {
        var result = await InitProjectAsync("codex");

        result.AssertSuccess();

        var settings = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".codex/hooks.json")));
        Assert.Null(settings["PreToolUse"]);

        var hooks = Assert.IsType<JsonObject>(settings["hooks"]);
        var preToolUse = Assert.IsType<JsonArray>(hooks["PreToolUse"]);
        var preToolUseEntry = Assert.Single(preToolUse);
        Assert.NotNull(preToolUseEntry);
        var matcher = preToolUseEntry["matcher"]?.GetValue<string>();
        Assert.NotNull(matcher);
        Assert.Contains("Edit", matcher);
        Assert.Contains("Write", matcher);
        Assert.Contains("Read", matcher);
        Assert.Contains("Bash", matcher);
        Assert.Contains("PowerShell", matcher);
        Assert.Contains("Agent", matcher);
        Assert.Contains("EnterPlanMode", matcher);
        Assert.Contains("ExitPlanMode", matcher);
        Assert.Contains("NotebookEdit", matcher);
        Assert.Contains("AskUserQuestion", matcher);
        Assert.Contains("apply_patch", matcher);
        // Codex shell tool names (issue 0295) — without these the guard never sees codex shell
        // commands, so off-limits/dangerous-bash/git-safety don't bind on the codex shell lane.
        Assert.Contains("shell_command", matcher);
        Assert.Contains("exec", matcher);
        Assert.Contains("local_shell", matcher);
        Assert.Contains("unified_exec", matcher);

        var guardHooks = Assert.IsType<JsonArray>(preToolUseEntry["hooks"]);
        var guardHook = Assert.Single(guardHooks);
        Assert.NotNull(guardHook);
        Assert.Equal("command", guardHook["type"]?.GetValue<string>());
        Assert.Equal("dydo guard", guardHook["command"]?.GetValue<string>());

        var stop = Assert.IsType<JsonArray>(hooks["Stop"]);
        var stopEntry = Assert.Single(stop);
        Assert.NotNull(stopEntry);
        var stopHooks = Assert.IsType<JsonArray>(stopEntry["hooks"]);
        var stopHook = Assert.Single(stopHooks);
        Assert.NotNull(stopHook);
        Assert.Equal("dydo guard --stop", stopHook["command"]?.GetValue<string>());
    }

    [Fact]
    public async Task Init_Codex_PreservesExistingHooks()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/hooks.json", """
            {
              "hooks": {
                "PreToolUse": [
                  {
                    "matcher": "CustomTool",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo custom"
                      }
                    ]
                  }
                ],
                "PostToolUse": [
                  {
                    "matcher": "AnyTool",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo post"
                      }
                    ]
                  }
                ]
              },
              "otherSetting": true
            }
            """);

        var result = await InitProjectAsync("codex");

        result.AssertSuccess();
        var content = ReadFile(".codex/hooks.json");
        Assert.Contains("CustomTool", content);
        Assert.Contains("echo custom", content);
        Assert.Contains("PostToolUse", content);
        Assert.Contains("echo post", content);
        Assert.Contains("otherSetting", content);
        Assert.Contains("dydo guard", content);
        Assert.Contains("dydo guard --stop", content);
    }

    [Fact]
    public async Task Init_Codex_PreservesCustomHooksWhenRemovingDydoHooks()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/hooks.json", """
            {
              "hooks": {
                "PreToolUse": [
                  {
                    "matcher": "CustomSubstring",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo before dydo guard after"
                      }
                    ]
                  },
                  {
                    "matcher": "Mixed",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "dydo guard"
                      },
                      {
                        "type": "command",
                        "command": "echo custom"
                      }
                    ]
                  },
                  {
                    "matcher": "ManagedOnly",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "dydo guard"
                      }
                    ]
                  }
                ],
                "Stop": [
                  {
                    "hooks": [
                      {
                        "type": "command",
                        "command": "dydo guard --stop"
                      },
                      {
                        "type": "command",
                        "command": "echo stop custom"
                      }
                    ]
                  },
                  {
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo before dydo guard --stop after"
                      }
                    ]
                  }
                ]
              }
            }
            """);

        var result = await InitProjectAsync("codex");

        result.AssertSuccess();

        var settings = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".codex/hooks.json")));
        var hooks = Assert.IsType<JsonObject>(settings["hooks"]);
        var preToolUse = Assert.IsType<JsonArray>(hooks["PreToolUse"]);
        Assert.Contains(preToolUse, entry =>
            entry?["matcher"]?.GetValue<string>() == "CustomSubstring" &&
            HookCommands(entry).Contains("echo before dydo guard after"));
        Assert.Contains(preToolUse, entry =>
            entry?["matcher"]?.GetValue<string>() == "Mixed" &&
            HookCommands(entry).SequenceEqual(["echo custom"]));
        Assert.DoesNotContain(preToolUse, entry =>
            entry?["matcher"]?.GetValue<string>() == "ManagedOnly");
        Assert.Equal(1, CountExactHookCommand(preToolUse, "dydo guard"));

        var stop = Assert.IsType<JsonArray>(hooks["Stop"]);
        Assert.Contains(stop, entry => HookCommands(entry).SequenceEqual(["echo stop custom"]));
        Assert.Contains(stop, entry => HookCommands(entry).Contains("echo before dydo guard --stop after"));
        Assert.Equal(1, CountExactHookCommand(stop, "dydo guard --stop"));
    }

    [Fact]
    public async Task Init_Codex_DoesNotDuplicateDydoHook()
    {
        await InitProjectAsync("codex");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        var content = ReadFile(".codex/hooks.json");
        Assert.Equal(1, content.Split("\"dydo guard\"").Length - 1);
        Assert.Equal(1, content.Split("dydo guard --stop").Length - 1);
    }

    [Fact]
    public async Task Init_Claude_HooksMatchEditWriteReadBash()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Edit|Write|Read|Bash", content);
    }

    [Fact]
    public async Task Init_Claude_MatcherIncludesPlanModeTools()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("EnterPlanMode", content);
        Assert.Contains("ExitPlanMode", content);
    }

    [Fact]
    public async Task Init_Claude_MatcherIncludesPowerShell()
    {
        // Bug B: PowerShell was missing from the matcher, so Claude Code did not pipe
        // PowerShell tool calls through `dydo guard` — total bypass of every guard layer.
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("PowerShell", content);
    }

    [Fact]
    public async Task Init_Claude_PreservesExistingHooks()
    {
        // Arrange: Create existing settings with a custom hook
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {
                "PreToolUse": [
                  {
                    "matcher": "CustomTool",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo custom"
                      }
                    ]
                  }
                ],
                "PostToolUse": [
                  {
                    "matcher": "AnyTool",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo post"
                      }
                    ]
                  }
                ]
              },
              "otherSetting": true
            }
            """);

        // Act
        var result = await InitProjectAsync("claude");

        // Assert
        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");

        // Should preserve existing PreToolUse entry
        Assert.Contains("CustomTool", content);
        Assert.Contains("echo custom", content);

        // Should add dydo guard entry
        Assert.Contains("dydo guard", content);
        Assert.Contains("Edit|Write|Read|Bash", content);

        // Should preserve PostToolUse
        Assert.Contains("PostToolUse", content);
        Assert.Contains("echo post", content);

        // Should preserve other settings
        Assert.Contains("otherSetting", content);
    }

    [Fact]
    public async Task Init_Claude_DoesNotDuplicateDydoHook()
    {
        // First init
        await InitProjectAsync("claude");

        // Manually re-run hook configuration (simulating re-init scenario)
        // Since init fails if already initialized, we test via join
        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");

        // The PreToolUse guard command is exactly `"dydo guard"` (trailing quote), distinct from the
        // Stop hook's `"dydo guard --stop"`. Counting the quoted form isolates the PreToolUse hook so a
        // re-run must leave exactly one of it — the Stop hook does not inflate the count.
        var guardCount = content.Split("\"dydo guard\"").Length - 1;
        Assert.Equal(1, guardCount);

        // The Stop hook is likewise de-duplicated across the re-run.
        var stopCount = content.Split("dydo guard --stop").Length - 1;
        Assert.Equal(1, stopCount);
    }

    [Fact]
    public async Task Init_Claude_MatcherIncludesAskUserQuestion()
    {
        // needs-human detection (Decision 030 §1): the guard must see the AskUserQuestion tool call.
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        Assert.Contains("AskUserQuestion", ReadFile(".claude/settings.local.json"));
    }

    [Fact]
    public async Task Init_Claude_InstallsStopHook_ForNeedsHumanTurnEnd()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("\"Stop\"", content);
        Assert.Contains("dydo guard --stop", content);
    }

    [Fact]
    public async Task Init_Claude_ExtendsHooksBlock_PreservingUnknownStopEntries()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {
                "Stop": [
                  { "hooks": [ { "type": "command", "command": "echo my-stop" } ] }
                ]
              }
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        // The project's own Stop hook survives, and the dydo one is added alongside it.
        Assert.Contains("echo my-stop", content);
        Assert.Contains("dydo guard --stop", content);
    }

    [Fact]
    public async Task Init_Claude_AddsDydoAllowEntry()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(dydo:*)", content);
    }

    [Fact]
    public async Task Init_Claude_AddsPowerShellDydoAllowEntry()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("PowerShell(dydo:*)", content);
    }

    [Fact]
    public async Task Init_Claude_AllowMergesWithExistingEntries()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "permissions": {
                "allow": [
                  "Bash(git:*)",
                  "Read(**)"
                ]
              }
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(git:*)", content);
        Assert.Contains("Read(**)", content);
        Assert.Contains("Bash(dydo:*)", content);
    }

    [Fact]
    public async Task Init_Claude_PowerShellAllowMergesWithExistingEntries()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "permissions": {
                "allow": [
                  "Bash(git:*)",
                  "Read(**)"
                ]
              }
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(git:*)", content);
        Assert.Contains("Read(**)", content);
        Assert.Contains("PowerShell(dydo:*)", content);
    }

    [Fact]
    public async Task Init_Claude_DoesNotDuplicateAllowEntry()
    {
        await InitProjectAsync("claude");

        // Re-run via join
        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        var count = content.Split("Bash(dydo:*)").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Init_Claude_DoesNotDuplicatePowerShellAllowEntry()
    {
        await InitProjectAsync("claude");

        // Re-run via join
        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        var count = content.Split("PowerShell(dydo:*)").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Init_Claude_CreatesAllowArrayWhenMissing()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {}
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(dydo:*)", content);
        Assert.Contains("permissions", content);
    }

    [Fact]
    public async Task Init_Claude_BothShellEntriesWhenAllowArrayMissing()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {}
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(dydo:*)", content);
        Assert.Contains("PowerShell(dydo:*)", content);
        Assert.Contains("permissions", content);
    }

    [Fact]
    public async Task Init_Claude_GeneratesRoleFiles()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        AssertDirectoryExists("dydo/_system/roles");

        var rolesDir = Path.Combine(TestDir, "dydo/_system/roles");
        var files = Directory.GetFiles(rolesDir, "*.role.json");
        Assert.Equal(7, files.Length);
        Assert.Contains(files, f => Path.GetFileName(f) == "code-writer.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "orchestrator.role.json");
        Assert.Contains(files, f => Path.GetFileName(f) == "chief-of-staff.role.json");
        Assert.DoesNotContain(files, f => Path.GetFileName(f) == "planner.role.json");
        Assert.DoesNotContain(files, f => Path.GetFileName(f) == "judge.role.json");
    }

    [Fact]
    public async Task Init_None_GeneratesRoleFiles()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();

        var rolesDir = Path.Combine(TestDir, "dydo/_system/roles");
        var files = Directory.GetFiles(rolesDir, "*.role.json");
        Assert.Equal(7, files.Length);
    }

    [Fact]
    public async Task Init_None_DoesNotCreateAllowEntry()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileNotExists(".claude/settings.local.json");
        AssertFileNotExists(".codex/hooks.json");
        AssertFileNotExists("AGENTS.md");
    }

    #endregion

    #region Init Join

    [Fact]
    public async Task Init_Join_WithoutExistingProject_Fails()
    {
        var command = InitCommand.Create();
        var result = await RunAsync(command, "none", "--join");

        result.AssertExitCode(2);
        result.AssertStderrContains("No DynaDocs project found");
    }

    [Fact]
    public async Task Init_Join_ExistingProject_Succeeds()
    {
        await InitProjectAsync("none");

        // With the roster gone (DR-041), join no longer assigns agents — it just re-wires the
        // local integration for an already-initialized project, and succeeds.
        var result = await JoinProjectAsync("none");

        result.AssertSuccess();
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Init_InvalidIntegration_Fails()
    {
        var command = InitCommand.Create();
        var result = await RunAsync(command, "invalid");

        result.AssertExitCode(2);
        result.AssertStderrContains("Unknown integration");
        result.AssertStderrContains("Valid options: claude, codex, none");
    }

    [Fact]
    public async Task Init_AlreadyInitialized_Fails()
    {
        await InitProjectAsync("none");

        var command = InitCommand.Create();
        var result = await RunAsync(command, "none");

        result.AssertExitCode(2);
        result.AssertStderrContains("already initialized");
    }

    #endregion

    #region Idempotency

    [Fact]
    public async Task Init_DoesNotOverwriteExistingDocs()
    {
        await InitProjectAsync("none");

        // Modify a file
        WriteFile("dydo/understand/architecture.md", "# Custom Architecture\n\nMy custom content");

        // Try to init again (should fail because already initialized)
        var command = InitCommand.Create();
        var result = await RunAsync(command, "none");

        // Init should fail, but more importantly, the file should not be overwritten
        var content = ReadFile("dydo/understand/architecture.md");
        Assert.Contains("Custom Architecture", content);
    }

    #endregion

    private static List<string> HookCommands(JsonNode? entry)
    {
        var entryObject = Assert.IsType<JsonObject>(entry);
        var hooks = Assert.IsType<JsonArray>(entryObject["hooks"]);
        return hooks
            .OfType<JsonObject>()
            .Select(hook => hook["command"]?.GetValue<string>())
            .Where(command => command != null)
            .Select(command => command!)
            .ToList();
    }

    private static int CountExactHookCommand(JsonArray entries, string command) =>
        entries.Sum(entry => HookCommands(entry).Count(existing => existing == command));
}
