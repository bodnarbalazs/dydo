namespace DynaDocs.Tests.Commands;

using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using Xunit;

public class SyncCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly RoleDefinition _reviewer;

    public SyncCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-sync-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _reviewer = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == "reviewer");
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void SyncRole_WritesAgentAndSkillFiles()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "reviewer.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md")));
    }

    [Fact]
    public void SyncCodexRole_WritesAgentAndRepoSkillFiles()
    {
        SyncCommand.SyncCodexRole(_reviewer, _testDir, ConfigFactory.CreateDefaultModels());

        Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", "reviewer", "SKILL.md")));
    }

    // DR-039 review-target subskills / DR-042: <role>-resource-<name>.template.md files
    // compile into the skill's resources/ folder, on both the Claude and Codex emit paths.
    [Fact]
    public void SyncRole_EmitsSkillReferences_PlanRubric()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);

        var plan = Path.Combine(_testDir, ".claude", "skills", "reviewer", "resources", "plan.md");
        Assert.True(File.Exists(plan), "reviewer skill must ship resources/plan.md");
        Assert.Contains("Reviewing a Plan", File.ReadAllText(plan));
    }

    [Fact]
    public void SyncCodexSkill_EmitsSkillReferences_PlanRubric()
    {
        SyncCommand.SyncCodexSkill(_reviewer, _testDir);

        Assert.True(File.Exists(
            Path.Combine(_testDir, ".agents", "skills", "reviewer", "resources", "plan.md")));
    }

    [Fact]
    public void GetSkillResources_RoleWithoutReferences_IsEmpty()
    {
        Assert.Empty(TemplateGenerator.GetSkillResources("code-writer"));
    }

    // Workflow harnesses are dydo-authored (Templates/workflow-<name>.js) and compiled to
    // .claude/workflows — hand-editing the emitted scripts is the drift the compiler ends.
    [Fact]
    public void SyncWorkflows_EmitsRunSprintAndInquisition()
    {
        var count = SyncCommand.SyncWorkflows(_testDir);

        Assert.Equal(2, count);
        var runSprint = Path.Combine(_testDir, ".claude", "workflows", "run-sprint.js");
        Assert.True(File.Exists(runSprint));
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "workflows", "inquisition.js")));
        var content = File.ReadAllText(runSprint);
        Assert.Contains("export const meta", content);
        Assert.DoesNotContain("\r", content); // LF-normalized for Claude Code
    }

    [Fact]
    public void SyncCodexRole_EmitsStrongOpenAiModelBinding()
    {
        SyncCommand.SyncCodexRole(_reviewer, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml"));
        Assert.Contains("model = \"gpt-5.6-sol\"", agent);
    }

    [Fact]
    public void SyncCodexRole_WithoutModelBinding_UsesStandardOpenAiFallback()
    {
        SyncCommand.SyncCodexRole(_reviewer, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml"));
        Assert.Contains("model = \"gpt-5.6-terra\"", agent);
    }

    [Theory]
    [InlineData("code-writer", "gpt-5.6-terra")]
    [InlineData("docs-writer", "gpt-5.6-terra")]
    public void SyncCodexRole_DefaultModels_EmitsTierCorrectModel(string roleName, string expectedModel)
    {
        var role = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == roleName);
        SyncCommand.SyncCodexRole(role, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", $"{roleName}.toml"));
        Assert.Contains($"model = \"{expectedModel}\"", agent);
    }

    [Fact]
    public void SyncCodexRole_EmitsDeveloperInstructions()
    {
        SyncCommand.SyncCodexRole(_reviewer, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml"));
        Assert.Contains("developer_instructions = \"\"\"", agent);
        Assert.Contains("Read these for project context before working:", agent);
        Assert.Contains("- dydo/guides/coding-standards.md", agent);
        Assert.DoesNotContain("must_reads", agent);
        Assert.DoesNotContain(agent.Split('\n'), line => line.StartsWith("instructions = \"\"\""));
    }

    // Issue 0271 (wire-shape guard, same class as 0261): codex's agent `tools` field is a
    // ToolsToml struct of codex toggles (view_image, web_search), NOT file/shell tool names.
    // The old emitter wrote `tools = "read, grep, glob, bash, ..."` — a bare string codex
    // rejects with 'invalid type: string ... expected struct ToolsToml', silently ignoring
    // every worker role. The fix drops the field; these pin that no worker role emits it,
    // for either the read-only or the read-write branch.
    [Theory]
    [InlineData("reviewer")]
    [InlineData("code-writer")]
    public void SyncCodexRole_OmitsToolsField(string roleName)
    {
        var role = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == roleName);

        SyncCommand.SyncCodexRole(role, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", $"{roleName}.toml"));
        Assert.DoesNotContain(agent.Split('\n'), line => line.TrimStart().StartsWith("tools"));
        // Fields codex does accept remain intact — the drop is surgical, not structural.
        Assert.Contains($"name = \"{roleName}\"", agent);
        Assert.Contains("description = \"", agent);
        Assert.Contains("model = \"", agent);
        Assert.Contains("developer_instructions = \"\"\"", agent);
    }

    [Fact]
    public void SyncRole_Agent_HasReadOnlyToolProfileAndFrontmatter()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);
        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));

        Assert.Contains("name: reviewer", agent);
        // Read-only role → no Edit/Write tools (that's how "reviewers don't write code" is native-enforced)
        Assert.Contains("tools: Read, Grep, Glob, Bash", agent);
        Assert.DoesNotContain("Edit", agent);
        Assert.DoesNotContain("Write", agent);
        // Carries project-context must-reads
        Assert.Contains("coding-standards.md", agent);
    }

    [Fact]
    public void SyncRole_Skill_KeepsMethodology_DropsOrchestration()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);
        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md"));

        // Timeless methodology survives
        Assert.Contains("Mindset", skill);
        Assert.Contains("YOU SHALL NOT PASS", skill);
        // Target rubrics ride resources/ (DR-039 subskills); the SKILL keeps the pointer map
        Assert.Contains("Review Targets", skill);
        Assert.Contains("resources/plan.md", skill);

        // Old-runtime orchestration is gone
        Assert.DoesNotContain("## Set Role", skill);
        Assert.DoesNotContain("## Register General Wait", skill);
        Assert.DoesNotContain("dydo wait", skill);
        Assert.DoesNotContain("dydo agent role", skill);
        // The {{AGENT_NAME}} placeholder is de-personalized
        Assert.DoesNotContain("{{AGENT_NAME}}", skill);
    }

    [Fact]
    public void ExtractMethodology_StripsFrontmatter()
    {
        var methodology = SyncCommand.ExtractMethodology(_reviewer, _testDir);
        // The mode-file frontmatter (agent:/mode:) must not leak into the skill body
        Assert.DoesNotContain("mode: reviewer", methodology);
        Assert.StartsWith("#", methodology.TrimStart());
        // No dangling horizontal rule at the end after dropping the trailing section
        Assert.False(methodology.TrimEnd().EndsWith("---"));
    }

    [Fact]
    public void SyncRole_WriterRole_GetsWriterToolsAndStance()
    {
        var codeWriter = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == "code-writer");
        SyncCommand.SyncRole(codeWriter, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "code-writer.md"));
        // A writer role gets Edit/Write AND writer-stance prose — not the read-only contradiction
        Assert.Contains("Edit, Write", agent);
        Assert.Contains("produce and modify the project's files", agent);
        Assert.DoesNotContain("read-only", agent);

        // The skill description must be role-correct, not reviewer-hardcoded
        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "code-writer", "SKILL.md"));
        Assert.DoesNotContain("reviewing a code change", skill);
        Assert.Contains("working as a code-writer", skill);
    }

    [Fact]
    public void ExtractMustReads_IsRoleSpecific()
    {
        // docs-writer's must-reads come from ITS template (writing-docs, not coding-standards)
        var docsWriter = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == "docs-writer");
        var mustReads = SyncCommand.ExtractMustReads(docsWriter, _testDir);

        Assert.Contains("dydo/reference/writing-docs.md", mustReads);
        Assert.DoesNotContain("dydo/guides/coding-standards.md", mustReads);
        // Reviewer's are different — code standards, not writing-docs
        var reviewerMustReads = SyncCommand.ExtractMustReads(_reviewer, _testDir);
        Assert.Contains("dydo/guides/coding-standards.md", reviewerMustReads);
    }

    [Fact]
    public void RenumberOrderedLists_ContinuesAcrossProse_ResetsOnHeading()
    {
        // A list interrupted by prose/code keeps numbering; a new heading restarts it.
        var input = "## A\n1. one\n2. two\n\nsome prose\n3. three\n\n## B\n1. fresh";
        var result = SyncCommand.RenumberOrderedLists(input);
        Assert.Equal("## A\n1. one\n2. two\n\nsome prose\n3. three\n\n## B\n1. fresh", result);
    }

    [Fact]
    public void RenumberOrderedLists_IgnoresContentInsideCodeFences()
    {
        // Inquisition finding: a literal "1." or "# comment" inside a ```fence``` must not
        // be renumbered or reset the running count.
        var input = "1. first\n```bash\n# a shell comment\n1. not a list item\n```\n2. second";
        var result = SyncCommand.RenumberOrderedLists(input);
        Assert.Equal("1. first\n```bash\n# a shell comment\n1. not a list item\n```\n2. second", result);
    }

    [Fact]
    public void SyncCommand_Run_GeneratesAllWorkerRoles()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            foreach (var role in new[] { "code-writer", "reviewer", "test-writer", "docs-writer" })
            {
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{role}.md")), $"missing agent: {role}");
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", role, "SKILL.md")), $"missing skill: {role}");
                Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", $"{role}.toml")), $"missing codex agent: {role}");
                Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", role, "SKILL.md")), $"missing repo skill: {role}");
            }
            Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "hooks.json")), "missing codex hooks");
            AssertCodexHooksShape();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void WriteCodexHooks_PreservesCustomEntries()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, ".codex"));
        File.WriteAllText(Path.Combine(_testDir, ".codex", "hooks.json"), """
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
                  },
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
                        "command": "echo mixed custom"
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
                        "command": "echo stop"
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

        SyncCommand.WriteCodexHooks(_testDir);

        var settings = ReadCodexHooks();
        var hooks = Assert.IsType<JsonObject>(settings["hooks"]);
        Assert.Null(settings["PreToolUse"]);
        Assert.Null(settings["Stop"]);

        var preToolUse = Assert.IsType<JsonArray>(hooks["PreToolUse"]);
        Assert.Contains(preToolUse, entry => entry?["matcher"]?.GetValue<string>() == "CustomTool");
        Assert.Contains(preToolUse, entry =>
            entry?["matcher"]?.GetValue<string>() == "CustomSubstring" &&
            HookCommands(entry).Contains("echo before dydo guard after"));
        Assert.Contains(preToolUse, entry =>
            entry?["matcher"]?.GetValue<string>() == "Mixed" &&
            HookCommands(entry).SequenceEqual(["echo mixed custom"]));
        Assert.Equal(1, CountExactHookCommand(preToolUse, "dydo guard"));

        var stop = Assert.IsType<JsonArray>(hooks["Stop"]);
        Assert.Contains(stop, entry => HookCommands(entry).SequenceEqual(["echo stop"]));
        Assert.Contains(stop, entry => HookCommands(entry).Contains("echo before dydo guard --stop after"));
        Assert.Equal(1, CountExactHookCommand(stop, "dydo guard --stop"));
    }

    private JsonObject ReadCodexHooks() =>
        Assert.IsType<JsonObject>(JsonNode.Parse(
            File.ReadAllText(Path.Combine(_testDir, ".codex", "hooks.json"))));

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

    private void AssertCodexHooksShape()
    {
        var settings = ReadCodexHooks();
        Assert.Null(settings["PreToolUse"]);
        Assert.Null(settings["Stop"]);

        var hooks = Assert.IsType<JsonObject>(settings["hooks"]);
        var preToolUse = Assert.IsType<JsonArray>(hooks["PreToolUse"]);
        var guardEntry = Assert.Single(preToolUse, entry =>
            entry?.ToJsonString().Contains("dydo guard") == true);
        Assert.NotNull(guardEntry);
        var matcher = guardEntry["matcher"]?.GetValue<string>();
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
        // Codex shell tool names (issue 0295) — the guard must see codex shell execution, not
        // only apply_patch file edits.
        Assert.Contains("shell_command", matcher);
        Assert.Contains("exec", matcher);
        Assert.Contains("local_shell", matcher);
        Assert.Contains("unified_exec", matcher);

        var stop = Assert.IsType<JsonArray>(hooks["Stop"]);
        Assert.Contains(stop, entry => entry?.ToJsonString().Contains("dydo guard --stop") == true);
    }

    [Fact]
    public void SyncCommand_Run_GeneratesPlannerSkill_ButNoPlannerAgent()
    {
        // Decision 024: planner is skill-only. `dydo sync` emits its SKILL.md so the
        // orchestrator can apply the planning methodology, but never an agent definition
        // (there is no planner sub-agent to spawn and no claimable planner identity).
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "planner", "SKILL.md")),
                "planner skill must be generated");
            Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", "planner.md")),
                "planner must NOT get a native agent definition");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void SyncSkillOnlyRole_WritesSkillButNoAgent()
    {
        var planner = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == "planner");
        SyncCommand.SyncSkillOnlyRole(planner, _testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "planner", "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", "planner.md")));
    }

    [Fact]
    public void RenumberOrderedLists_FixesDuplicateNumbering()
    {
        // A list whose numbering was broken by an included continuation (…4. then 4./5.)
        // is renumbered as a single 1..N run; blank lines don't break the run.
        var input = "1. first\n2. second\n\n2. dup\n3. next";
        var result = SyncCommand.RenumberOrderedLists(input);
        Assert.Equal("1. first\n2. second\n\n3. dup\n4. next", result);
    }

    [Fact]
    public void SyncRole_Reviewer_MergeSprintReference_CarriesInquisitorJudgeCharacter()
    {
        // DR-039: the sprint-auditor folded into the reviewer — its inquisitor/judge character
        // now rides the merge-sprint review-target reference the reviewer skill ships.
        SyncCommand.SyncRole(_reviewer, _testDir);

        var reference = File.ReadAllText(
            Path.Combine(_testDir, ".claude", "skills", "reviewer", "resources", "merge-sprint.md"));
        // Inquisitor lens: hunts real cross-slice issues (seams are the signature concern)
        Assert.Contains("Inquisitor", reference);
        Assert.Contains("Seams", reference);
        // Judge strictness: verdict with findings, no "pass with notes"
        Assert.Contains("Judge", reference);
        Assert.Contains("pass with notes", reference);
    }

    [Fact]
    public void SyncRole_EmitsLfLineEndings()
    {
        // CRLF in .claude/ artifacts makes Claude Code's permission handler reject them
        // ("control characters that would be hidden in the approval dialog"), so sync
        // must emit LF regardless of platform/template line endings.
        SyncCommand.SyncRole(_reviewer, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md"));
        Assert.DoesNotContain('\r', agent);
        Assert.DoesNotContain('\r', skill);
    }

    [Fact]
    public void SyncCommand_Run_WritesReviewerArtifacts()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            var result = SyncCommand.Create().Parse([]).Invoke();

            Assert.Equal(0, result);
            Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "reviewer.md")));
            Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    // --- Model-tier resolution (Decision 028) ---

    private static ModelsConfig TestModels() => new()
    {
        Tiers = new Dictionary<string, Dictionary<string, string>>
        {
            ["anthropic"] = new() { ["strong"] = "model-strong", ["standard"] = "model-standard" }
        },
        Roles = new Dictionary<string, string>
        {
            ["reviewer"] = "strong",
            ["code-writer"] = "standard",
            ["docs-writer"] = "light" // tier NOT bound in the vendor map
        },
        Efforts = new Dictionary<string, string> { ["code-writer"] = "low" }
    };

    [Fact]
    public void ResolveModel_MappedRole_ReturnsConcreteModel()
    {
        var (model, effort) = SyncCommand.ResolveModel(TestModels(), "reviewer");
        Assert.Equal("model-strong", model);
        Assert.Null(effort);
    }

    [Fact]
    public void ResolveModel_OpenAiDefault_ReturnsStrongTierModel()
    {
        var (model, effort) = SyncCommand.ResolveModel(ConfigFactory.CreateDefaultModels(), "reviewer", "openai");

        Assert.Equal("gpt-5.6-sol", model);
        Assert.Null(effort);
    }

    [Theory]
    [InlineData("reviewer", "gpt-5.6-sol")]
    [InlineData("planner", "gpt-5.6-sol")]
    [InlineData("code-writer", "gpt-5.6-terra")]
    [InlineData("docs-writer", "gpt-5.6-terra")]
    public void ResolveModel_OpenAiDefault_UsesRoleTier(string roleName, string expectedModel)
    {
        var (model, _) = SyncCommand.ResolveModel(ConfigFactory.CreateDefaultModels(), roleName, "openai");

        Assert.Equal(expectedModel, model);
    }

    [Fact]
    public void ResolveModel_RoleWithEffort_ReturnsBoth()
    {
        var (model, effort) = SyncCommand.ResolveModel(TestModels(), "code-writer");
        Assert.Equal("model-standard", model);
        Assert.Equal("low", effort);
    }

    [Fact]
    public void ResolveModel_UnmappedRole_ReturnsNull()
    {
        // No role → tier entry: inherit the session model (Decision 028 — no silent downgrade).
        var (model, effort) = SyncCommand.ResolveModel(TestModels(), "test-writer");
        Assert.Null(model);
        Assert.Null(effort);
    }

    [Fact]
    public void ResolveModel_TierMissingFromVendorMap_ReturnsNull()
    {
        // docs-writer maps to "light", which the vendor map does not bind → inherit.
        var (model, _) = SyncCommand.ResolveModel(TestModels(), "docs-writer");
        Assert.Null(model);
    }

    [Fact]
    public void ResolveModel_AbsentModelsSection_ReturnsNull()
    {
        var (model, effort) = SyncCommand.ResolveModel(null, "reviewer");
        Assert.Null(model);
        Assert.Null(effort);
    }

    [Fact]
    public void SyncRole_WithModels_EmitsResolvedModelFrontmatter()
    {
        SyncCommand.SyncRole(_reviewer, _testDir, TestModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
        Assert.Contains("\nmodel: model-strong\n", agent);
        Assert.DoesNotContain("model: inherit", agent);
        Assert.DoesNotContain("effort:", agent); // no effort configured for reviewer
    }

    [Fact]
    public void SyncRole_WithEffort_EmitsEffortLine()
    {
        var codeWriter = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == "code-writer");
        SyncCommand.SyncRole(codeWriter, _testDir, TestModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "code-writer.md"));
        Assert.Contains("\nmodel: model-standard\neffort: low\n", agent);
    }

    [Fact]
    public void SyncRole_UnmappedRole_FallsBackToInherit()
    {
        var testWriter = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == "test-writer");
        SyncCommand.SyncRole(testWriter, _testDir, TestModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "test-writer.md"));
        Assert.Contains("model: inherit", agent);
    }

    [Fact]
    public void SyncRole_NoModelsSection_FallsBackToInherit()
    {
        SyncCommand.SyncRole(_reviewer, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
        Assert.Contains("model: inherit", agent);
    }

    [Fact]
    public void SyncCommand_Run_ResolvesModelsFromDydoJson()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
                {
                  "version": 1,
                  "models": {
                    "tiers": { "anthropic": { "strong": "vendor-strong-model" } },
                    "roles": { "reviewer": "strong" }
                  }
                }
                """);
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            var reviewer = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
            Assert.Contains("model: vendor-strong-model", reviewer);
            // Unmapped worker roles inherit the session model
            var codeWriter = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "code-writer.md"));
            Assert.Contains("model: inherit", codeWriter);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void DefaultModels_ResolveForAllTieredWorkerRoles()
    {
        // The shipped defaults (Decision 028) must actually bind: every role in the
        // default role → tier map resolves to a concrete model.
        var models = ConfigFactory.CreateDefaultModels();
        foreach (var role in models.Roles.Keys)
        {
            var (model, _) = SyncCommand.ResolveModel(models, role);
            Assert.False(string.IsNullOrEmpty(model), $"default tier for '{role}' did not resolve");
        }
    }

    // --- Tier-1 manager modes (Decision 026) ---

    [Fact]
    public void SyncCommand_Run_GeneratesTier1ManagerSkills_ButNoAgents()
    {
        // Decision 026: orchestrator / co-thinker / chief-of-staff mode skills join the
        // sync output, but Tier-1 identities are terminal sessions, never sub-agents.
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            foreach (var role in new[] { "orchestrator", "co-thinker", "chief-of-staff" })
            {
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", role, "SKILL.md")),
                    $"missing Tier-1 skill: {role}");
                Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{role}.md")),
                    $"Tier-1 mode '{role}' must NOT get a native agent definition");
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Theory]
    [InlineData("orchestrator")]
    [InlineData("co-thinker")]
    [InlineData("chief-of-staff")]
    public void Tier1ManagerSkills_StateTheManagersDoctrine(string roleName)
    {
        // Decision 026 §1–2: every Tier-1 mode skill states the doctrine — managers
        // write no code, implementation goes through workflows, trivial-edit exception.
        var role = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == roleName);
        SyncCommand.SyncSkillOnlyRole(role, _testDir);

        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", roleName, "SKILL.md"));
        Assert.Contains("Managers Doctrine", skill);
        Assert.Contains("reviewed workflow", skill);
        Assert.Contains("if it needs a reviewer, it needs a plan and a workflow", skill);
    }

    [Fact]
    public void ChiefOfStaff_Skill_CarriesCharacterAndInvariants()
    {
        var chief = RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == "chief-of-staff");
        SyncCommand.SyncSkillOnlyRole(chief, _testDir);

        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "chief-of-staff", "SKILL.md"));
        // The human's right hand: triage + routing, status reports, mediation
        Assert.Contains("right hand", skill);
        Assert.Contains("Triage the funnel", skill);
        Assert.Contains("Status reports", skill);
        Assert.Contains("Escalations awaiting decisions", skill);
        Assert.Contains("Gates awaiting the human", skill);
        Assert.Contains("Mediate", skill);
        // Invariants: never in an approval path; PM objects/docs, never code
        Assert.Contains("never in an approval path", skill);
        Assert.Contains("never code", skill);
    }

    [Fact]
    public void OrchestratorTemplate_HasNoDead10Machinery()
    {
        // Decision 026 sweep: worker-tier dispatch, .needs-merge markers, and
        // `dydo worktree merge` flows are 1.0 machinery that no longer exists.
        var methodology = SyncCommand.ExtractMethodology(
            RoleDefinitionService.DiscoverRoles(_testDir).First(r => r.Name == "orchestrator"), _testDir);

        Assert.DoesNotContain(".needs-merge", methodology);
        Assert.DoesNotContain("dydo worktree merge", methodology);
        Assert.DoesNotContain("--role inquisitor", methodology);
        Assert.DoesNotContain("--role code-writer", methodology);
        // The 2.0 reality is stated instead
        Assert.Contains("run-sprint", methodology);
    }

    [Fact]
    public void CoThinkerTemplate_DoesNotSwitchIntoCodeWriting()
    {
        var raw = TemplateGenerator.ReadBuiltInTemplate("mode-co-thinker.template.md");

        Assert.DoesNotContain("dydo agent role code-writer", raw);
        Assert.DoesNotContain("--role planner", raw);
        Assert.Contains("reviewed workflow", raw);
    }
}
