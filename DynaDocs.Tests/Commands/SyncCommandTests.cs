namespace DynaDocs.Tests.Commands;

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Tests;
using DynaDocs.Utils;
using Xunit;

public class SyncCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly SkillTemplate _reviewer;

    public SyncCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-sync-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _reviewer = SkillTemplateService.DiscoverSkills().First(r => r.Name == "reviewer");
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void SyncAgent_WritesAgentAndSkillFiles()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "reviewer.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md")));
    }

    // Issue 0300: sync gates emission on the integrations recorded in dydo.json. A config
    // recording neither hook-wired integration (legacy, or "none") emits everything.
    [Fact]
    public void Execute_ClaudeOnlyIntegration_SkipsCodexArtifacts()
    {
        SaveConfigWithIntegrations(claude: true, codex: false);

        SyncCommand.Execute(_testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "reviewer.md")));
        Assert.False(Directory.Exists(Path.Combine(_testDir, ".codex")));
        Assert.False(Directory.Exists(Path.Combine(_testDir, ".agents")));
    }

    [Fact]
    public void Execute_CodexOnlyIntegration_SkipsClaudeArtifacts()
    {
        SaveConfigWithIntegrations(claude: false, codex: true);

        SyncCommand.Execute(_testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", "reviewer", "SKILL.md")));
        Assert.False(Directory.Exists(Path.Combine(_testDir, ".claude")));
    }

    [Fact]
    public void Execute_NoRecordedIntegrations_EmitsEverything()
    {
        SaveConfigWithIntegrations(claude: false, codex: false);

        SyncCommand.Execute(_testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "reviewer.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml")));
    }

    [Fact]
    public void Execute_FreshProject_EmitsInquisitorForBothRuntimes()
    {
        SyncCommand.Execute(_testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "inquisitor.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "inquisitor", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", "inquisitor.toml")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", "inquisitor", "SKILL.md")));
        Assert.Contains(
            "You are an **inquisitor**.",
            File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "inquisitor.md")));
        Assert.Contains(
            "You are an **inquisitor**.",
            File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "inquisitor.toml")));
    }

    [Fact]
    public void Execute_RetiredSprintAuditor_RemovesGeneratedFilesButPreservesSiblings()
    {
        var generatedFiles = new[]
        {
            Path.Combine(_testDir, ".claude", "agents", "sprint-auditor.md"),
            Path.Combine(_testDir, ".claude", "skills", "sprint-auditor", "SKILL.md"),
            Path.Combine(_testDir, ".codex", "agents", "sprint-auditor.toml"),
            Path.Combine(_testDir, ".agents", "skills", "sprint-auditor", "SKILL.md"),
        };
        foreach (var file in generatedFiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "legacy generated content");
        }

        var claudeSibling = Path.Combine(
            _testDir, ".claude", "skills", "sprint-auditor", "project-notes.md");
        var codexSibling = Path.Combine(
            _testDir, ".agents", "skills", "sprint-auditor", "project-notes.md");
        File.WriteAllText(claudeSibling, "project owned");
        File.WriteAllText(codexSibling, "project owned");

        SyncCommand.Execute(_testDir);

        Assert.All(generatedFiles, file => Assert.False(File.Exists(file), file));
        Assert.True(File.Exists(claudeSibling));
        Assert.True(File.Exists(codexSibling));
    }

    // DR 045: the orchestrator retires into the admiral hat. Its template file still ships
    // through the transition, so a retired name must leave the shipped template set — otherwise
    // the skill stays "active", the sweep below is suppressed by dydo's own source, and the stale
    // skill folder outlives the skill on both hosts.
    [Fact]
    public void Execute_RetiredOrchestrator_RemovesStaleSkillFoldersOnBothHosts()
    {
        var stale = new[]
        {
            Path.Combine(_testDir, ".claude", "skills", "orchestrator", "SKILL.md"),
            Path.Combine(_testDir, ".agents", "skills", "orchestrator", "SKILL.md"),
        };
        foreach (var file in stale)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "stale generated content");
        }

        SyncCommand.Execute(_testDir);

        Assert.All(stale, file => Assert.False(File.Exists(file), file));
        Assert.False(Directory.Exists(Path.Combine(_testDir, ".claude", "skills", "orchestrator")));
        Assert.False(Directory.Exists(Path.Combine(_testDir, ".agents", "skills", "orchestrator")));
        Assert.DoesNotContain(SkillTemplateService.DiscoverSkills(), s => s.Name == "orchestrator");
    }

    [Fact]
    public void Execute_ImplementerToIssueCaptainMigration_SweepsLegacyArtifactsAndEmitsReplacement()
    {
        Assert.Contains("implementer", SyncCommand.RetiredSkills);

        var issueCaptain = Assert.Single(
            SkillTemplateService.DiscoverSkills(), skill => skill.Name == "issue-captain");
        Assert.True(issueCaptain.EmitAgent);
        Assert.True(issueCaptain.Delegates);

        var legacyArtifacts = new[]
        {
            Path.Combine(_testDir, ".claude", "agents", "implementer.md"),
            Path.Combine(_testDir, ".claude", "skills", "implementer", "SKILL.md"),
            Path.Combine(_testDir, ".codex", "agents", "implementer.toml"),
            Path.Combine(_testDir, ".agents", "skills", "implementer", "SKILL.md"),
        };
        foreach (var artifact in legacyArtifacts)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
            File.WriteAllText(artifact, "legacy generated content");
        }

        SyncCommand.Execute(_testDir);

        Assert.All(legacyArtifacts, artifact => Assert.False(File.Exists(artifact), artifact));
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", "issue-captain.md")));
        Assert.True(File.Exists(Path.Combine(
            _testDir, ".claude", "skills", "issue-captain", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", "issue-captain.toml")));
        Assert.True(File.Exists(Path.Combine(
            _testDir, ".agents", "skills", "issue-captain", "SKILL.md")));
    }

    [Fact]
    public void Execute_ManagerToAdmiralMigration_SweepsLegacyArtifactsAndEmitsReplacement()
    {
        Assert.Contains("manager", SyncCommand.RetiredSkills);

        var admiral = Assert.Single(
            SkillTemplateService.DiscoverSkills(), skill => skill.Name == "admiral");
        Assert.False(admiral.EmitAgent);
        Assert.True(admiral.ExplicitInvocation);

        var legacyArtifacts = new[]
        {
            Path.Combine(_testDir, ".claude", "agents", "manager.md"),
            Path.Combine(_testDir, ".claude", "skills", "manager", "SKILL.md"),
            Path.Combine(_testDir, ".codex", "agents", "manager.toml"),
            Path.Combine(_testDir, ".agents", "skills", "manager", "SKILL.md"),
        };
        foreach (var artifact in legacyArtifacts)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
            File.WriteAllText(artifact, "legacy generated content");
        }

        SyncCommand.Execute(_testDir);

        Assert.All(legacyArtifacts, artifact => Assert.False(File.Exists(artifact), artifact));
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", "admiral", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", "admiral", "SKILL.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", "admiral.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".codex", "agents", "admiral.toml")));
    }

    [Fact]
    public void Execute_RetiredWorkflow_RemovesTheStaleRunSprintScript()
    {
        var stale = Path.Combine(_testDir, ".claude", "workflows", "run-sprint.js");
        Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
        File.WriteAllText(stale, "export const meta = {};");

        SyncCommand.Execute(_testDir);

        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "workflows", "inquisition.js")));
    }

    // Renamed and split rubrics are not overwritten by their replacements, so sync has to delete
    // every retired compiled file on both hosts without sweeping project-owned siblings.
    [Fact]
    public void CleanRetiredArtifacts_RemovesRenamedReviewerRubricsOnBothHosts()
    {
        var stale = new[] { ".claude", ".agents" }
            .SelectMany(host => new[] { "merge-sprint.md", "plan.md" }
                .Select(name => Path.Combine(
                    _testDir, host, "skills", "reviewer", "resources", name)))
            .ToList();
        var sibling = Path.Combine(
            _testDir, ".claude", "skills", "reviewer", "resources", "project-notes.md");
        foreach (var file in stale)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "stale rubric");
        }
        File.WriteAllText(sibling, "project owned");

        var removed = SyncCommand.CleanRetiredArtifacts(_testDir);

        Assert.All(stale, file => Assert.False(File.Exists(file), file));
        Assert.True(File.Exists(sibling), "a project-owned sibling must survive the sweep");
        Assert.Equal(4, removed);
    }

    private void SaveConfigWithIntegrations(bool claude, bool codex)
    {
        var config = ConfigFactory.CreateDefault();
        if (claude) config.Integrations["claude"] = true;
        if (codex) config.Integrations["codex"] = true;
        new ConfigService().SaveConfig(config, Path.Combine(_testDir, "dydo.json"));
    }

    [Fact]
    public void SyncCodexAgent_WritesAgentAndRepoSkillFiles()
    {
        SyncCommand.SyncCodexAgent(_reviewer, _testDir, ConfigFactory.CreateDefaultModels());

        Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", "reviewer", "SKILL.md")));
    }

    // DR-039 review-target subskills / DR-042: <skill>-resource-<name>.template.md files
    // compile into the skill's resources/ folder, on both the Claude and Codex emit paths.
    [Fact]
    public void SyncAgent_EmitsSkillReferences_BothPlanRubrics()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir);

        foreach (var name in new[] { "project-plan.md", "issue-plan.md" })
        {
            var plan = Path.Combine(_testDir, ".claude", "skills", "reviewer", "resources", name);
            Assert.True(File.Exists(plan), $"reviewer skill must ship resources/{name}");
            Assert.NotEmpty(File.ReadAllText(plan));
        }
    }

    [Fact]
    public void SyncCodexSkill_EmitsSkillReferences_BothPlanRubrics()
    {
        SyncCommand.SyncCodexSkill(_reviewer, _testDir);

        foreach (var name in new[] { "project-plan.md", "issue-plan.md" })
        {
            Assert.True(File.Exists(
                Path.Combine(_testDir, ".agents", "skills", "reviewer", "resources", name)));
        }
    }

    [Fact]
    public void SyncSkill_ExplicitInvocation_EmitsClaudePolicyOnly()
    {
        var template = ExplicitSkill();

        SyncCommand.SyncSkill(template, _testDir);

        var skill = File.ReadAllText(
            Path.Combine(_testDir, ".claude", "skills", template.Name, "SKILL.md"));
        Assert.Contains("\ndisable-model-invocation: true\n", skill);
        Assert.Equal(1, skill.Split('\n').Count(line => line == $"description: {template.Description}"));
    }

    [Fact]
    public void SyncSkill_AutomaticInvocation_OmitsClaudePolicy()
    {
        var template = AutomaticSkill();

        SyncCommand.SyncSkill(template, _testDir);

        var skill = File.ReadAllText(
            Path.Combine(_testDir, ".claude", "skills", template.Name, "SKILL.md"));
        Assert.DoesNotContain("disable-model-invocation", skill);
        Assert.Contains($"description: {template.Description}\n", skill);
    }

    [Fact]
    public void SyncCodexSkill_ExplicitInvocation_EmitsOpenAiPolicy()
    {
        var template = ExplicitSkill();

        SyncCommand.SyncCodexSkill(template, _testDir);

        var skill = File.ReadAllText(
            Path.Combine(_testDir, ".agents", "skills", template.Name, "SKILL.md"));
        var policy = File.ReadAllText(
            Path.Combine(_testDir, ".agents", "skills", template.Name, "agents", "openai.yaml"));
        Assert.DoesNotContain("disable-model-invocation", skill);
        Assert.Contains($"description: {template.Description}\n", skill);
        Assert.Equal("policy:\n  allow_implicit_invocation: false\n", policy);
    }

    [Fact]
    public void SyncCodexSkill_AutomaticInvocation_RemovesStalePolicy()
    {
        var template = AutomaticSkill();
        var agentsDir = Path.Combine(_testDir, ".agents", "skills", template.Name, "agents");
        Directory.CreateDirectory(agentsDir);
        var policyFile = Path.Combine(agentsDir, "openai.yaml");
        File.WriteAllText(policyFile, "policy:\n  allow_implicit_invocation: false\n");

        SyncCommand.SyncCodexSkill(template, _testDir);

        Assert.False(File.Exists(policyFile));
        Assert.False(Directory.Exists(agentsDir));
    }

    [Fact]
    public void SyncSkill_RepeatEmission_IsByteIdenticalIncludingInvocationPolicy()
    {
        var template = ExplicitSkill();
        SyncCommand.SyncSkill(template, _testDir);
        SyncCommand.SyncCodexSkill(template, _testDir);
        var files = new[]
        {
            Path.Combine(_testDir, ".claude", "skills", template.Name, "SKILL.md"),
            Path.Combine(_testDir, ".agents", "skills", template.Name, "SKILL.md"),
            Path.Combine(_testDir, ".agents", "skills", template.Name, "agents", "openai.yaml"),
        };
        var first = files.ToDictionary(path => path, File.ReadAllBytes);

        SyncCommand.SyncSkill(template, _testDir);
        SyncCommand.SyncCodexSkill(template, _testDir);

        Assert.All(files, path => Assert.Equal(first[path], File.ReadAllBytes(path)));
    }

    // Which skills are human-only is DR 045 section 9's to decide and the taxonomy Issues' to set,
    // so the invocation fixtures take whatever the shipped set currently declares.
    private SkillTemplate ExplicitSkill() =>
        SkillTemplateService.DiscoverSkills().First(skill => skill.ExplicitInvocation);

    private SkillTemplate AutomaticSkill() =>
        SkillTemplateService.DiscoverSkills()
            .First(skill => !skill.ExplicitInvocation && !skill.EmitAgent);

    [Fact]
    public void SyncAgent_DescriptionsPassThroughExactly()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
        var skill = File.ReadAllText(
            Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md"));
        Assert.Contains($"description: {_reviewer.Description}\n", agent);
        Assert.Contains($"description: {_reviewer.Description}\n", skill);
    }

    [Fact]
    public void GetSkillResources_SkillWithoutReferences_IsEmpty()
    {
        Assert.Empty(TemplateGenerator.GetSkillResources("docs-writer"));
    }

    // Workflow harnesses are dydo-authored (Templates/workflow-<name>.js) and compiled to
    // .claude/workflows — hand-editing the emitted scripts is the drift the compiler ends.
    // The emitted set IS the shipped set, so retiring a harness retires its output.
    [Fact]
    public void SyncWorkflows_EmitsExactlyTheShippedHarnessSet()
    {
        var shipped = Directory.GetFiles(Path.Combine(RepositoryRoot(), "Templates"), "workflow-*.js")
            .Select(path => Path.GetFileName(path)!["workflow-".Length..])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var count = SyncCommand.SyncWorkflows(_testDir);

        var workflowDir = Path.Combine(_testDir, ".claude", "workflows");
        var emitted = Directory.GetFiles(workflowDir, "*.js")
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(shipped, emitted);
        Assert.Equal(shipped.Count, count);
        Assert.Contains("inquisition.js", emitted);
        Assert.DoesNotContain("run-sprint.js", emitted);

        foreach (var file in Directory.GetFiles(workflowDir, "*.js"))
        {
            var content = File.ReadAllText(file);
            Assert.Contains("export const meta", content);
            Assert.DoesNotContain("\r", content); // LF-normalized for Claude Code
        }
    }

    [Fact]
    public void SyncCodexAgent_EmitsStrongOpenAiModelBinding()
    {
        SyncCommand.SyncCodexAgent(_reviewer, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml"));
        Assert.Contains("model = \"gpt-5.6-sol\"", agent);
    }

    [Fact]
    public void SyncCodexAgent_WithoutModelBinding_UsesStandardOpenAiFallback()
    {
        SyncCommand.SyncCodexAgent(_reviewer, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml"));
        Assert.Contains("model = \"gpt-5.6-terra\"", agent);
    }

    [Theory]
    [InlineData("reviewer")]
    [InlineData("inquisitor")]
    public void SyncCodexAgent_ReadOnlyWorker_EmitsSingleReadOnlySandboxImmediatelyAfterModel(string skillName)
    {
        var skill = SkillTemplateService.DiscoverSkills().Single(s => s.Name == skillName);

        SyncCommand.SyncCodexAgent(skill, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", $"{skillName}.toml"));
        var lines = agent.Split('\n');
        var modelIndex = Array.FindIndex(lines, line => line.StartsWith("model = \"", StringComparison.Ordinal));

        Assert.StartsWith($"name = \"{skillName}\"\ndescription = \"", agent);
        Assert.True(modelIndex >= 0, "Codex agent must emit a quoted model line.");
        Assert.Equal("sandbox_mode = \"read-only\"", lines[modelIndex + 1]);
        Assert.Equal(1, lines.Count(line => line == "sandbox_mode = \"read-only\""));
        Assert.DoesNotContain('\r', agent);
    }

    // DR 045 section 10: a Codex writing agent needs the workspace-write sandbox, or it cannot act
    // on the methodology it was told to load. Read-only agents keep their narrower sandbox.
    [Fact]
    public void SyncCodexAgent_WritableWorker_GetsWorkspaceWriteSandbox()
    {
        var codeWriter = SkillTemplateService.DiscoverSkills().Single(s => s.Name == "code-writer");

        SyncCommand.SyncCodexAgent(codeWriter, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "code-writer.toml"));
        var lines = agent.Split('\n');
        var modelIndex = Array.FindIndex(lines, line => line.StartsWith("model = \"", StringComparison.Ordinal));

        Assert.True(modelIndex >= 0, "Codex agent must emit a quoted model line.");
        Assert.Equal("sandbox_mode = \"workspace-write\"", lines[modelIndex + 1]);
        Assert.Equal(1, lines.Count(line => line.StartsWith("sandbox_mode", StringComparison.Ordinal)));
    }

    [Fact]
    public void SyncCodexAgent_ReadOnlySkillWithQuotedDescription_EscapesTomlAndUsesLf()
    {
        var reviewer = new SkillTemplate
        {
            Name = "reviewer",
            TemplateFile = "skill-reviewer.template.md",
            Description = "Project \"reviewer\".",
            EmitAgent = true,
            ReadOnly = true,
        };

        SyncCommand.SyncCodexAgent(reviewer, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml"));
        Assert.Contains("description = \"Project \\\"reviewer\\\".\"", agent);
        Assert.Contains("model = \"gpt-5.6-sol\"\nsandbox_mode = \"read-only\"\n\ndeveloper_instructions = \"\"\"", agent);
        Assert.DoesNotContain('\r', agent);
    }

    [Fact]
    public void SyncCodexAgent_SecondIsolatedEmit_IsByteIdentical()
    {
        var skills = SkillTemplateService.DiscoverSkills()
            .Where(skill => skill.Name is "reviewer" or "inquisitor" or "code-writer")
            .ToList();

        foreach (var skill in skills)
            SyncCommand.SyncCodexAgent(skill, _testDir, ConfigFactory.CreateDefaultModels());

        var firstEmit = skills.ToDictionary(
            skill => skill.Name,
            skill => File.ReadAllBytes(Path.Combine(_testDir, ".codex", "agents", $"{skill.Name}.toml")));

        foreach (var skill in skills)
            SyncCommand.SyncCodexAgent(skill, _testDir, ConfigFactory.CreateDefaultModels());

        foreach (var skill in skills)
        {
            var path = Path.Combine(_testDir, ".codex", "agents", $"{skill.Name}.toml");
            Assert.Equal(firstEmit[skill.Name], File.ReadAllBytes(path));
        }
    }

    [Theory]
    [InlineData("code-writer", "gpt-5.6-terra")]
    [InlineData("docs-writer", "gpt-5.6-terra")]
    public void SyncCodexAgent_DefaultModels_EmitsTierCorrectModel(string skillName, string expectedModel)
    {
        var skill = SkillTemplateService.DiscoverSkills().First(s => s.Name == skillName);
        SyncCommand.SyncCodexAgent(skill, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", $"{skillName}.toml"));
        Assert.Contains($"model = \"{expectedModel}\"", agent);
    }

    [Fact]
    public void SyncCodexAgent_EmitsDeveloperInstructions()
    {
        SyncCommand.SyncCodexAgent(_reviewer, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", "reviewer.toml"));
        var mustReads = SyncCommand.ExtractMustReads(_reviewer, _testDir);
        Assert.NotEmpty(mustReads);
        Assert.Contains("developer_instructions = \"\"\"", agent);
        Assert.Contains("Read these for project context before working:", agent);
        Assert.All(mustReads, path => Assert.Contains($"- {path}", agent));
        Assert.DoesNotContain("must_reads", agent);
        Assert.DoesNotContain(agent.Split('\n'), line => line.StartsWith("instructions = \"\"\""));
    }

    // Issue 0271 (wire-shape guard, same class as 0261): codex's agent `tools` field is a
    // ToolsToml struct of codex toggles (view_image, web_search), NOT file/shell tool names.
    // The old emitter wrote `tools = "read, grep, glob, bash, ..."` — a bare string codex
    // rejects with 'invalid type: string ... expected struct ToolsToml', silently ignoring
    // every agent. The fix drops the field; these pin that no agent emits it,
    // for either the read-only or the read-write branch.
    [Theory]
    [InlineData("reviewer")]
    [InlineData("project-planner")]
    [InlineData("issue-planner")]
    [InlineData("code-writer")]
    public void SyncCodexAgent_OmitsToolsField(string skillName)
    {
        var skill = SkillTemplateService.DiscoverSkills().First(s => s.Name == skillName);

        SyncCommand.SyncCodexAgent(skill, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", $"{skillName}.toml"));
        Assert.DoesNotContain(agent.Split('\n'), line => line.TrimStart().StartsWith("tools"));
        // Fields codex does accept remain intact — the drop is surgical, not structural.
        Assert.Contains($"name = \"{skillName}\"", agent);
        Assert.Contains("description = \"", agent);
        Assert.Contains("model = \"", agent);
        Assert.Contains("developer_instructions = \"\"\"", agent);
    }

    [Fact]
    public void SyncAgent_Agent_HasReadOnlyToolProfileAndFrontmatter()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir);
        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));

        Assert.Contains("name: reviewer\n", agent);
        // Read-only skill → no Edit/Write tools (that's how "reviewers don't write code" is native-enforced)
        Assert.Contains("tools: Read, Grep, Glob, Bash, Skill\n", agent);
        Assert.DoesNotContain("Edit", ToolsLine(agent));
        Assert.DoesNotContain("Write", ToolsLine(agent));
        // Carries this skill's own project-context must-reads, whatever its source names.
        var mustReads = SyncCommand.ExtractMustReads(_reviewer, _testDir);
        Assert.NotEmpty(mustReads);
        Assert.All(mustReads, path => Assert.Contains($"- {path}", agent));
    }

    // DR 045 section 10: an agent definition is a thin identity wrapper, and the compiler must
    // make the skill actually reach the spawned agent. A `tools` allowlist without Skill gives no
    // guarantee the Skill tool is available, and `skills:` is what preloads the skill's content.
    [Theory]
    [InlineData("reviewer")]
    [InlineData("project-planner")]
    [InlineData("issue-planner")]
    [InlineData("code-writer")]
    [InlineData("docs-writer")]
    [InlineData("inquisitor")]
    public void SyncAgent_Agent_PreloadsItsOwnSkillAndCarriesTheSkillTool(string skillName)
    {
        var skill = SkillTemplateService.DiscoverSkills().Single(s => s.Name == skillName);

        SyncCommand.SyncAgent(skill, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", $"{skillName}.md"));
        Assert.Contains($"skills: [{skillName}]\n", agent);
        Assert.Contains("Skill", ToolsLine(agent));
    }

    // `delegates: true` is the only thing that grants the Agent tool: a worker that could fan out
    // would turn a reviewed one-writer contract into an unreviewed tree of writers.
    [Fact]
    public void SyncAgent_DelegatingSkill_GetsTheAgentTool()
    {
        // No shipped skill delegates, so the flag is set on a constructed template; its
        // TemplateFile names a shipped template because that is where the body comes from.
        var delegator = new SkillTemplate
        {
            Name = "delegator",
            TemplateFile = "skill-reviewer.template.md",
            Description = "Keeps several Issues in flight as sub-agents.",
            EmitAgent = true,
            Delegates = true,
        };

        SyncCommand.SyncAgent(delegator, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "delegator.md"));
        Assert.Contains("Agent", ToolsLine(agent));
    }

    [Theory]
    [InlineData("reviewer")]
    [InlineData("code-writer")]
    public void SyncAgent_WorkerWithoutDelegation_NeverGetsTheAgentTool(string skillName)
    {
        var skill = SkillTemplateService.DiscoverSkills().Single(s => s.Name == skillName);
        Assert.False(skill.Delegates);

        SyncCommand.SyncAgent(skill, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", $"{skillName}.md"));
        Assert.DoesNotContain("Agent", ToolsLine(agent));
    }

    // Codex has no `skills:` preload, so the load line is the only thing carrying the methodology
    // into the spawned agent.
    [Theory]
    [InlineData("reviewer")]
    [InlineData("code-writer")]
    public void SyncCodexAgent_DeveloperInstructions_NameTheSkillToLoad(string skillName)
    {
        var skill = SkillTemplateService.DiscoverSkills().Single(s => s.Name == skillName);

        SyncCommand.SyncCodexAgent(skill, _testDir, ConfigFactory.CreateDefaultModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".codex", "agents", $"{skillName}.toml"));
        Assert.Contains($"Load the `${skillName}` skill before working.", agent);
    }

    private static string ToolsLine(string agent) =>
        agent.Split('\n').Single(line => line.StartsWith("tools: ", StringComparison.Ordinal));

    // DR 045 section 10: the compiler used to drop ## Must-Reads, so every coordinating skill compiled
    // without its context pointers and {{include:extra-must-reads}} silently resolved to nothing.
    // Every authored section now survives into the compiled body.
    [Fact]
    public void SyncAgent_Skill_KeepsEveryAuthoredSection_MustReadsIncluded()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir);
        var template = TemplateGenerator.ReadBuiltInTemplate(_reviewer.TemplateFile);
        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "reviewer", "SKILL.md"));

        foreach (var heading in Headings(template))
            Assert.Contains(heading, skill);

        Assert.Contains("## Must-Reads", skill);
        // The {{AGENT_NAME}} placeholder is de-personalized and no include tag survives unresolved.
        Assert.DoesNotContain("{{AGENT_NAME}}", skill);
        Assert.DoesNotContain("{{include:", skill);
    }

    // Both hosts emit SKILL.md three levels below the project root, so one climb serves both; a
    // link authored for Templates/ lands one folder short of dydo/ and resolves to nothing.
    [Theory]
    [InlineData(".claude")]
    [InlineData(".agents")]
    public void SyncAgent_Skill_LinksResolveFromTheEmittedSkillFolder(string host)
    {
        // Materialize whatever this skill actually points at, so the check follows its source.
        var mustReads = SyncCommand.ExtractMustReads(_reviewer, _testDir);
        Assert.NotEmpty(mustReads);
        foreach (var path in mustReads)
        {
            var file = Path.Combine(_testDir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "# context");
        }

        if (host == ".claude") SyncCommand.SyncAgent(_reviewer, _testDir);
        else SyncCommand.SyncCodexAgent(_reviewer, _testDir);

        var skillDir = Path.Combine(_testDir, host, "skills", "reviewer");
        var skill = File.ReadAllText(Path.Combine(skillDir, "SKILL.md"));

        foreach (var path in mustReads)
        {
            Assert.Contains($"(../../../{path})", skill);
            Assert.True(
                File.Exists(Path.GetFullPath(Path.Combine(skillDir, "../../../" + path))),
                $"the compiled must-read link '{path}' must resolve from the emitted skill folder");
        }

        // A rubric link becomes the host's emitted path: a preloaded agent reads its skill from
        // context and has no folder to resolve a relative link against.
        var rubricLinks = Regex.Matches(skill, @"\(([^)\s]*resources/[^)\s]+)\)")
            .Select(match => match.Groups[1].Value)
            .ToList();
        Assert.NotEmpty(rubricLinks);
        Assert.All(rubricLinks, link => Assert.StartsWith($"{host}/skills/reviewer/resources/", link));
        // At least one resolves to an emitted rubric; a link renamed but not yet rewritten is the
        // tolerated window the plan names, not a compiler defect.
        Assert.Contains(rubricLinks, link => File.Exists(Path.Combine(
            _testDir, link.Replace('/', Path.DirectorySeparatorChar))));
    }

    // The rewrite is a fixed point and only touches what it must: prose in parentheses and
    // absolute URLs are not links to rewrite, and an already-rewritten body compiles unchanged.
    [Fact]
    public void RewriteSkillLinks_IsIdempotent_AndLeavesNonPathTargetsAlone()
    {
        const string body = "[a](../../../understand/about.md) [b](dydo/index.md) [c](resources/merge.md)\n"
            + "[d](https://linear.app/x) [e](Linear URL) [f](#anchor)";

        var once = SyncCommand.RewriteSkillLinks(body, "reviewer", ".claude/skills");

        Assert.Equal(
            "[a](../../../dydo/understand/about.md) [b](../../../dydo/index.md) "
            + "[c](.claude/skills/reviewer/resources/merge.md)\n"
            + "[d](https://linear.app/x) [e](Linear URL) [f](#anchor)",
            once);
        Assert.Equal(once, SyncCommand.RewriteSkillLinks(once, "reviewer", ".claude/skills"));
    }

    // A resource body is authored one folder deeper than SKILL.md, so its climbs already resolve
    // from resources/. The skill-body rewrite must not reach it.
    [Fact]
    public void SyncAgent_SkillResources_AreCopiedVerbatim()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir);

        foreach (var (fileName, expected) in TemplateGenerator.GetSkillResources("reviewer"))
        {
            var emitted = File.ReadAllText(Path.Combine(
                _testDir, ".claude", "skills", "reviewer", "resources", fileName));
            Assert.Equal(expected.Replace("\r\n", "\n"), emitted);
        }
    }

    // {{include:extra-must-reads}} is the project's hook for adding its own context pointers.
    // While the compiler dropped ## Must-Reads it resolved into a section nobody ever saw.
    [Fact]
    public void SyncSkill_ResolvesTheExtraMustReadsInclude()
    {
        var additions = Path.Combine(_testDir, "dydo", "_system", "template-additions");
        Directory.CreateDirectory(additions);
        File.WriteAllText(Path.Combine(additions, "extra-must-reads.md"),
            "9. [house-rules.md](../../../guides/house-rules.md)");
        var template = SkillTemplateService.DiscoverSkills().Single(s => s.Name == "chief-of-staff");

        SyncCommand.SyncSkill(template, _testDir);

        var skill = File.ReadAllText(
            Path.Combine(_testDir, ".claude", "skills", "chief-of-staff", "SKILL.md"));
        Assert.Contains("## Must-Reads", skill);
        Assert.Contains("[house-rules.md](../../../dydo/guides/house-rules.md)", skill);
        Assert.DoesNotContain("{{include:", skill);
    }

    /// <summary>
    /// Counts H1 lines outside code fences. A fenced "# ..." line is shell prose, not a heading:
    /// the shipped doc templates and the guides carry them inside ```bash blocks, and a procedure
    /// guide or a skill skeleton may well do the same. A naive line count would read those as extra
    /// H1s and fail a document that is structurally correct. Fence handling mirrors
    /// SyncCommand.RenumberOrderedLists, which skips fenced content for the same reason.
    /// </summary>
    internal static int H1Count(string content)
    {
        var count = 0;
        var inFence = false;
        foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (!inFence && line.StartsWith("# ", StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    [Fact]
    public void H1Count_CountsHeadingsOutsideFencesOnly()
    {
        var fenced = "# Title\n\n```bash\n# not a heading\n```\n\nprose";
        Assert.Equal(1, H1Count(fenced));
        Assert.Equal(2, H1Count("# One\n\nprose\n\n# Two"));
        Assert.Equal(0, H1Count("```\n# fenced only\n```"));
    }

    /// <summary>
    /// Collapses the two hosts' skill roots to one token so a compiled body can be compared
    /// across hosts. SyncCommand.RewriteSkillLinks turns a skill's own <c>resources/&lt;n&gt;.md</c>
    /// link into the host's emitted path — <c>.claude/skills/…</c> for Claude, <c>.agents/skills/…</c>
    /// for Codex — so a skill that links its own resource legitimately differs at exactly those
    /// links and nowhere else. Only the prefix is normalized; everything after it, the skill name
    /// and the resource file included, still compares byte-exact, so real divergence still fails.
    /// </summary>
    internal static string NormalizeHostSkillRoot(string content) =>
        content
            .Replace(".claude/skills/", "<host>/skills/", StringComparison.Ordinal)
            .Replace(".agents/skills/", "<host>/skills/", StringComparison.Ordinal);

    [Fact]
    public void NormalizeHostSkillRoot_CollapsesTheHostPrefixAndNothingElse()
    {
        const string claude = "read [merge](.claude/skills/reviewer/resources/merge.md) first";
        const string codex = "read [merge](.agents/skills/reviewer/resources/merge.md) first";

        Assert.Equal(NormalizeHostSkillRoot(claude), NormalizeHostSkillRoot(codex));
        // Both segments the summary above enumerates, because this fixture is the only guard on
        // the helper's width: a normalization that also collapsed the skill folder would still
        // pass the resource-file inversion, and would then hide a genuinely divergent link.
        Assert.NotEqual(
            NormalizeHostSkillRoot(claude),
            NormalizeHostSkillRoot(codex.Replace("reviewer/", "project-planner/")));
        Assert.NotEqual(
            NormalizeHostSkillRoot(claude),
            NormalizeHostSkillRoot(codex.Replace("merge.md", "plan.md")));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the DynaDocs repository root.");
    }

    private static IEnumerable<string> Headings(string template) =>
        template.Replace("\r\n", "\n").Split('\n')
            .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(line => line.TrimEnd());

    [Fact]
    public void ExtractMethodology_StripsFrontmatter()
    {
        var methodology = SyncCommand.ExtractMethodology(_reviewer, _testDir);
        // The skill-template frontmatter (agent:/mode:) must not leak into the skill body
        Assert.DoesNotContain("mode: reviewer", methodology);
        Assert.Equal(1, H1Count(methodology));
        // No dangling horizontal rule at the end after dropping the trailing section
        Assert.False(methodology.TrimEnd().EndsWith("---"));
    }

    [Fact]
    public void SyncAgent_WritingSkill_GetsWriterToolsAndStance()
    {
        var codeWriter = SkillTemplateService.DiscoverSkills().First(s => s.Name == "code-writer");
        SyncCommand.SyncAgent(codeWriter, _testDir);

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "code-writer.md"));
        // A writing agent gets Edit/Write AND writer-stance prose — not the read-only contradiction
        Assert.Contains("Edit, Write", agent);
        Assert.Contains("produce and modify the project's files", agent);
        Assert.DoesNotContain("read-only", agent);

        // The skill description must be skill-correct, not reviewer-hardcoded
        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "code-writer", "SKILL.md"));
        Assert.DoesNotContain("reviewing a code change", skill);
        Assert.Contains($"description: {codeWriter.Description}\n", skill);
    }

    // Each skill points at its own context: the extracted list is normalized to dydo-relative
    // paths the agent prompt can hand to Read, every entry is named by that skill's own template,
    // and the lists are not one shared default. Which documents a skill names is its source's
    // business, so nothing here pins a filename.
    [Fact]
    public void ExtractMustReads_AreSkillSpecific_AndComeFromTheSkillsOwnTemplate()
    {
        var skills = SkillTemplateService.DiscoverSkills();
        var lists = skills.ToDictionary(
            skill => skill.Name, skill => SyncCommand.ExtractMustReads(skill, _testDir));

        foreach (var skill in skills)
        {
            var template = TemplateGenerator.ReadBuiltInTemplate(skill.TemplateFile).Replace("\r\n", "\n");
            foreach (var path in lists[skill.Name])
            {
                Assert.StartsWith("dydo/", path);
                Assert.Contains(path["dydo/".Length..], template);
            }
        }

        Assert.True(
            lists.Values.Where(list => list.Count > 0)
                .Select(list => string.Join('|', list))
                .Distinct()
                .Count() > 1,
            "must-reads must differ between skills; one shared list is not skill-specific");
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
    public void SyncCommand_Run_GeneratesAllAgents()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            foreach (var name in new[] { "code-writer", "reviewer", "docs-writer" })
            {
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{name}.md")), $"missing agent: {name}");
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", name, "SKILL.md")), $"missing skill: {name}");
                Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", $"{name}.toml")), $"missing codex agent: {name}");
                Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", name, "SKILL.md")), $"missing repo skill: {name}");
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
        // Documented Codex tool names plus the legacy shell names kept from issue 0295; the
        // Claude-only UI names Codex never emits are dropped.
        Assert.Equal("Bash|apply_patch|Edit|Write|Agent|shell_command|exec|local_shell|unified_exec", matcher);

        var stop = Assert.IsType<JsonArray>(hooks["Stop"]);
        Assert.Contains(stop, entry => entry?.ToJsonString().Contains("dydo guard --stop") == true);
    }

    [Fact]
    public void SyncCommand_Run_GeneratesBothSpawnablePlanningAgents()
    {
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            var retiredPlannerArtifacts = new[]
            {
                Path.Combine(_testDir, ".claude", "agents", "planner.md"),
                Path.Combine(_testDir, ".claude", "skills", "planner", "resources", "project.md"),
                Path.Combine(_testDir, ".claude", "skills", "planner", "resources", "issue.md"),
                Path.Combine(_testDir, ".codex", "agents", "planner.toml"),
                Path.Combine(_testDir, ".agents", "skills", "planner", "resources", "project.md"),
                Path.Combine(_testDir, ".agents", "skills", "planner", "resources", "issue.md")
            };
            foreach (var artifact in retiredPlannerArtifacts)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
                File.WriteAllText(artifact, "stale generic planner output");
            }
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            foreach (var skillName in new[] { "project-planner", "issue-planner" })
            {
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", skillName, "SKILL.md")));
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{skillName}.md")));
                Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", skillName, "SKILL.md")));
                Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", $"{skillName}.toml")));
            }

            Assert.All(retiredPlannerArtifacts, artifact => Assert.False(File.Exists(artifact), artifact));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void SyncAgent_PlanningSkillsWriteAgentAndSkillForBothHosts()
    {
        foreach (var skillName in new[] { "project-planner", "issue-planner" })
        {
            var planner = SkillTemplateService.DiscoverSkills().First(s => s.Name == skillName);
            Assert.True(planner.EmitAgent);

            SyncCommand.SyncAgent(planner, _testDir, ConfigFactory.CreateDefaultModels());
            SyncCommand.SyncCodexAgent(planner, _testDir, ConfigFactory.CreateDefaultModels());

            Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", skillName, "SKILL.md")));
            Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{skillName}.md")));
            Assert.True(File.Exists(Path.Combine(_testDir, ".agents", "skills", skillName, "SKILL.md")));
            Assert.True(File.Exists(Path.Combine(_testDir, ".codex", "agents", $"{skillName}.toml")));
        }
    }

    [Fact]
    public void SelfImprovementSkill_CompilesForBothSurfacesWithoutAgentDefinitions()
    {
        var template = SkillTemplateService.DiscoverSkills().First(s => s.Name == "self-improvement");

        SyncCommand.SyncSkill(template, _testDir);
        SyncCommand.SyncCodexSkill(template, _testDir);

        var claudeSkill = Path.Combine(_testDir, ".claude", "skills", "self-improvement", "SKILL.md");
        var codexSkill = Path.Combine(_testDir, ".agents", "skills", "self-improvement", "SKILL.md");
        Assert.True(File.Exists(claudeSkill));
        Assert.True(File.Exists(codexSkill));

        var claudeContent = File.ReadAllText(claudeSkill);
        var codexContent = File.ReadAllText(codexSkill);
        Assert.Equal(
            NormalizeHostSkillRoot(claudeContent), NormalizeHostSkillRoot(codexContent));
        Assert.Contains($"description: {template.Description}\n", claudeContent);
        Assert.Equal(1, H1Count(FrontmatterParser.StripFrontmatter(claudeContent)));
        Assert.DoesNotContain("{{", claudeContent);

        Assert.DoesNotContain('\r', claudeContent);
        Assert.DoesNotContain('\r', codexContent);
        Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", "self-improvement.md")));
        Assert.False(File.Exists(Path.Combine(_testDir, ".codex", "agents", "self-improvement.toml")));
    }

    [Fact]
    public void MattDerivedSkills_CompileAsSkills_WithWayfinderSemanticStructure()
    {
        var skillNames = new[]
        {
            "wayfinder", "grilling", "grill-me", "bro", "writing-for-agents"
        };
        var compiled = new Dictionary<string, string>();

        foreach (var skillName in skillNames)
        {
            var template = SkillTemplateService.DiscoverSkills().Single(s => s.Name == skillName);
            SyncCommand.SyncSkill(template, _testDir);
            SyncCommand.SyncCodexSkill(template, _testDir);

            var claudeSkill = Path.Combine(_testDir, ".claude", "skills", skillName, "SKILL.md");
            var codexSkill = Path.Combine(_testDir, ".agents", "skills", skillName, "SKILL.md");
            var claudeContent = File.ReadAllText(claudeSkill);
            var codexContent = File.ReadAllText(codexSkill);

            // One authored source, so both hosts compile the same body — except where the
            // compiler writes the host's own skill root into a link to the skill's resources.
            Assert.Equal(
                NormalizeHostSkillRoot(FrontmatterParser.StripFrontmatter(claudeContent)),
                NormalizeHostSkillRoot(FrontmatterParser.StripFrontmatter(codexContent)));
            Assert.Contains($"name: {skillName}", claudeContent);
            Assert.Contains("mattpocock/skills", claudeContent);
            Assert.DoesNotContain('\r', claudeContent);
            Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{skillName}.md")));
            Assert.False(File.Exists(Path.Combine(_testDir, ".codex", "agents", $"{skillName}.toml")));

            compiled[skillName] = claudeContent;
        }

        // The glossary retired the Waypoint ontology (DR 045 section 11); no compiled skill may
        // reintroduce it, and each keeps its own H1 plus the sections its source authored.
        foreach (var (skillName, content) in compiled)
        {
            var body = FrontmatterParser.StripFrontmatter(content);
            Assert.Equal(1, H1Count(body));
            Assert.DoesNotContain("Waypoint", body, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                Headings(TemplateGenerator.ReadBuiltInTemplate($"skill-{skillName}.template.md")).ToList(),
                Headings(body).ToList());
        }
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

    // DR-039: the sprint-auditor folded into the reviewer, and DR 045 renamed its rubric to
    // `merge`. The reviewer ships one rubric per review target and nothing under the old name.
    [Fact]
    public void SyncAgent_Reviewer_ShipsOneRubricPerReviewTarget()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir);

        var resources = Path.Combine(_testDir, ".claude", "skills", "reviewer", "resources");
        var emitted = Directory.GetFiles(resources, "*.md")
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .ToList();

        Assert.Contains("merge", emitted);
        Assert.Contains("code", emitted);
        Assert.Contains("project-plan", emitted);
        Assert.Contains("issue-plan", emitted);
        Assert.DoesNotContain("plan", emitted);
        Assert.Contains("docs", emitted);
        Assert.Contains("tests", emitted);
        Assert.All(emitted, name => Assert.NotEmpty(File.ReadAllText(Path.Combine(resources, $"{name}.md"))));
    }

    [Fact]
    public void SyncAgent_EmitsLfLineEndings()
    {
        // CRLF in .claude/ artifacts makes Claude Code's permission handler reject them
        // ("control characters that would be hidden in the approval dialog"), so sync
        // must emit LF regardless of platform/template line endings.
        SyncCommand.SyncAgent(_reviewer, _testDir);

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
        Agents = new Dictionary<string, string>
        {
            ["reviewer"] = "strong",
            ["code-writer"] = "standard",
            ["docs-writer"] = "light" // tier NOT bound in the vendor map
        }
    };

    [Fact]
    public void ResolveModel_MappedAgent_ReturnsConcreteModel()
    {
        var model = SyncCommand.ResolveModel(TestModels(), "reviewer");
        Assert.Equal("model-strong", model);
    }

    [Fact]
    public void ResolveModel_OpenAiDefault_ReturnsStrongTierModel()
    {
        var model = SyncCommand.ResolveModel(ConfigFactory.CreateDefaultModels(), "reviewer", "openai");

        Assert.Equal("gpt-5.6-sol", model);
    }

    [Theory]
    [InlineData("reviewer", "gpt-5.6-sol")]
    [InlineData("project-planner", "gpt-5.6-sol")]
    [InlineData("issue-planner", "gpt-5.6-sol")]
    [InlineData("issue-captain", "gpt-5.6-sol")]
    [InlineData("code-writer", "gpt-5.6-terra")]
    [InlineData("docs-writer", "gpt-5.6-terra")]
    public void ResolveModel_OpenAiDefault_UsesAgentTier(string agentName, string expectedModel)
    {
        var model = SyncCommand.ResolveModel(ConfigFactory.CreateDefaultModels(), agentName, "openai");

        Assert.Equal(expectedModel, model);
    }

    [Fact]
    public void ResolveModel_UnmappedAgent_ReturnsNull()
    {
        // No agent → tier entry: inherit the session model (Decision 028 — no silent downgrade).
        var model = SyncCommand.ResolveModel(TestModels(), "project-planner");
        Assert.Null(model);
    }

    [Fact]
    public void ResolveModel_TierMissingFromVendorMap_ReturnsNull()
    {
        // docs-writer maps to "light", which the vendor map does not bind → inherit.
        var model = SyncCommand.ResolveModel(TestModels(), "docs-writer");
        Assert.Null(model);
    }

    [Fact]
    public void ResolveModel_AbsentModelsSection_ReturnsNull()
    {
        var model = SyncCommand.ResolveModel(null, "reviewer");
        Assert.Null(model);
    }

    [Fact]
    public void SyncAgent_WithModels_EmitsResolvedModelFrontmatter()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir, TestModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
        Assert.Contains("\nmodel: model-strong\n", agent);
        Assert.DoesNotContain("model: inherit", agent);
    }

    [Fact]
    public void SyncAgent_UnmappedAgent_FallsBackToInherit()
    {
        var projectPlanner = SkillTemplateService.DiscoverSkills().First(s => s.Name == "project-planner");
        SyncCommand.SyncAgent(projectPlanner, _testDir, TestModels());

        var agent = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "project-planner.md"));
        Assert.Contains("model: inherit", agent);
    }

    [Fact]
    public void SyncAgent_NoModelsSection_FallsBackToInherit()
    {
        SyncCommand.SyncAgent(_reviewer, _testDir);

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
                    "agents": { "reviewer": "strong" }
                  }
                }
                """);
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            var reviewer = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "reviewer.md"));
            Assert.Contains("model: vendor-strong-model", reviewer);
            // Unmapped agents inherit the session model
            var codeWriter = File.ReadAllText(Path.Combine(_testDir, ".claude", "agents", "code-writer.md"));
            Assert.Contains("model: inherit", codeWriter);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void DefaultModels_ResolveForAllTieredAgents()
    {
        // The shipped defaults (Decision 028) must actually bind: every agent in the
        // default agent → tier map resolves to a concrete model.
        var models = ConfigFactory.CreateDefaultModels();
        foreach (var agent in models.Agents.Keys)
        {
            var model = SyncCommand.ResolveModel(models, agent);
            Assert.False(string.IsNullOrEmpty(model), $"default tier for '{agent}' did not resolve");
        }
    }

    // --- Skill-only coordinating methodologies ---

    [Fact]
    public void SyncCommand_Run_GeneratesCoordinatingSkills_ButNoAgents()
    {
        // Coordinating methodologies are invokable skills, not spawnable agent definitions.
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{\"version\":1}");
            Directory.SetCurrentDirectory(_testDir);

            SyncCommand.Create().Parse([]).Invoke();

            foreach (var name in new[] { "co-thinker", "chief-of-staff", "admiral" })
            {
                Assert.True(File.Exists(Path.Combine(_testDir, ".claude", "skills", name, "SKILL.md")),
                    $"missing coordinating skill: {name}");
                Assert.False(File.Exists(Path.Combine(_testDir, ".claude", "agents", $"{name}.md")),
                    $"coordinating skill '{name}' must NOT get a native agent definition");
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Theory]
    [InlineData("co-thinker")]
    [InlineData("chief-of-staff")]
    [InlineData("admiral")]
    public void SkillOnlyTemplates_CompileWithoutTheRetiredTierDoctrine(string skillName)
    {
        var template = SkillTemplateService.DiscoverSkills().First(s => s.Name == skillName);
        SyncCommand.SyncSkill(template, _testDir);

        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", skillName, "SKILL.md"));
        Assert.Contains($"name: {skillName}\n", skill);
        Assert.DoesNotContain("Managers Doctrine", skill);
        Assert.DoesNotContain("Tier-1", skill);
    }

    [Fact]
    public void ChiefOfStaff_Skill_CompilesItsSourceWithoutAPersonalMemoryPolicy()
    {
        var chief = SkillTemplateService.DiscoverSkills().First(s => s.Name == "chief-of-staff");
        SyncCommand.SyncSkill(chief, _testDir);

        var skill = File.ReadAllText(Path.Combine(_testDir, ".claude", "skills", "chief-of-staff", "SKILL.md"));
        Assert.Equal(
            Headings(TemplateGenerator.ReadBuiltInTemplate(chief.TemplateFile)).ToList(),
            Headings(skill).ToList());
        // dydo keeps durable knowledge in documents; a skill that carries its own memory policy
        // would be a second, unreviewable store.
        Assert.DoesNotContain("memory", skill, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompiledSkills_CarryNoRetiredRuntimeMachinery()
    {
        // Decision 026 sweep, widened from one skill to every skill: worker-tier dispatch,
        // .needs-merge markers and worktree-merge flows are dead command surfaces, and a skill
        // that still names one sends its agent at nothing. Retired vocabulary in skill prose is
        // Gate C's rg over Templates/, not this test's.
        foreach (var skill in SkillTemplateService.DiscoverSkills())
        {
            var methodology = SyncCommand.ExtractMethodology(skill, _testDir);

            foreach (var retired in new[]
            {
                ".needs-merge", "dydo worktree merge", "--role ", "callback",
            })
            {
                Assert.DoesNotContain(retired, methodology, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void CoThinkerTemplate_NamesNoRetiredRoleSwitchingCommand()
    {
        var raw = TemplateGenerator.ReadBuiltInTemplate("skill-co-thinker.template.md");

        Assert.DoesNotContain("dydo agent role", raw);
        Assert.DoesNotContain("--role ", raw);
    }
}
