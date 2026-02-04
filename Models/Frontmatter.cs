namespace DynaDocs.Models;

public class Frontmatter
{
    public string? Area { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public string? Date { get; set; }

    public static readonly string[] ValidAreas = ["frontend", "backend", "microservices", "platform", "general", "understand", "guides", "reference", "project"];
    public static readonly string[] ValidTypes = ["hub", "concept", "guide", "reference", "decision", "pitfall", "changelog", "context", "folder-meta"];
    public static readonly string[] ValidStatuses = ["proposed", "accepted", "deprecated", "superseded"];
}
