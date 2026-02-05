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
        new("project/decisions", "Decision records", "project"),
        new("project/changelog", "Change history", "project"),
        new("project/pitfalls", "Known issues and gotchas", "project"),
        new("_system", "System configuration (committed)", "_system"),
        new("_system/templates", "Project-local template overrides", "_system"),
        new("_system/audit", "Agent activity audit logs", "_system"),
        new("_system/audit/reports", "Generated audit visualizations", "_system"),
        new("_assets", "Documentation assets (images, diagrams)", "_assets")
    ];

    /// <summary>
    /// Scaffold the complete DynaDocs structure with JITI documentation funnel.
    /// </summary>
    public void Scaffold(string basePath)
    {
        // Create folder structure (without hubs - they'll be generated at the end)
        foreach (var folder in Folders)
        {
            var folderPath = Path.Combine(basePath, folder.Path);
            Directory.CreateDirectory(folderPath);
        }

        // Create agents folder (will be gitignored)
        var agentsPath = Path.Combine(basePath, "agents");
        Directory.CreateDirectory(agentsPath);

        // Copy built-in templates to _system/templates/ for customization
        CopyBuiltInTemplates(basePath);

        // Copy built-in assets to _assets/
        CopyBuiltInAssets(basePath);

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

        // Create project subfolder documentation (meta files only, hubs generated below)
        ScaffoldProjectSubfolderMetaFiles(basePath);

        // Generate all hub files now that docs exist
        GenerateHubFiles(basePath);
    }

    /// <summary>
    /// Scaffold with custom agent names (from config).
    /// </summary>
    public void Scaffold(string basePath, List<string> agentNames)
    {
        // Create folder structure (without hubs - they'll be generated at the end)
        foreach (var folder in Folders)
        {
            var folderPath = Path.Combine(basePath, folder.Path);
            Directory.CreateDirectory(folderPath);
        }

        // Create agents folder
        var agentsPath = Path.Combine(basePath, "agents");
        Directory.CreateDirectory(agentsPath);

        // Copy built-in templates to _system/templates/ for customization
        CopyBuiltInTemplates(basePath);

        // Copy built-in assets to _assets/
        CopyBuiltInAssets(basePath);

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

        // Create project subfolder documentation (meta files only, hubs generated below)
        ScaffoldProjectSubfolderMetaFiles(basePath);

        // Generate all hub files now that docs exist
        GenerateHubFiles(basePath);
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
        // Derive dydo basePath from agentsPath (agentsPath is dydo/agents/)
        var basePath = Path.GetDirectoryName(agentsPath);

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
            var content = TemplateGenerator.GenerateWorkflowFile(agentName, basePath);
            File.WriteAllText(workflowPath, content);
        }

        // Create mode files
        foreach (var modeName in TemplateGenerator.GetModeNames())
        {
            var modePath = Path.Combine(modesPath, $"{modeName}.md");
            if (!File.Exists(modePath))
            {
                var content = TemplateGenerator.GenerateModeFile(agentName, modeName, basePath);
                File.WriteAllText(modePath, content);
            }
        }
    }

    /// <summary>
    /// Regenerate workflow and mode files for an agent (used after rename).
    /// </summary>
    public void RegenerateAgentFiles(string agentsPath, string agentName)
    {
        // Derive dydo basePath from agentsPath (agentsPath is dydo/agents/)
        var basePath = Path.GetDirectoryName(agentsPath);

        var agentPath = Path.Combine(agentsPath, agentName);
        var modesPath = Path.Combine(agentPath, "modes");

        // Regenerate workflow.md
        var workflowPath = Path.Combine(agentPath, "workflow.md");
        var workflowContent = TemplateGenerator.GenerateWorkflowFile(agentName, basePath);
        File.WriteAllText(workflowPath, workflowContent);

        // Regenerate mode files
        Directory.CreateDirectory(modesPath);
        foreach (var modeName in TemplateGenerator.GetModeNames())
        {
            var modePath = Path.Combine(modesPath, $"{modeName}.md");
            var modeContent = TemplateGenerator.GenerateModeFile(agentName, modeName, basePath);
            File.WriteAllText(modePath, modeContent);
        }
    }

    /// <summary>
    /// Create the foundation documentation files (must-reads).
    /// </summary>
    private void ScaffoldFoundationDocs(string basePath)
    {
        // welcome.md (human entry point, alongside index.md)
        var welcomePath = Path.Combine(basePath, "welcome.md");
        if (!File.Exists(welcomePath))
        {
            File.WriteAllText(welcomePath, TemplateGenerator.GenerateWelcomeMd());
        }

        // glossary.md (project glossary, referenced by welcome.md)
        var glossaryPath = Path.Combine(basePath, "glossary.md");
        if (!File.Exists(glossaryPath))
        {
            File.WriteAllText(glossaryPath, TemplateGenerator.GenerateGlossaryMd());
        }

        // understand/about.md (project context)
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

        // reference/dydo-commands.md
        var dydoCommandsPath = Path.Combine(basePath, "reference", "dydo-commands.md");
        if (!File.Exists(dydoCommandsPath))
        {
            File.WriteAllText(dydoCommandsPath, TemplateGenerator.GenerateDydoCommandsMd());
        }

        // reference/writing-docs.md
        var writingDocsPath = Path.Combine(basePath, "reference", "writing-docs.md");
        if (!File.Exists(writingDocsPath))
        {
            File.WriteAllText(writingDocsPath, TemplateGenerator.GenerateWritingDocsMd());
        }

        // reference/about-dynadocs.md
        var aboutDynadocsPath = Path.Combine(basePath, "reference", "about-dynadocs.md");
        if (!File.Exists(aboutDynadocsPath))
        {
            File.WriteAllText(aboutDynadocsPath, TemplateGenerator.GenerateAboutDynadocsMd());
        }

        // files-off-limits.md (security config)
        var offLimitsPath = Path.Combine(basePath, "files-off-limits.md");
        if (!File.Exists(offLimitsPath))
        {
            File.WriteAllText(offLimitsPath, TemplateGenerator.GenerateFilesOffLimitsMd());
        }

        // _system/audit/_audit.md (audit system documentation)
        var auditMetaPath = Path.Combine(basePath, "_system", "audit", "_audit.md");
        if (!File.Exists(auditMetaPath))
        {
            File.WriteAllText(auditMetaPath, GenerateAuditMetaMd());
        }

        // Main folder meta files
        var understandMetaPath = Path.Combine(basePath, "understand", "_understand.md");
        if (!File.Exists(understandMetaPath))
        {
            File.WriteAllText(understandMetaPath, TemplateGenerator.GenerateUnderstandMetaMd());
        }

        var guidesMetaPath = Path.Combine(basePath, "guides", "_guides.md");
        if (!File.Exists(guidesMetaPath))
        {
            File.WriteAllText(guidesMetaPath, TemplateGenerator.GenerateGuidesMetaMd());
        }

        var referenceMetaPath = Path.Combine(basePath, "reference", "_reference.md");
        if (!File.Exists(referenceMetaPath))
        {
            File.WriteAllText(referenceMetaPath, TemplateGenerator.GenerateReferenceMetaMd());
        }

        var projectMetaPath = Path.Combine(basePath, "project", "_project.md");
        if (!File.Exists(projectMetaPath))
        {
            File.WriteAllText(projectMetaPath, TemplateGenerator.GenerateProjectMetaMd());
        }
    }

    /// <summary>
    /// Create meta files for project subfolders (tasks, decisions, changelog, pitfalls).
    /// Hub files are generated separately by GenerateHubFiles().
    /// </summary>
    private void ScaffoldProjectSubfolderMetaFiles(string basePath)
    {
        // Tasks folder meta
        var tasksMetaPath = Path.Combine(basePath, "project", "tasks", "_tasks.md");
        if (!File.Exists(tasksMetaPath))
        {
            File.WriteAllText(tasksMetaPath, TemplateGenerator.GenerateTasksMetaMd());
        }

        // Decisions folder meta
        var decisionsMetaPath = Path.Combine(basePath, "project", "decisions", "_decisions.md");
        if (!File.Exists(decisionsMetaPath))
        {
            File.WriteAllText(decisionsMetaPath, TemplateGenerator.GenerateDecisionsMetaMd());
        }

        // Changelog folder meta
        var changelogMetaPath = Path.Combine(basePath, "project", "changelog", "_changelog.md");
        if (!File.Exists(changelogMetaPath))
        {
            File.WriteAllText(changelogMetaPath, TemplateGenerator.GenerateChangelogMetaMd());
        }

        // Pitfalls folder meta
        var pitfallsMetaPath = Path.Combine(basePath, "project", "pitfalls", "_pitfalls.md");
        if (!File.Exists(pitfallsMetaPath))
        {
            File.WriteAllText(pitfallsMetaPath, TemplateGenerator.GeneratePitfallsMetaMd());
        }
    }

    /// <summary>
    /// Generate hub files (_index.md) for all documentation folders.
    /// Uses DocScanner to scan existing docs and HubGenerator for consistent output.
    /// </summary>
    private void GenerateHubFiles(string basePath)
    {
        var parser = new MarkdownParser();
        var scanner = new DocScanner(parser);
        var docs = scanner.ScanDirectory(basePath);

        var hubs = HubGenerator.GenerateAllHubs(basePath, docs);

        foreach (var (relativePath, content) in hubs)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(fullPath, content);
        }
    }

    /// <summary>
    /// Copy all built-in templates to _system/templates/ for project-local customization.
    /// </summary>
    private void CopyBuiltInTemplates(string basePath)
    {
        var destPath = Path.Combine(basePath, "_system", "templates");
        Directory.CreateDirectory(destPath);

        foreach (var templateName in TemplateGenerator.GetAllTemplateNames())
        {
            var destFile = Path.Combine(destPath, templateName);
            if (!File.Exists(destFile))
            {
                var content = TemplateGenerator.ReadBuiltInTemplate(templateName);
                File.WriteAllText(destFile, content);
            }
        }
    }

    /// <summary>
    /// Copy all built-in assets to _assets/ folder.
    /// </summary>
    private void CopyBuiltInAssets(string basePath)
    {
        var destPath = Path.Combine(basePath, "_assets");
        Directory.CreateDirectory(destPath);

        foreach (var assetName in TemplateGenerator.GetAssetNames())
        {
            var destFile = Path.Combine(destPath, assetName);
            if (!File.Exists(destFile))
            {
                var content = TemplateGenerator.ReadEmbeddedAsset(assetName);
                if (content != null)
                {
                    File.WriteAllBytes(destFile, content);
                }
            }
        }
    }

    /// <summary>
    /// Generate the audit system meta file content.
    /// </summary>
    private static string GenerateAuditMetaMd() => """
        ---
        title: Audit System
        type: guide
        area: general
        ---

        # Audit System

        This folder contains audit logs tracking agent activity.

        ## Structure

        - `YYYY/` - Year folders containing session logs
        - `reports/` - Generated HTML visualizations

        ## Usage

        ```bash
        # Generate replay visualization (all sessions)
        dydo audit

        # Filter to specific year
        dydo audit /2025

        # List available sessions
        dydo audit --list

        # Show details for a specific session
        dydo audit --session <session-id>
        ```

        ## Log Format

        Each session is stored as `yyyy-mm-dd-sessionid.json` containing all events from that session:

        ```json
        {
          "session": "abc123",
          "agent": "Alpha",
          "human": "john",
          "started": "2025-01-15T10:23:45Z",
          "git_head": "a1b2c3d",
          "events": [
            {"ts": "...", "event": "claim", "agent": "Alpha"},
            {"ts": "...", "event": "role", "role": "docs-writer"},
            {"ts": "...", "event": "read", "path": "dydo/docs/api.md"},
            {"ts": "...", "event": "edit", "path": "src/auth.ts"},
            {"ts": "...", "event": "bash", "cmd": "npm test"}
          ]
        }
        ```

        ## Event Types

        - `claim` - Agent claimed identity
        - `release` - Agent released identity
        - `role` - Role changed
        - `read` - File read
        - `write` - File created
        - `edit` - File modified
        - `delete` - File deleted
        - `bash` - Command executed
        - `commit` - Git commit made
        - `blocked` - Action blocked by guard

        ## Visualization

        Run `dydo audit` to generate an interactive HTML replay. Open `reports/replay.html` in your browser to visualize agent activity as a graph.
        """;
}

