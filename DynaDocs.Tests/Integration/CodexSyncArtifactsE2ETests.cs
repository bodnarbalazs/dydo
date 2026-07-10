namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// c1-7 / issue 0233 (ask 4): `dydo sync` emits skill-only artifacts for the Tier-1 modes
/// (planner + the three manager modes) without ever minting a codex AGENT role file for them.
/// Existing sync tests pin the ABSENCE of a <c>.claude/agents/&lt;role&gt;.md</c>; this one closes the
/// codex-side gap the issue named — a spawnable <c>.codex/agents/&lt;role&gt;.toml</c> for a Tier-1
/// terminal identity would be artifact drift. Driven through the real sync command on an
/// initialized project.
/// </summary>
[Collection("Integration")]
public class CodexSyncArtifactsE2ETests : IntegrationTestBase
{
    [Fact]
    public async Task Sync_Tier1Modes_EmitSkillOnly_NoCodexAgentRoleFiles()
    {
        await InitProjectAsync("none", "testuser", 3);

        var sync = await RunAsync(SyncCommand.Create());
        sync.AssertSuccess();

        foreach (var role in new[] { "planner", "orchestrator", "co-thinker", "chief-of-staff" })
        {
            // Skill on both surfaces — the methodology the Tier-1 agent applies in its own thread.
            AssertFileExists($".claude/skills/{role}/SKILL.md");
            AssertFileExists($".agents/skills/{role}/SKILL.md");
            // But NO spawnable agent definition on either host.
            AssertFileNotExists($".claude/agents/{role}.md");
            AssertFileNotExists($".codex/agents/{role}.toml");
        }

        // Contrast: worker roles DO get a codex agent role file — sync is emitting codex artifacts,
        // it just never mints a spawnable agent for a Tier-1 terminal identity.
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
        await InitProjectAsync("none", "testuser", 3);

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
}
