namespace DynaDocs.Tests.Rules;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Rules;

public sealed class FutureFeatureRuleTests : IDisposable
{
    private readonly string _basePath = Path.Combine(Path.GetTempPath(), "future-feature-rule-" + Guid.NewGuid().ToString("N"));
    private readonly FutureFeatureRule _rule = new();

    public FutureFeatureRuleTests()
    {
        Directory.CreateDirectory(_basePath);
    }

    public static TheoryData<string> PromotedLinearReferences => new()
    {
        "https://linear.app/example/issue/DYD-34",
        "https://linear.app/example/issue/DYD-34/close-validators",
        "https://linear.app/example/project/dydo-30-123456789abc",
        "https://linear.app/example/initiative/dydo-migration-123456789abc"
    };

    public static TheoryData<string> DeliveryFields => new()
    {
        "assigned", "assignee", "priority", "blocked-by", "blocks", "dependency", "dependencies",
        "project", "initiative", "cycle", "milestone", "sprint", "campaign", "slice", "task", "issue",
        "workflow", "state", "due-date", "estimate", "labels", "parent", "sub-issue", "team"
    };

    public void Dispose()
    {
        Directory.Delete(_basePath, recursive: true);
    }

    [Fact]
    public void Validate_AcceptsIdea()
    {
        var idea = CreateFutureFeature();
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        Assert.Empty(_rule.Validate(idea, [idea, related], _basePath));
    }

    [Theory]
    [MemberData(nameof(PromotedLinearReferences))]
    public void Validate_AcceptsPromotedFutureFeatureWithOneLinearReference(string linearReference)
    {
        var feature = CreateFutureFeature(status: "promoted", linearReference: linearReference);
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        Assert.Empty(_rule.Validate(feature, [feature, related], _basePath));
    }

    [Theory]
    [InlineData("idea", "https://linear.app/example/issue/DYD-34/close-validators")]
    [InlineData("promoted", null)]
    [InlineData("promoted", "https://linear.app/example/issue/not-an-issue")]
    public void Validate_RejectsMissingOrExtraLinearReference(string status, string? linearReference)
    {
        var feature = CreateFutureFeature(status: status, linearReference: linearReference);
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violation = Assert.Single(_rule.Validate(feature, [feature, related], _basePath));

        Assert.Contains("linear-reference", violation.Message);
    }

    [Theory]
    [InlineData("https://linear.app/bad workspace/project/dydo-123456789abc")]
    [InlineData("https://linear.app/example/project/bad slug-123456789abc")]
    [InlineData("https://linear.app/example/initiative/bad slug-123456789abc")]
    public void Validate_RejectsNonUrlSafeLinearReferenceSegments(string linearReference)
    {
        var feature = CreateFutureFeature(status: "promoted", linearReference: linearReference);
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violation = Assert.Single(_rule.Validate(feature, [feature, related], _basePath));

        Assert.Contains("linear-reference", violation.Message);
    }

    [Theory]
    [InlineData("https://linear.app/example/issue/DYD-34/close-validators?state=done")]
    [InlineData("https://linear.app/example/issue/DYD-34/close-validators#details")]
    [InlineData("https://linear.app/example/issue/DYD-34/close_validators")]
    [InlineData("https://linear.app/example/issue/DYD-٣٤")]
    public void Validate_RejectsInvalidIssueSlugSuffix(string linearReference)
    {
        var feature = CreateFutureFeature(status: "promoted", linearReference: linearReference);
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violation = Assert.Single(_rule.Validate(feature, [feature, related], _basePath));

        Assert.Contains("linear-reference", violation.Message);
    }

    [Fact]
    public void Validate_RejectsDuplicateLinearReference()
    {
        var feature = CreateFutureFeature(
            status: "promoted",
            linearReference: "https://linear.app/example/issue/DYD-34/close-validators",
            extraFrontmatter: "linear-reference: https://linear.app/example/issue/DYD-35/duplicate");
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violation = Assert.Single(_rule.Validate(feature, [feature, related], _basePath));

        Assert.Contains("linear-reference", violation.Message);
    }

    [Theory]
    [MemberData(nameof(DeliveryFields))]
    public void Validate_RejectsEveryDeliveryField(string field)
    {
        var feature = CreateFutureFeature(extraFrontmatter: $"{field}: value");
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violation = Assert.Single(_rule.Validate(feature, [feature, related], _basePath));

        Assert.Contains($"delivery field: {field}", violation.Message);
    }

