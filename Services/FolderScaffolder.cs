namespace DynaDocs.Services;

public class FolderScaffolder : IFolderScaffolder
{
    private readonly record struct FolderSpec(string Path, string Description, string Area);

    private static readonly FolderSpec[] Folders =
    [
        new("understand", "Core concepts, domain knowledge, and architecture", "general"),
        new("guides", "Task-oriented development guides", "general"),
        new("reference", "API specs, configuration, and tool documentation", "general"),
        new("project", "Decisions, pitfalls, changelog, and meta documentation", "general")
    ];

    public void Scaffold(string basePath)
    {
        foreach (var folder in Folders)
        {
            var folderPath = Path.Combine(basePath, folder.Path);
            Directory.CreateDirectory(folderPath);

            var indexPath = Path.Combine(folderPath, "_index.md");
            if (!File.Exists(indexPath))
            {
                var content = GenerateHubContent(folder);
                File.WriteAllText(indexPath, content);
            }
        }

        var rootIndexPath = Path.Combine(basePath, "index.md");
        if (!File.Exists(rootIndexPath))
        {
            var indexGenerator = new IndexGenerator();
            File.WriteAllText(rootIndexPath, indexGenerator.Generate([], basePath));
        }
    }

    private static string GenerateHubContent(FolderSpec folder)
    {
        var title = char.ToUpper(folder.Path[0]) + folder.Path[1..];
        return $"""
            ---
            area: {folder.Area}
            type: hub
            ---

            # {title}

            {folder.Description}

            ## Contents

            *Add links to documents in this section.*
            """;
    }
}
