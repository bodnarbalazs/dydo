namespace DynaDocs.Services;

using DynaDocs.Models;

public class FolderScaffolder : IFolderScaffolder
{
    private readonly record struct FolderSpec(string Path, string Description, string Area);

    private static readonly FolderSpec[] Folders =
    [
        new("understand", "Core concepts, domain knowledge, and architecture", "understand"),
        new("guides", "Task-oriented development guides", "guides"),
        new("reference", "API specs, configuration, and tool documentation", "reference"),
        new("project", "Decisions, pitfalls, changelog, and meta documentation", "project"),
        new("project/tasks", "Task tracking and dispatch", "project"),
        new("project/decisions", "Architecture decision records", "project"),
        new("project/changelog", "Change history", "project"),
        new("workflows", "Agent workflow files", "workflows")
    ];

    /// <summary>
    /// Scaffold the complete DynaDocs structure with JITI documentation funnel.
    /// </summary>
    public void Scaffold(string basePath)
    {
        // Create folder structure
        foreach (var folder in Folders)
        {
            var folderPath = Path.Combine(basePath, folder.Path);
            Directory.CreateDirectory(folderPath);

            // Only create _index.md for main content folders (not subfolders like project/tasks)
            if (!folder.Path.Contains('/'))
            {
                var indexPath = Path.Combine(folderPath, "_index.md");
                if (!File.Exists(indexPath))
                {
                    var content = GenerateHubContent(folder);
                    File.WriteAllText(indexPath, content);
                }
            }
        }

        // Create agents folder (will be gitignored)
        var agentsPath = Path.Combine(basePath, "agents");
        Directory.CreateDirectory(agentsPath);

        // Create root index.md with JITI entry point
        var rootIndexPath = Path.Combine(basePath, "index.md");
        if (!File.Exists(rootIndexPath))
        {
            var agentNames = PresetAgentNames.Set1.ToList();
            var indexContent = TemplateGenerator.GenerateIndexMd(agentNames);
            File.WriteAllText(rootIndexPath, indexContent);
        }

        // Create workflow files for each agent
        ScaffoldWorkflowFiles(basePath);

        // Create foundation documentation (must-reads)
        ScaffoldFoundationDocs(basePath);
    }

    /// <summary>
    /// Scaffold with custom agent names (from config).
    /// </summary>
    public void Scaffold(string basePath, List<string> agentNames)
    {
        // Create folder structure
        foreach (var folder in Folders)
        {
            var folderPath = Path.Combine(basePath, folder.Path);
            Directory.CreateDirectory(folderPath);

            if (!folder.Path.Contains('/'))
            {
                var indexPath = Path.Combine(folderPath, "_index.md");
                if (!File.Exists(indexPath))
                {
                    var content = GenerateHubContent(folder);
                    File.WriteAllText(indexPath, content);
                }
            }
        }

        // Create agents folder
        var agentsPath = Path.Combine(basePath, "agents");
        Directory.CreateDirectory(agentsPath);

        // Create root index.md
        var rootIndexPath = Path.Combine(basePath, "index.md");
        if (!File.Exists(rootIndexPath))
        {
            var indexContent = TemplateGenerator.GenerateIndexMd(agentNames);
            File.WriteAllText(rootIndexPath, indexContent);
        }

        // Create workflow files for provided agents
        ScaffoldWorkflowFiles(basePath, agentNames);

        // Create foundation documentation
        ScaffoldFoundationDocs(basePath);
    }

    /// <summary>
    /// Create workflow files for each agent in the pool.
    /// </summary>
    private void ScaffoldWorkflowFiles(string basePath, List<string>? agentNames = null)
    {
        agentNames ??= PresetAgentNames.Set1.ToList();

        var workflowsPath = Path.Combine(basePath, "workflows");
        Directory.CreateDirectory(workflowsPath);

        foreach (var agentName in agentNames)
        {
            var workflowPath = Path.Combine(workflowsPath, $"{agentName.ToLowerInvariant()}.md");
            if (!File.Exists(workflowPath))
            {
                var content = TemplateGenerator.GenerateWorkflowFile(agentName);
                File.WriteAllText(workflowPath, content);
            }
        }

        // Create workflows _index.md
        var workflowsIndexPath = Path.Combine(workflowsPath, "_index.md");
        if (!File.Exists(workflowsIndexPath))
        {
            var content = GenerateWorkflowsIndex(agentNames);
            File.WriteAllText(workflowsIndexPath, content);
        }
    }

    /// <summary>
    /// Create the foundation documentation files (must-reads).
    /// </summary>
    private void ScaffoldFoundationDocs(string basePath)
    {
        // understand/architecture.md
        var architecturePath = Path.Combine(basePath, "understand", "architecture.md");
        if (!File.Exists(architecturePath))
        {
            File.WriteAllText(architecturePath, TemplateGenerator.GenerateArchitectureMd());
        }

        // guides/coding-standards.md
        var codingStandardsPath = Path.Combine(basePath, "guides", "coding-standards.md");
        if (!File.Exists(codingStandardsPath))
        {
            File.WriteAllText(codingStandardsPath, TemplateGenerator.GenerateCodingStandardsMd());
        }

        // guides/how-to-use-docs.md
        var howToPath = Path.Combine(basePath, "guides", "how-to-use-docs.md");
        if (!File.Exists(howToPath))
        {
            File.WriteAllText(howToPath, TemplateGenerator.GenerateHowToUseDocsMd());
        }

        // files-off-limits.md (security config)
        var offLimitsPath = Path.Combine(basePath, "files-off-limits.md");
        if (!File.Exists(offLimitsPath))
        {
            File.WriteAllText(offLimitsPath, TemplateGenerator.GenerateFilesOffLimitsMd());
        }
    }

    private static string GenerateHubContent(FolderSpec folder)
    {
        var title = char.ToUpper(folder.Path[0]) + folder.Path[1..];

        // Remove path prefix for subfolders
        if (folder.Path.Contains('/'))
        {
            var parts = folder.Path.Split('/');
            title = char.ToUpper(parts[^1][0]) + parts[^1][1..];
        }

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

    private static string GenerateWorkflowsIndex(List<string> agentNames)
    {
        var links = string.Join("\n", agentNames.Select(name =>
            $"- [{name}]({name.ToLowerInvariant()}.md)"));

        return $"""
            ---
            area: workflows
            type: hub
            ---

            # Agent Workflows

            Each agent has a dedicated workflow file containing:
            - Identity and claim command
            - Must-read document list
            - Role permissions and task workflow

            ## Available Agents

            {links}

            ---

            ## How to Use

            1. Pick an agent from the list above
            2. Read its workflow file
            3. Follow the instructions to claim and begin work

            See [../index.md](../index.md) for the full getting started guide.
            """;
    }
}