    [Theory]
    [InlineData("area: guides\ntype: concept\nstatus: idea", "area: project")]
    [InlineData("area: project\ntype: guide\nstatus: idea", "type: concept")]
    [InlineData("area: project\ntype: concept\nstatus: backlog", "status: idea")]
    [InlineData("type: concept\nstatus: idea", "area: project")]
    [InlineData("area: project\nstatus: idea", "type: concept")]
    [InlineData("area: project\ntype: concept", "status: idea")]
    public void Validate_RejectsMissingOrInvalidRequiredFutureFeatureFields(string frontmatter, string expected)
    {
        var feature = CreateDoc("project/future-features/idea.md", $"---\n{frontmatter}\n---\n\n# Idea\n\n## Rationale\n\nReason.\n\n## Related\n\n[Decision](../decisions/one.md)\n");
        feature.Links = [CreateRelatedLink(feature.Content, "Decision", "../decisions/one.md")];
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        Assert.Contains(_rule.Validate(feature, [feature, related], _basePath), violation => violation.Message.Contains(expected));
    }

    [Fact]
    public void Validate_RejectsMissingRationale()
    {
        var feature = CreateFutureFeature(rationale: "");
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violation = Assert.Single(_rule.Validate(feature, [feature, related], _basePath));

        Assert.Contains("Rationale", violation.Message);
    }

    [Fact]
    public void Validate_RejectsAbsentRationaleHeading()
    {
        var feature = CreateDoc("project/future-features/idea.md", "---\narea: project\ntype: concept\nstatus: idea\n---\n\n# Idea\n\nReason without a rationale heading.\n\n## Related\n\n[Decision](../decisions/one.md)\n");
        feature.Links = [CreateRelatedLink(feature.Content, "Decision", "../decisions/one.md")];
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violation = Assert.Single(_rule.Validate(feature, [feature, related], _basePath));

        Assert.Contains("Rationale", violation.Message);
    }

    [Fact]
    public void Validate_RejectsMissingRelatedLink()
    {
        var feature = CreateFutureFeature(related: "No durable link.", links: []);

        var violation = Assert.Single(_rule.Validate(feature, [feature], _basePath));

        Assert.Contains("Related", violation.Message);
    }

    [Fact]
    public void Validate_RejectsAbsentRelatedHeading()
    {
        var feature = CreateDoc("project/future-features/idea.md", "---\narea: project\ntype: concept\nstatus: idea\n---\n\n# Idea\n\n## Rationale\n\nReason.\n\n[Decision](../decisions/one.md)\n");
        feature.Links = [CreateRelatedLink(feature.Content, "Decision", "../decisions/one.md")];
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violation = Assert.Single(_rule.Validate(feature, [feature, related], _basePath));

        Assert.Contains("Related", violation.Message);
    }

    [Theory]
    [InlineData("https://linear.app/example/issue/DYD-34/close-validators", LinkType.External)]
    [InlineData("https://example.com/knowledge", LinkType.External)]
    public void Validate_RejectsExternalRelatedLinks(string target, LinkType type)
    {
        var related = $"[External]({target})";
        var feature = CreateFutureFeature(related: related, links:
        [new LinkInfo(related, "External", target, null, type, 15)]);

        var violation = Assert.Single(_rule.Validate(feature, [feature], _basePath));

        Assert.Contains("resolving repository link", violation.Message);
    }

    [Fact]
    public void Validate_RejectsNonResolvingRelatedLink()
    {
        var feature = CreateFutureFeature(related: "[Missing](../decisions/missing.md)");

        var violation = Assert.Single(_rule.Validate(feature, [feature], _basePath));

        Assert.Contains("resolving", violation.Message);
    }

    [Fact]
    public void Validate_AcceptsNestedRationaleAndRelatedSections()
    {
        var feature = CreateFutureFeature(
            rationale: "### Detail\n\nThis is worth retaining.",
            related: "### Durable source\n\n[Decision](../decisions/one.md)",
            links: []);
        feature.Links = [CreateRelatedLink(feature.Content, "Decision", "../decisions/one.md")];
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        Assert.Empty(_rule.Validate(feature, [feature, related], _basePath));
    }

