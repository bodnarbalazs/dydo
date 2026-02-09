namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for project-local template overrides in _system/templates/.
/// </summary>
[Collection("Integration")]
public class TemplateOverrideTests : IntegrationTestBase
{
    [Fact]
    public async Task Init_CopiesTemplatesToSystemFolder()
    {
        await InitProjectAsync();

        AssertDirectoryExists("dydo/_system/templates");
        AssertFileExists("dydo/_system/templates/agent-workflow.template.md");
        AssertFileExists("dydo/_system/templates/mode-code-writer.template.md");
        AssertFileExists("dydo/_system/templates/mode-reviewer.template.md");
        AssertFileExists("dydo/_system/templates/mode-co-thinker.template.md");
        AssertFileExists("dydo/_system/templates/mode-interviewer.template.md");
        AssertFileExists("dydo/_system/templates/mode-planner.template.md");
        AssertFileExists("dydo/_system/templates/mode-docs-writer.template.md");
        AssertFileExists("dydo/_system/templates/mode-tester.template.md");
    }

    [Fact]
    public async Task Init_SystemTemplatesMatchBuiltIn()
    {
        await InitProjectAsync();

        // Verify the copied template matches the built-in template
        var copiedContent = ReadFile("dydo/_system/templates/agent-workflow.template.md");
        var builtInContent = TemplateGenerator.ReadBuiltInTemplate("agent-workflow.template.md");

        Assert.Equal(builtInContent, copiedContent);
    }

    [Fact]
    public async Task AgentWorkspace_UsesProjectLocalTemplate()
    {
        await InitProjectAsync("none", "testuser", 3);
        await JoinProjectAsync("none", "alice", 0);

        // Modify project-local template - add a unique marker
        var templatePath = Path.Combine(TestDir, "dydo/_system/templates/agent-workflow.template.md");
        var content = File.ReadAllText(templatePath);
        content = "<!-- CUSTOM_TEMPLATE_MARKER -->\n" + content;
        File.WriteAllText(templatePath, content);

        // Create new agent
        var result = await AgentNewAsync("Zara", "alice");
        result.AssertSuccess();

        // Verify custom template was used
        var workflowContent = ReadFile("dydo/agents/Zara/workflow.md");
        Assert.Contains("CUSTOM_TEMPLATE_MARKER", workflowContent);
    }

    [Fact]
    public async Task ModeFiles_UseProjectLocalTemplate()
    {
        await InitProjectAsync("none", "testuser", 3);
        await JoinProjectAsync("none", "alice", 0);

        // Modify project-local mode template
        var templatePath = Path.Combine(TestDir, "dydo/_system/templates/mode-code-writer.template.md");
        var content = File.ReadAllText(templatePath);
        content = "<!-- CUSTOM_MODE_MARKER -->\n" + content;
        File.WriteAllText(templatePath, content);

        // Create new agent and claim it (mode files are created at claim)
        var result = await AgentNewAsync("Zara", "alice");
        result.AssertSuccess();
        SetHuman("alice");
        var claimResult = await ClaimAgentAsync("Zara");
        claimResult.AssertSuccess();

        // Verify custom template was used for mode file
        var modeContent = ReadFile("dydo/agents/Zara/modes/code-writer.md");
        Assert.Contains("CUSTOM_MODE_MARKER", modeContent);
    }

    [Fact]
    public async Task PartialOverride_FallsBackToBuiltIn()
    {
        await InitProjectAsync("none", "testuser", 3);
        await JoinProjectAsync("none", "alice", 0);

        // Modify only the code-writer template, leave reviewer as-is
        var codeWriterTemplatePath = Path.Combine(TestDir, "dydo/_system/templates/mode-code-writer.template.md");
        var content = File.ReadAllText(codeWriterTemplatePath);
        content = "<!-- ONLY_CODE_WRITER_CUSTOM -->\n" + content;
        File.WriteAllText(codeWriterTemplatePath, content);

        // Delete the reviewer template - should fall back to built-in
        var reviewerTemplatePath = Path.Combine(TestDir, "dydo/_system/templates/mode-reviewer.template.md");
        File.Delete(reviewerTemplatePath);

        // Create new agent and claim it (mode files are created at claim)
        var result = await AgentNewAsync("Zara", "alice");
        result.AssertSuccess();
        SetHuman("alice");
        var claimResult = await ClaimAgentAsync("Zara");
        claimResult.AssertSuccess();

        // Verify code-writer used custom template
        var codeWriterContent = ReadFile("dydo/agents/Zara/modes/code-writer.md");
        Assert.Contains("ONLY_CODE_WRITER_CUSTOM", codeWriterContent);

        // Verify reviewer file was still created (using built-in fallback)
        AssertFileExists("dydo/agents/Zara/modes/reviewer.md");
        var reviewerContent = ReadFile("dydo/agents/Zara/modes/reviewer.md");
        Assert.DoesNotContain("ONLY_CODE_WRITER_CUSTOM", reviewerContent);
        Assert.Contains("Zara", reviewerContent); // Agent name should still be substituted
    }

    [Fact]
    public async Task Join_DoesNotOverwriteExistingTemplates()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Modify a template
        var templatePath = Path.Combine(TestDir, "dydo/_system/templates/agent-workflow.template.md");
        var customContent = "<!-- CUSTOM_CONTENT_PRESERVED -->\nCustom template";
        File.WriteAllText(templatePath, customContent);

        // Join as another user
        await JoinProjectAsync("none", "alice", 0);

        // Verify custom template was NOT overwritten
        var contentAfterJoin = File.ReadAllText(templatePath);
        Assert.Contains("CUSTOM_CONTENT_PRESERVED", contentAfterJoin);
    }

    [Fact]
    public void GetAllTemplateNames_ReturnsExpectedTemplates()
    {
        var templateNames = TemplateGenerator.GetAllTemplateNames();

        Assert.Contains("agent-workflow.template.md", templateNames);
        Assert.Contains("mode-code-writer.template.md", templateNames);
        Assert.Contains("mode-reviewer.template.md", templateNames);

        // Should have all expected templates (8 total: 1 workflow + 7 modes)
        Assert.Equal(8, templateNames.Count);
    }

    [Fact]
    public void ReadBuiltInTemplate_ReturnsTemplateContent()
    {
        var content = TemplateGenerator.ReadBuiltInTemplate("agent-workflow.template.md");

        Assert.NotEmpty(content);
        Assert.Contains("{{AGENT_NAME}}", content);
    }

    [Fact]
    public void ReadBuiltInTemplate_ThrowsForMissingTemplate()
    {
        Assert.Throws<FileNotFoundException>(() =>
            TemplateGenerator.ReadBuiltInTemplate("nonexistent.template.md"));
    }

    #region Helper Methods

    private async Task<CommandResult> AgentNewAsync(string name, string human)
    {
        var command = AgentCommand.Create();
        return await RunAsync(command, "new", name, human);
    }

    #endregion
}
