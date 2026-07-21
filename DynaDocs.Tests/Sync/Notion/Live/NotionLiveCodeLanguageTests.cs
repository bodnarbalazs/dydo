namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Notion;

/// <summary>
/// LIVE (ns-9): every fence tag the converter's <c>NormalizeLanguage</c> maps is accepted by Notion's code-block
/// endpoint. Notion rejects a language outside its fixed vocabulary with a 400 (the ubiquitous <c>csharp</c> →
/// must be <c>c#</c>), which the fake — treating the language as an opaque string — cannot catch. This appends one
/// code block per alias in a single page: any tag whose normalized output Notion rejects fails the whole append,
/// so a green run proves the alias table's outputs are all in the live vocabulary.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveCodeLanguageTests : NotionLiveTestBase
{
    // The converter's alias INPUTS (each normalized to a Notion spelling) plus a sample of the canonical outputs —
    // every one must be accepted by the live code-block endpoint.
    private static readonly string[] FenceTags =
    [
        "csharp", "cs", "cpp", "fsharp", "py", "js", "node", "ts", "yml", "sh", "zsh", "console", "shell-session",
        "pwsh", "ps1", "md", "text", "plaintext", "plain", "txt", "dockerfile", "objc", "vb", "asm", "tex",
        "golang", "rs", "kt", "rb",
        "c#", "c++", "f#", "python", "javascript", "typescript", "yaml", "shell", "powershell", "markdown",
        "plain text", "docker", "objective-c", "visual basic", "assembly", "latex", "go", "rust", "kotlin", "ruby",
        "bash", "json", "html", "css", "sql", "java", "xml", "toml",
    ];

    [NotionLiveFact]
    public void EveryLanguageAlias_AcceptedByCodeBlockEndpoint()
    {
        var markdown = string.Join("\n\n", FenceTags.Select(tag => $"```{tag}\nsample\n```"));
        var blocks = NotionBlockConverter.ToBlocks(markdown);
        Assert.Equal(FenceTags.Length, blocks.Count);
        Assert.All(blocks, b => Assert.Equal("code", b.Type));

        // A rejected language 400s the append; reaching the assertion means every normalized tag was accepted.
        NotionBlockAppender.AppendForest(Client, ChildPageId, blocks);

        Assert.Equal(FenceTags.Length, Client.GetBlockChildren(ChildPageId).Count);
    }
}