    [Fact]
    public void Validate_IgnoresFencedPseudoSectionsAndLinks()
    {
        var feature = CreateDoc("project/future-features/idea.md", "---\narea: project\ntype: concept\nstatus: idea\n---\n\n# Idea\n\n```md\n## Rationale\n\nThis is only an example.\n\n## Related\n\n[Decision](../decisions/one.md)\n```\n");
        feature.Links = [CreateRelatedLink(feature.Content, "Decision", "../decisions/one.md")];
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violations = _rule.Validate(feature, [feature, related], _basePath).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("Rationale"));
        Assert.Contains(violations, violation => violation.Message.Contains("Related"));
    }

    [Fact]
    public void Validate_IgnoresIndentedPseudoSections()
    {
        var feature = CreateDoc("project/future-features/idea.md", "---\narea: project\ntype: concept\nstatus: idea\n---\n\n# Idea\n\n    ## Rationale\n\n    Example content.\n\n    ## Related\n\n    [Decision](../decisions/one.md)\n");
        feature.Links = [CreateRelatedLink(feature.Content, "Decision", "../decisions/one.md")];
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violations = _rule.Validate(feature, [feature, related], _basePath).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("Rationale"));
        Assert.Contains(violations, violation => violation.Message.Contains("Related"));
    }

    [Fact]
    public void Validate_IgnoresPseudoSectionsAfterInvalidFenceCloser()
    {
        var feature = CreateDoc("project/future-features/idea.md", "---\narea: project\ntype: concept\nstatus: idea\n---\n\n# Idea\n\n```md\n## Rationale\n\nExample content.\n```not-a-close\n\n## Related\n\n[Decision](../decisions/one.md)\n```\n");
        feature.Links = [CreateRelatedLink(feature.Content, "Decision", "../decisions/one.md")];
        var related = CreateDoc("project/decisions/one.md", "# Decision");

        var violations = _rule.Validate(feature, [feature, related], _basePath).ToList();

        Assert.Contains(violations, violation => violation.Message.Contains("Rationale"));
        Assert.Contains(violations, violation => violation.Message.Contains("Related"));
    }

    [Theory]
    [InlineData("project/future-features/_future-features.md")]
    [InlineData("project/future-features/nested/idea.md")]
    [InlineData("project/ideas/idea.md")]
    public void Validate_SkipsNonDirectOrMetaDocuments(string path)
    {
        var doc = CreateDoc(path, "# Not a FutureFeature");

        Assert.Empty(_rule.Validate(doc, [doc], _basePath));
    }

    [Fact]
    public void CheckDocValidator_RegistersFutureFeatureRule()
    {
        var folder = Path.Combine(_basePath, "project", "future-features");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "idea.md"), "---\narea: project\ntype: concept\nstatus: idea\n---\n\n# Idea\n\n## Related\n\n[Decision](../decisions/one.md)\n");

        var result = CheckDocValidator.Validate(_basePath);

        Assert.Contains(result.Violations, violation => violation.RuleName == "FutureFeature" && violation.FilePath == "project/future-features/idea.md");
    }

    private DocFile CreateFutureFeature(
        string status = "idea",
        string? linearReference = null,
        string? extraFrontmatter = null,
        string rationale = "This is worth retaining.",
        string related = "[Decision](../decisions/one.md)",
        List<LinkInfo>? links = null)
    {
        var frontmatter = $"area: project\ntype: concept\nstatus: {status}";
        if (linearReference != null)
            frontmatter += $"\nlinear-reference: {linearReference}";
        if (extraFrontmatter != null)
            frontmatter += $"\n{extraFrontmatter}";

        var content = $"---\n{frontmatter}\n---\n\n# Idea\n\n## Rationale\n\n{rationale}\n\n## Related\n\n{related}\n";
        var generatedLinks = links;
        if (generatedLinks == null && related == "[Decision](../decisions/one.md)")
            generatedLinks = [CreateRelatedLink(content, "Decision", "../decisions/one.md")];
        if (generatedLinks == null && related == "[Missing](../decisions/missing.md)")
            generatedLinks = [CreateRelatedLink(content, "Missing", "../decisions/missing.md")];

        return CreateDoc("project/future-features/idea.md", content, generatedLinks);
    }

    private static LinkInfo CreateRelatedLink(string content, string label, string target)
    {
        var lineNumber = Array.FindIndex(content.Split('\n'), line => line.Contains($"[{label}]({target})", StringComparison.Ordinal)) + 1;
        return new LinkInfo($"[{label}]({target})", label, target, null, LinkType.Markdown, lineNumber);
    }

    private DocFile CreateDoc(string relativePath, string content, List<LinkInfo>? links = null)
    {
        return new DocFile
        {
            FilePath = Path.Combine(_basePath, relativePath).Replace('\\', '/'),
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = content,
            Links = links ?? []
        };
    }
}
