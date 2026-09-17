namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;

public class RuleBaseTests
{
    [Fact]
    public void DefaultHooksValidateDocumentsAndCreateFolderErrorsWithRuleIdentity()
    {
        var rule = new ProbeRule();
        var doc = new DocFile
        {
            FilePath = "project/guide.md",
            RelativePath = "project/guide.md",
            FileName = "guide.md",
            Content = "# Guide"
        };

        Assert.False(rule.Skips(doc));
        Assert.Equal(
            new Violation("project", "Probe", "missing folder", ViolationSeverity.Error),
            rule.FolderError("project", "missing folder"));
    }

    private sealed class ProbeRule : RuleBase
    {
        public override string Name => "Probe";
        public override string Description => "Exercises the base rule contract";
        public bool Skips(DocFile doc) => ShouldSkip(doc);
        public Violation FolderError(string path, string message) => CreateFolderError(path, message);
    }
}
