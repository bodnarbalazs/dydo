namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class FolderScaffolderTests : IDisposable
{
    private readonly string _testDir;
    private readonly FolderScaffolder _scaffolder;

    public FolderScaffolderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-scaffold-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _scaffolder = new FolderScaffolder();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Scaffold_CreatesExpectedFolderStructure()
    {
        _scaffolder.Scaffold(_testDir);

        Assert.True(Directory.Exists(Path.Combine(_testDir, "understand")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "guides")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "reference")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "tasks")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "decisions")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "changelog")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "agents")));
    }

    [Fact]
    public void Scaffold_CreatesRootIndexMd()
    {
        _scaffolder.Scaffold(_testDir);

        var indexPath = Path.Combine(_testDir, "index.md");
        Assert.True(File.Exists(indexPath));

        var content = File.ReadAllText(indexPath);
        Assert.Contains("DynaDocs", content);
        Assert.Contains("agents/", content);  // Links to agents folder
    }

    [Fact]
    public void Scaffold_CreatesHubIndexFiles()
    {
        _scaffolder.Scaffold(_testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, "understand", "_index.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "guides", "_index.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "reference", "_index.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "project", "_index.md")));
    }

    [Fact]
    public void Scaffold_CreatesFoundationDocs()
    {
        _scaffolder.Scaffold(_testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, "welcome.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "understand", "about.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "understand", "architecture.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "guides", "coding-standards.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "guides", "how-to-use-docs.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "files-off-limits.md")));
    }

    [Fact]
    public void Scaffold_CreatesAgentWorkspaces()
    {
        var agentNames = new List<string> { "Adele", "Brian" };
        _scaffolder.Scaffold(_testDir, agentNames);

        // Check agent workspaces exist
        foreach (var name in agentNames)
        {
            var agentPath = Path.Combine(_testDir, "agents", name);
            Assert.True(Directory.Exists(agentPath), $"Agent workspace for {name} should exist");
            Assert.True(Directory.Exists(Path.Combine(agentPath, "modes")), $"Modes folder for {name} should exist");
            Assert.True(Directory.Exists(Path.Combine(agentPath, "inbox")), $"Inbox folder for {name} should exist");
            Assert.True(File.Exists(Path.Combine(agentPath, "workflow.md")), $"workflow.md for {name} should exist");
        }
    }

    [Fact]
    public void ScaffoldAgentWorkspace_CreatesWorkflowFile()
    {
        var agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(agentsPath);

        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "TestAgent");

        var workflowPath = Path.Combine(agentsPath, "TestAgent", "workflow.md");
        Assert.True(File.Exists(workflowPath));

        var content = File.ReadAllText(workflowPath);
        Assert.Contains("TestAgent", content);
        Assert.Contains("dydo agent claim TestAgent", content);
    }

    [Fact]
    public void ScaffoldAgentWorkspace_CreatesModeFiles()
    {
        var agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(agentsPath);

        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "TestAgent");

        var modesPath = Path.Combine(agentsPath, "TestAgent", "modes");
        var expectedModes = new[] { "code-writer", "reviewer", "interviewer", "planner", "docs-writer" };

        foreach (var mode in expectedModes)
        {
            var modePath = Path.Combine(modesPath, $"{mode}.md");
            Assert.True(File.Exists(modePath), $"Mode file {mode}.md should exist");

            var content = File.ReadAllText(modePath);
            Assert.Contains("TestAgent", content);  // Agent name should be baked in
        }
    }

    [Fact]
    public void ScaffoldAgentWorkspace_CreatesInboxFolder()
    {
        var agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(agentsPath);

        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "TestAgent");

        Assert.True(Directory.Exists(Path.Combine(agentsPath, "TestAgent", "inbox")));
    }

    [Fact]
    public void ScaffoldAgentWorkspace_DoesNotOverwriteExistingFiles()
    {
        var agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(agentsPath);

        // Scaffold once
        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "TestAgent");

        // Modify workflow file
        var workflowPath = Path.Combine(agentsPath, "TestAgent", "workflow.md");
        File.WriteAllText(workflowPath, "Custom content");

        // Scaffold again
        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "TestAgent");

        // Should not overwrite
        var content = File.ReadAllText(workflowPath);
        Assert.Equal("Custom content", content);
    }

    [Fact]
    public void RegenerateAgentFiles_OverwritesExistingFiles()
    {
        var agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(agentsPath);

        // Scaffold once
        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "OldName");

        // Rename folder (simulating agent rename)
        Directory.Move(Path.Combine(agentsPath, "OldName"), Path.Combine(agentsPath, "NewName"));

        // Regenerate with new name
        _scaffolder.RegenerateAgentFiles(agentsPath, "NewName");

        // Workflow should have new name
        var workflowPath = Path.Combine(agentsPath, "NewName", "workflow.md");
        var content = File.ReadAllText(workflowPath);
        Assert.Contains("NewName", content);
        Assert.DoesNotContain("OldName", content);
    }

    [Fact]
    public void RegenerateAgentFiles_CreatesModeFilesWithNewName()
    {
        var agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(agentsPath);

        // Scaffold with old name
        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "OldAgent");

        // Rename folder
        Directory.Move(Path.Combine(agentsPath, "OldAgent"), Path.Combine(agentsPath, "RenamedAgent"));

        // Regenerate
        _scaffolder.RegenerateAgentFiles(agentsPath, "RenamedAgent");

        // Check mode files have new name
        var codeWriterPath = Path.Combine(agentsPath, "RenamedAgent", "modes", "code-writer.md");
        var content = File.ReadAllText(codeWriterPath);
        Assert.Contains("RenamedAgent", content);
    }

    [Fact]
    public void Scaffold_AboutMd_ContainsProjectPlaceholders()
    {
        _scaffolder.Scaffold(_testDir);

        var aboutPath = Path.Combine(_testDir, "understand", "about.md");
        var content = File.ReadAllText(aboutPath);

        Assert.Contains("What We're Building", content);
        Assert.Contains("Tech Stack", content);
        Assert.Contains("Key Concepts", content);
    }

    [Fact]
    public void ScaffoldAgentWorkspace_ModeFiles_HaveCorrectFrontmatter()
    {
        var agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(agentsPath);

        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "TestAgent");

        var codeWriterPath = Path.Combine(agentsPath, "TestAgent", "modes", "code-writer.md");
        var content = File.ReadAllText(codeWriterPath);

        Assert.StartsWith("---", content);
        Assert.Contains("agent: TestAgent", content);
        Assert.Contains("mode: code-writer", content);
    }

    [Fact]
    public void ScaffoldAgentWorkspace_WorkflowFile_LinksToModes()
    {
        var agentsPath = Path.Combine(_testDir, "agents");
        Directory.CreateDirectory(agentsPath);

        _scaffolder.ScaffoldAgentWorkspace(agentsPath, "TestAgent");

        var workflowPath = Path.Combine(agentsPath, "TestAgent", "workflow.md");
        var content = File.ReadAllText(workflowPath);

        Assert.Contains("modes/interviewer.md", content);
        Assert.Contains("modes/planner.md", content);
        Assert.Contains("modes/code-writer.md", content);
        Assert.Contains("modes/reviewer.md", content);
        Assert.Contains("modes/docs-writer.md", content);
    }
}
