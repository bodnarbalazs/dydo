namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class ResolveIncludesTests : IDisposable
{
    private readonly string _tempDir;

    public ResolveIncludesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    private string CreateAdditionsFolder()
    {
        var additionsPath = Path.Combine(_tempDir, "_system", "template-additions");
        Directory.CreateDirectory(additionsPath);
        return additionsPath;
    }

    private void WriteAddition(string additionsPath, string name, string content)
    {
        File.WriteAllText(Path.Combine(additionsPath, $"{name}.md"), content);
    }

    #region Core resolution

    [Fact]
    public void ResolveIncludes_ResolvesExistingFile()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "extra-verify", "5. Run gap_check.py");

        var result = TemplateGenerator.ResolveIncludes(
            "Step 4\n{{include:extra-verify}}\nStep 6", _tempDir);

        Assert.Contains("5. Run gap_check.py", result);
        Assert.DoesNotContain("{{include:extra-verify}}", result);
    }

    [Fact]
    public void ResolveIncludes_MissingFile_ResolvesToEmpty()
    {
        CreateAdditionsFolder();

        var result = TemplateGenerator.ResolveIncludes(
            "Step 4\n{{include:nonexistent}}\nStep 6", _tempDir);

        Assert.DoesNotContain("{{include:nonexistent}}", result);
        Assert.Contains("Step 4", result);
        Assert.Contains("Step 6", result);
    }

    [Fact]
    public void ResolveIncludes_NoAdditionsFolder_ResolvesToEmpty()
    {
        var result = TemplateGenerator.ResolveIncludes(
            "Step 4\n{{include:extra-verify}}\nStep 6", _tempDir);

        Assert.DoesNotContain("{{include:extra-verify}}", result);
    }

    [Fact]
    public void ResolveIncludes_MultipleTagsSameFile_AllResolved()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "shared-step", "Run shared check");

        var result = TemplateGenerator.ResolveIncludes(
            "{{include:shared-step}}\nMiddle\n{{include:shared-step}}", _tempDir);

        var count = result.Split("Run shared check").Length - 1;
        Assert.Equal(2, count);
    }

    [Fact]
    public void ResolveIncludes_MultipleDifferentTags_EachResolved()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "hook-a", "Content A");
        WriteAddition(additionsPath, "hook-b", "Content B");

        var result = TemplateGenerator.ResolveIncludes(
            "{{include:hook-a}}\n---\n{{include:hook-b}}", _tempDir);

        Assert.Contains("Content A", result);
        Assert.Contains("Content B", result);
    }

    [Fact]
    public void ResolveIncludes_EmptyFile_ResolvesToEmpty()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "empty-hook", "");

        var result = TemplateGenerator.ResolveIncludes(
            "Before\n{{include:empty-hook}}\nAfter", _tempDir);

        Assert.DoesNotContain("{{include:empty-hook}}", result);
    }

    [Fact]
    public void ResolveIncludes_InvalidTagChars_LeftAsIs()
    {
        CreateAdditionsFolder();

        var input = "{{include:no spaces!}}";
        var result = TemplateGenerator.ResolveIncludes(input, _tempDir);

        Assert.Contains("{{include:no spaces!}}", result);
    }

    [Fact]
    public void ResolveIncludes_TrimsTrailingNewlines()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "trimmed", "Content here\n\n\n");

        var result = TemplateGenerator.ResolveIncludes(
            "Before\n{{include:trimmed}}\nAfter", _tempDir);

        Assert.Contains("Content here", result);
        // Should not have 3+ consecutive newlines
        Assert.DoesNotContain("\n\n\n", result);
    }

    [Fact]
    public void ResolveIncludes_ExcessiveBlankLinesCollapsed()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "spacey", "");

        var input = "Line 1\n\n\n\n\nLine 2";
        var result = TemplateGenerator.ResolveIncludes(input, _tempDir);

        Assert.DoesNotContain("\n\n\n", result);
    }

    #endregion

    #region Tag name formats

    [Fact]
    public void ResolveIncludes_SupportsHyphens()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "my-custom-step", "Custom step content");

        var result = TemplateGenerator.ResolveIncludes(
            "{{include:my-custom-step}}", _tempDir);

        Assert.Contains("Custom step content", result);
    }

    [Fact]
    public void ResolveIncludes_SupportsUnderscores()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "my_custom_step", "Underscore content");

        var result = TemplateGenerator.ResolveIncludes(
            "{{include:my_custom_step}}", _tempDir);

        Assert.Contains("Underscore content", result);
    }

    [Fact]
    public void ResolveIncludes_SupportsNumbers()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "step2-verify", "Step 2 verify");

        var result = TemplateGenerator.ResolveIncludes(
            "{{include:step2-verify}}", _tempDir);

        Assert.Contains("Step 2 verify", result);
    }

    [Fact]
    public void ResolveIncludes_CaseSensitive()
    {
        var additionsPath = CreateAdditionsFolder();
        WriteAddition(additionsPath, "Extra-Verify", "Case sensitive content");

        var result = TemplateGenerator.ResolveIncludes(
            "{{include:Extra-Verify}}", _tempDir);

        Assert.Contains("Case sensitive content", result);
    }

    #endregion

    #region Example file behavior

    [Fact]
    public void ResolveIncludes_ExampleFile_NotResolved()
    {
        var additionsPath = CreateAdditionsFolder();
        // Write an .example file — should NOT be resolved
        File.WriteAllText(Path.Combine(additionsPath, "extra-verify.md.example"),
            "This should not appear");

        var result = TemplateGenerator.ResolveIncludes(
            "Before\n{{include:extra-verify}}\nAfter", _tempDir);

        Assert.DoesNotContain("This should not appear", result);
        Assert.DoesNotContain("{{include:extra-verify}}", result);
    }

    [Fact]
    public void ResolveIncludes_ExampleRenamed_ThenResolved()
    {
        var additionsPath = CreateAdditionsFolder();
        // Write an active .md file (simulating rename from .example)
        WriteAddition(additionsPath, "extra-verify", "Active content");

        var result = TemplateGenerator.ResolveIncludes(
            "Before\n{{include:extra-verify}}\nAfter", _tempDir);

        Assert.Contains("Active content", result);
    }

    #endregion

    #region Integration with generation

    [Fact]
    public void GenerateWorkflowFile_WithAddition_IncludesContent()
    {
        var additionsPath = CreateAdditionsFolder();
        var templatesPath = Path.Combine(_tempDir, "_system", "templates");
        Directory.CreateDirectory(templatesPath);
        var templateContent = TemplateGenerator.ReadBuiltInTemplate("agent-workflow.template.md");
        // Add an include tag to the workflow template (it doesn't ship with one)
        templateContent += "\n{{include:extra-workflow}}\n";
        File.WriteAllText(Path.Combine(templatesPath, "agent-workflow.template.md"), templateContent);

        WriteAddition(additionsPath, "extra-workflow", "Custom workflow addition");

        var result = TemplateGenerator.GenerateWorkflowFile("TestAgent", _tempDir);

        Assert.Contains("Custom workflow addition", result);
        Assert.DoesNotContain("{{include:", result);
    }

    [Fact]
    public void GenerateModeFile_WithAddition_IncludesContent()
    {
        var additionsPath = CreateAdditionsFolder();
        // Copy templates to _system/templates/ so ReadTemplate can find them
        var templatesPath = Path.Combine(_tempDir, "_system", "templates");
        Directory.CreateDirectory(templatesPath);
        var templateContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");
        File.WriteAllText(Path.Combine(templatesPath, "mode-code-writer.template.md"), templateContent);

        WriteAddition(additionsPath, "extra-verify", "5. Run project linter");

        var result = TemplateGenerator.GenerateModeFile("TestAgent", "code-writer", _tempDir);

        Assert.Contains("5. Run project linter", result);
    }

    [Fact]
    public void GenerateModeFile_WithoutAddition_NoLeftoverTags()
    {
        var templatesPath = Path.Combine(_tempDir, "_system", "templates");
        Directory.CreateDirectory(templatesPath);
        var templateContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");
        File.WriteAllText(Path.Combine(templatesPath, "mode-code-writer.template.md"), templateContent);

        var result = TemplateGenerator.GenerateModeFile("TestAgent", "code-writer", _tempDir);

        Assert.DoesNotContain("{{include:", result);
    }

    [Fact]
    public void GenerateModeFile_AdditionAndPlaceholders_BothResolved()
    {
        var additionsPath = CreateAdditionsFolder();
        var templatesPath = Path.Combine(_tempDir, "_system", "templates");
        Directory.CreateDirectory(templatesPath);
        var templateContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");
        File.WriteAllText(Path.Combine(templatesPath, "mode-code-writer.template.md"), templateContent);

        WriteAddition(additionsPath, "extra-must-reads", "5. [custom.md](custom.md) — Custom doc");

        var result = TemplateGenerator.GenerateModeFile("TestAgent", "code-writer", _tempDir);

        Assert.Contains("TestAgent", result); // Placeholder resolved
        Assert.Contains("5. [custom.md](custom.md) — Custom doc", result); // Include resolved
        Assert.DoesNotContain("{{AGENT_NAME}}", result);
        Assert.DoesNotContain("{{include:", result);
    }

    #endregion
}
