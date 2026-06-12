namespace DynaDocs.Tests.Commands;

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
        _reviewer = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "reviewer");
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
        Assert.Contains("Review checklist", skill);

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
        var codeWriter = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "code-writer");
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
        var docsWriter = RoleDefinitionService.GetBaseRoleDefinitions().First(r => r.Name == "docs-writer");
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
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
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
}
