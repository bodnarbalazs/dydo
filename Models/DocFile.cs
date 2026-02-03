namespace DynaDocs.Models;

public class DocFile
{
    public required string FilePath { get; init; }
    public required string RelativePath { get; init; }
    public required string FileName { get; init; }
    public required string Content { get; init; }
    public Frontmatter? Frontmatter { get; set; }
    public string? Title { get; set; }
    public string? SummaryParagraph { get; set; }
    public List<LinkInfo> Links { get; set; } = [];
    public List<string> Anchors { get; set; } = [];
    public bool HasFrontmatter { get; set; }
    public bool IsHubFile => FileName.Equals("_index.md", StringComparison.OrdinalIgnoreCase);
    public bool IsIndexFile => FileName.Equals("index.md", StringComparison.OrdinalIgnoreCase);
}
