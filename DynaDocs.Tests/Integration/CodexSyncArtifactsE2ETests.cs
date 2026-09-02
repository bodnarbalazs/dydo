namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Tests.Commands;
using DynaDocs.Utils;

/// <summary>
/// c1-7 / issue 0233 (ask 4): `dydo sync` emits a skill for every skill-only template without
/// minting an agent definition. Existing sync tests pin the ABSENCE of a
/// <c>.claude/agents/&lt;name&gt;.md</c>; this one closes the codex-side gap: a spawnable
/// <c>.codex/agents/&lt;name&gt;.toml</c> for a skill-only template would be artifact drift. Driven
/// through the real sync command on an initialized project, over the discovered set rather than a
/// hand-kept list.
/// </summary>
[Collection("Integration")]
public class CodexSyncArtifactsE2ETests : IntegrationTestBase
{
    [Fact]
    public async Task Sync_AllShippedSkillOnlyTemplates_EmitSkillsWithoutAgentDefinitions()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        var skillOnly = SkillTemplateService.DiscoverSkills()
            .Where(skill => !skill.EmitAgent)
            .Select(skill => skill.Name)
            .ToList();
        Assert.NotEmpty(skillOnly);

        foreach (var name in skillOnly)
        {
            // Skill on both surfaces: the methodology a session applies in its own thread.
            AssertFileExists($".claude/skills/{name}/SKILL.md");
            AssertFileExists($".agents/skills/{name}/SKILL.md");
            // But NO spawnable agent definition on either host.
            AssertFileNotExists($".claude/agents/{name}.md");
            AssertFileNotExists($".codex/agents/{name}.toml");
        }

        foreach (var name in new[] { "wayfinder", "grilling", "grill-me", "bro", "writing-for-agents" })
        {
            var claude = ReadFile($".claude/skills/{name}/SKILL.md");
            var codex = ReadFile($".agents/skills/{name}/SKILL.md");
            // Same body on both hosts, modulo the host skill root the compiler writes into a
            // link to the skill's own resources.
            Assert.Equal(
                SyncCommandTests.NormalizeHostSkillRoot(FrontmatterParser.StripFrontmatter(claude)),
                SyncCommandTests.NormalizeHostSkillRoot(FrontmatterParser.StripFrontmatter(codex)));
            Assert.DoesNotContain('\r', claude);
            Assert.DoesNotContain('\r', codex);
        }

