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
        new("project/changelog", "Change history", "project")
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

        // Create agent workspaces with workflow and mode files
        ScaffoldAgentWorkspaces(basePath);

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

        // Create agent workspaces with workflow and mode files
        ScaffoldAgentWorkspaces(basePath, agentNames);

        // Create foundation documentation
        ScaffoldFoundationDocs(basePath);
    }

    /// <summary>
    /// Create agent workspaces with workflow.md and mode files for each agent.
    /// </summary>
    private void ScaffoldAgentWorkspaces(string basePath, List<string>? agentNames = null)
    {
        agentNames ??= PresetAgentNames.Set1.ToList();

        var agentsPath = Path.Combine(basePath, "agents");
        Directory.CreateDirectory(agentsPath);

        foreach (var agentName in agentNames)
        {
            ScaffoldAgentWorkspace(agentsPath, agentName);
        }
    }

    /// <summary>
    /// Create a single agent's workspace with workflow.md and mode files.
    /// </summary>
    public void ScaffoldAgentWorkspace(string agentsPath, string agentName)
    {
        var agentPath = Path.Combine(agentsPath, agentName);
        Directory.CreateDirectory(agentPath);

        // Create modes folder
        var modesPath = Path.Combine(agentPath, "modes");
        Directory.CreateDirectory(modesPath);

        // Create inbox folder
        var inboxPath = Path.Combine(agentPath, "inbox");
        Directory.CreateDirectory(inboxPath);

        // Create workflow.md
        var workflowPath = Path.Combine(agentPath, "workflow.md");
        if (!File.Exists(workflowPath))
        {
            var content = TemplateGenerator.GenerateWorkflowFile(agentName);
            File.WriteAllText(workflowPath, content);
        }

        // Create mode files
        foreach (var modeName in TemplateGenerator.GetModeNames())
        {
            var modePath = Path.Combine(modesPath, $"{modeName}.md");
            if (!File.Exists(modePath))
            {
                var content = TemplateGenerator.GenerateModeFile(agentName, modeName);
                File.WriteAllText(modePath, content);
            }
        }
    }

    /// <summary>
    /// Regenerate workflow and mode files for an agent (used after rename).
    /// </summary>
    public void RegenerateAgentFiles(string agentsPath, string agentName)
    {
        var agentPath = Path.Combine(agentsPath, agentName);
        var modesPath = Path.Combine(agentPath, "modes");

        // Regenerate workflow.md
        var workflowPath = Path.Combine(agentPath, "workflow.md");
        var workflowContent = TemplateGenerator.GenerateWorkflowFile(agentName);
        File.WriteAllText(workflowPath, workflowContent);

        // Regenerate mode files
        Directory.CreateDirectory(modesPath);
        foreach (var modeName in TemplateGenerator.GetModeNames())
        {
            var modePath = Path.Combine(modesPath, $"{modeName}.md");
            var modeContent = TemplateGenerator.GenerateModeFile(agentName, modeName);
            File.WriteAllText(modePath, modeContent);
        }
    }

    /// <summary>
    /// Create the foundation documentation files (must-reads).
    /// </summary>
    private void ScaffoldFoundationDocs(string basePath)
    {
        // understand/about.md (NEW - project context)
        var aboutPath = Path.Combine(basePath, "understand", "about.md");
        if (!File.Exists(aboutPath))
        {
            File.WriteAllText(aboutPath, TemplateGenerator.GenerateAboutMd());
        }

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
}
