namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// c1-7 / issue 0233 (ask 4): `dydo sync` emits skills for every shipped skill-only role
/// (planner, the three coordinating skills, self-improvement, and the three prompt-level skills) without
/// minting an agent definition.
/// Existing sync tests pin the ABSENCE of a <c>.claude/agents/&lt;role&gt;.md</c>; this one closes the
/// codex-side gap: a spawnable <c>.codex/agents/&lt;role&gt;.toml</c> for any skill-only role would be
/// artifact drift. Driven through the real sync command on an initialized project.
/// Self-improvement is a generic harness method, not a worker identity.
/// </summary>
[Collection("Integration")]
public class CodexSyncArtifactsE2ETests : IntegrationTestBase
{
    [Fact]
    public async Task Sync_AllShippedSkillOnlyRoles_EmitSkillsWithoutAgentDefinitions()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        // Self-improvement is included as a generic skill, not as a worker identity.
        foreach (var role in new[]
        {
            "planner", "orchestrator", "co-thinker", "chief-of-staff", "self-improvement",
            "wayfinder", "grilling", "bro",
        })
        {
            // Skill on both surfaces: the methodology the skill-only role applies in its thread.
            AssertFileExists($".claude/skills/{role}/SKILL.md");
            AssertFileExists($".agents/skills/{role}/SKILL.md");
            // But NO spawnable agent definition on either host.
            AssertFileNotExists($".claude/agents/{role}.md");
            AssertFileNotExists($".codex/agents/{role}.toml");
        }

        foreach (var role in new[] { "wayfinder", "grilling", "bro" })
        {
            var claude = ReadFile($".claude/skills/{role}/SKILL.md");
            var codex = ReadFile($".agents/skills/{role}/SKILL.md");
            Assert.Equal(claude, codex);
            Assert.DoesNotContain('\r', claude);
            Assert.DoesNotContain('\r', codex);
        }

        // Contrast: worker roles DO get a codex agent role file — sync is emitting codex artifacts,
        // it just never mints a spawnable agent for a skill-only role.
        AssertFileExists(".codex/agents/code-writer.toml");
        AssertFileExists(".codex/agents/reviewer.toml");
    }

    /// <summary>
    /// Issue 0271: every compiled codex worker role must parse for codex. The old emitter wrote
    /// <c>tools = "read, grep, ..."</c> — a bare string codex rejects ('invalid type: string ...
    /// expected struct ToolsToml'), so it silently ignored ALL six worker roles and the DR-024
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
            var role = Path.GetFileNameWithoutExtension(toml);
            Assert.DoesNotContain(content.Split('\n'), line => line.TrimStart().StartsWith("tools"));
            Assert.Contains($"name = \"{role}\"", content);
            Assert.Contains("model = \"", content);
            Assert.Contains("developer_instructions = \"\"\"", content);
        }
    }

    [Fact]
    public async Task Sync_OrchestratorSkill_StaysRuntimeNeutralAndPreservesDeliveryBoundary()
    {
        await InitProjectAsync("none");

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        var orchestrator = ReadFile(".agents/skills/orchestrator/SKILL.md");
        Assert.Contains("You coordinate; workers implement", orchestrator);
        Assert.Contains("Review independently", orchestrator);
        Assert.Contains("Integrate serially", orchestrator);
        Assert.Contains("Audit the whole", orchestrator);
        Assert.Contains("Keep Linear current", orchestrator);
        Assert.DoesNotContain("Codex", orchestrator);
        Assert.DoesNotContain("callback", orchestrator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("task or thread", orchestrator, StringComparison.OrdinalIgnoreCase);
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
            "run-sprint",
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

        var codeWriter = ReadFile(".agents/skills/code-writer/SKILL.md");
        Assert.Contains("Linear Issue", codeWriter);
        Assert.Contains("Verify the contract", codeWriter);
    }
}
