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
}