        // Contrast: agent-emitting skills DO get a codex agent file — sync is emitting codex
        // artifacts, it just never mints a spawnable agent for a skill-only template.
        AssertFileExists(".codex/agents/code-writer.toml");
        AssertFileExists(".codex/agents/reviewer.toml");
    }

    [Fact]
    public async Task Sync_InvocationPolicy_CompilesToEachRuntimeWithoutChangingDescriptions()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        // Over every skill-only template, not two named ones: which skills are human-only is DR 045
        // section 9's to decide, and a fixture naming today's explicit skill goes red the day it
        // changes, with no later Issue owning this file to fix it.
        var skills = SkillTemplateService.DiscoverSkills().Where(skill => !skill.EmitAgent).ToList();
        Assert.Contains(skills, skill => skill.ExplicitInvocation);
        Assert.Contains(skills, skill => !skill.ExplicitInvocation);

        foreach (var skill in skills)
        {
            var claude = ReadFile($".claude/skills/{skill.Name}/SKILL.md");
            var codex = ReadFile($".agents/skills/{skill.Name}/SKILL.md");
            var policy = $".agents/skills/{skill.Name}/agents/openai.yaml";

            // The description survives compilation unchanged on both hosts — it is what routes.
            Assert.Contains($"description: {skill.Description}\n", claude);
            Assert.Contains($"description: {skill.Description}\n", codex);

            // Each host expresses the same policy its own way, and never the other host's way.
            Assert.Equal(skill.ExplicitInvocation, claude.Contains("disable-model-invocation: true"));
            Assert.DoesNotContain("disable-model-invocation", codex);

            if (skill.ExplicitInvocation)
                AssertFileContains(policy, "allow_implicit_invocation: false");
            else
                AssertFileNotExists(policy);
        }
    }

    /// <summary>
    /// Issue 0271: every compiled codex agent must parse for codex. The old emitter wrote
    /// <c>tools = "read, grep, ..."</c> — a bare string codex rejects ('invalid type: string ...
    /// expected struct ToolsToml'), so it silently ignored ALL six agents and the DR-024
    /// dual-compilation codex leg was non-functional. This drives the real sync command and pins
    /// the wire shape of every emitted <c>.codex/agents/*.toml</c>: no <c>tools</c> field, and the
    /// fields codex does accept present. The prior sync tests validated content, not codex-parseability.
    /// </summary>
    [Fact]
    public async Task Sync_CodexWorkerAgents_OmitToolsField_KeepAcceptedFields()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        var agentDir = Path.Combine(TestDir, ".codex", "agents");
        var tomls = Directory.GetFiles(agentDir, "*.toml");
        Assert.NotEmpty(tomls);

        foreach (var toml in tomls)
        {
            var content = File.ReadAllText(toml);
            var name = Path.GetFileNameWithoutExtension(toml);
            Assert.DoesNotContain(content.Split('\n'), line => line.TrimStart().StartsWith("tools"));
            Assert.Contains($"name = \"{name}\"", content);
            Assert.Contains("model = \"", content);
            Assert.Contains("developer_instructions = \"\"\"", content);
        }
    }

    // One authored source compiles to both runtimes, so no compiled skill may name a host or the
    // session choreography that would make it wrong on the other one.
    [Fact]
    public async Task Sync_CompiledSkills_StayRuntimeNeutral()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        var skills = Directory.GetFiles(
            Path.Combine(TestDir, ".agents", "skills"), "SKILL.md", SearchOption.AllDirectories);
        Assert.NotEmpty(skills);

        foreach (var file in skills)
        {
            var skill = File.ReadAllText(file);

            Assert.DoesNotContain("Claude Code", skill);
            Assert.DoesNotContain("Codex", skill);
            Assert.DoesNotContain("callback", skill, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("task or thread", skill, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Sync_CompiledSkills_UseLinearIssuesWithoutRepositoryWorkCommands()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        var skillRoot = Path.Combine(TestDir, ".agents", "skills");
        var forbidden = new[]
        {
            "dydo task",
            "dydo issue",
            "dydo review",
            string.Join('/', "project", "tasks"),
            string.Join('/', "project", "issues"),
        };
        var hits = new List<string>();

        foreach (var file in Directory.GetFiles(skillRoot, "*.md", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            foreach (var phrase in forbidden)
            {
                if (content.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    hits.Add($"{Path.GetRelativePath(TestDir, file)}: {phrase}");
            }
        }

        Assert.True(hits.Count == 0,
            $"Compiled skill still instructs repository work management:\n  {string.Join("\n  ", hits)}");

        // The positive half is structural: every compiled skill is a non-empty body under its
        // own H1. Which nouns it uses is the source's business.
        foreach (var file in Directory.GetFiles(skillRoot, "SKILL.md", SearchOption.AllDirectories))
        {
            var body = FrontmatterParser.StripFrontmatter(File.ReadAllText(file));
            Assert.NotEmpty(body.Trim());
            Assert.Equal(1, SyncCommandTests.H1Count(body));
        }
    }

    // The retirement has to survive `dydo init`, not just the shipped set.
    [Fact]
    public async Task InitThenSync_NeverCompilesARetiredSkill()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        Assert.NotEmpty(SyncCommand.RetiredSkills);
        foreach (var retired in SyncCommand.RetiredSkills)
        {
            AssertDirectoryNotExists($".claude/skills/{retired}");
            AssertDirectoryNotExists($".agents/skills/{retired}");
            AssertFileNotExists($".claude/agents/{retired}.md");
            AssertFileNotExists($".codex/agents/{retired}.toml");
        }

        // The sweep is not vacuous: the surviving skills still compile on both hosts.
        AssertFileExists(".claude/skills/reviewer/SKILL.md");
        AssertFileExists(".agents/skills/reviewer/SKILL.md");
    }

    private void AssertDirectoryNotExists(string relativePath) =>
        Assert.False(
            Directory.Exists(Path.Combine(TestDir, relativePath.Replace('/', Path.DirectorySeparatorChar))),
            $"expected no directory at {relativePath}");

    // DR 045 section 10: an agent definition only works if the skill reaches the spawned agent.
    // On Claude that is `skills:` plus the Skill tool; on Codex it is the load line, and a writer
    // additionally needs the sandbox that lets it act.
    [Fact]
    public async Task Sync_CompiledAgents_ReachTheirSkillOnBothHosts()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        var workers = SkillTemplateService.DiscoverSkills().Where(skill => skill.EmitAgent).ToList();
        Assert.NotEmpty(workers);

        foreach (var skill in workers)
        {
            var claude = ReadFile($".claude/agents/{skill.Name}.md");
            var toolsLine = claude.Split('\n').Single(line => line.StartsWith("tools: ", StringComparison.Ordinal));
            Assert.Contains($"skills: [{skill.Name}]", claude);
            Assert.Contains("Skill", toolsLine);
            Assert.Equal(skill.Delegates, toolsLine.Contains("Agent", StringComparison.Ordinal));

            var codex = ReadFile($".codex/agents/{skill.Name}.toml");
            Assert.Contains($"Load the `${skill.Name}` skill before working.", codex);
            Assert.Contains(
                skill.ReadOnly ? "sandbox_mode = \"read-only\"" : "sandbox_mode = \"workspace-write\"",
                codex);
        }
    }
}
