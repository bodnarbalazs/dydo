namespace DynaDocs.Services;

using DynaDocs.Commands;
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
        new("project/issues", "Actionable work items with lifecycle", "project"),
        new("_system", "System configuration (committed)", "_system"),
        new("_system/roles", "Role definition files", "_system"),
        new("_system/templates", "Project-local template overrides", "_system"),
        new("_system/.local", "Machine-local runtime state (not committed)", "_system"),
        new("_system/audit", "Agent activity audit logs", "_system"),
        new("_system/audit/reports", "Generated audit visualizations", "_system"),
        new("_assets", "Documentation assets (images, diagrams)", "_assets")
    ];

    private static readonly (string RelativePath, Func<string> Generate)[] DocFiles =
    [
        ("welcome.md", TemplateGenerator.GenerateWelcomeMd),
        ("glossary.md", TemplateGenerator.GenerateGlossaryMd),
        ("understand/about.md", TemplateGenerator.GenerateAboutMd),
        ("understand/architecture.md", TemplateGenerator.GenerateArchitectureMd),
        ("guides/coding-standards.md", TemplateGenerator.GenerateCodingStandardsMd),
        ("guides/how-to-use-docs.md", TemplateGenerator.GenerateHowToUseDocsMd),
        ("guides/how-to-merge-worktrees.md", TemplateGenerator.GenerateHowToMergeWorktreesMd),
        ("guides/how-to-review-worktree-merges.md", TemplateGenerator.GenerateHowToReviewWorktreeMergesMd),
        ("reference/dydo-commands.md", TemplateGenerator.GenerateDydoCommandsMd),
        ("reference/writing-docs.md", TemplateGenerator.GenerateWritingDocsMd),
        ("reference/about-dynadocs.md", TemplateGenerator.GenerateAboutDynadocsMd),
        ("files-off-limits.md", TemplateGenerator.GenerateFilesOffLimitsMd),
        ("_system/audit/_audit.md", GenerateAuditMetaMd),
        ("understand/_understand.md", TemplateGenerator.GenerateUnderstandMetaMd),
        ("guides/_guides.md", TemplateGenerator.GenerateGuidesMetaMd),
        ("reference/_reference.md", TemplateGenerator.GenerateReferenceMetaMd),
        ("project/_project.md", TemplateGenerator.GenerateProjectMetaMd),
        ("project/tasks/_tasks.md", TemplateGenerator.GenerateTasksMetaMd),
        ("project/decisions/_decisions.md", TemplateGenerator.GenerateDecisionsMetaMd),
        ("project/changelog/_changelog.md", TemplateGenerator.GenerateChangelogMetaMd),
        ("project/pitfalls/_pitfalls.md", TemplateGenerator.GeneratePitfallsMetaMd),
        ("project/issues/_issues.md", TemplateGenerator.GenerateIssuesMetaMd),
    ];

    public void Scaffold(string basePath) =>
        Scaffold(basePath, PresetAgentNames.Set1.ToList());

    public void Scaffold(string basePath, List<string> agentNames)
    {
        foreach (var folder in Folders)
            Directory.CreateDirectory(Path.Combine(basePath, folder.Path));

        Directory.CreateDirectory(Path.Combine(basePath, "agents"));

        CopyBuiltInTemplates(basePath);
        ScaffoldTemplateAdditions(basePath);
        new RoleDefinitionService().WriteBaseRoleDefinitions(basePath);
        CopyBuiltInAssets(basePath);

        WriteIfNotExists(
            Path.Combine(basePath, "index.md"),
            TemplateGenerator.GenerateIndexMd(agentNames));

        ScaffoldAgentWorkspaces(basePath, agentNames);
        ScaffoldDocFiles(basePath);
        GenerateHubFiles(basePath);
    }

    private void ScaffoldAgentWorkspaces(string basePath, List<string> agentNames)
    {
        var agentsPath = Path.Combine(basePath, "agents");
        Directory.CreateDirectory(agentsPath);

        foreach (var agentName in agentNames)
            ScaffoldAgentWorkspace(agentsPath, agentName);
    }

    public void ScaffoldAgentWorkspace(string agentsPath, string agentName)
    {
        var basePath = Path.GetDirectoryName(agentsPath);

        var agentPath = Path.Combine(agentsPath, agentName);
        Directory.CreateDirectory(agentPath);
        Directory.CreateDirectory(Path.Combine(agentPath, "inbox"));

        WriteIfNotExists(
            Path.Combine(agentPath, "workflow.md"),
            TemplateGenerator.GenerateWorkflowFile(agentName, basePath));
    }

    public void RegenerateAgentFiles(string agentsPath, string agentName,
        List<string>? sourcePaths = null, List<string>? testPaths = null)
    {
        var basePath = Path.GetDirectoryName(agentsPath);

        var agentPath = Path.Combine(agentsPath, agentName);
        var modesPath = Path.Combine(agentPath, "modes");

        File.WriteAllText(
            Path.Combine(agentPath, "workflow.md"),
            TemplateGenerator.GenerateWorkflowFile(agentName, basePath, sourcePaths, testPaths));

        Directory.CreateDirectory(modesPath);
        foreach (var modeName in TemplateGenerator.GetModeNames())
            File.WriteAllText(
                Path.Combine(modesPath, $"{modeName}.md"),
                TemplateGenerator.GenerateModeFile(agentName, modeName, basePath, sourcePaths, testPaths));
    }

    private void ScaffoldDocFiles(string basePath)
    {
        foreach (var (relativePath, generate) in DocFiles)
            WriteIfNotExists(Path.Combine(basePath, relativePath), generate());
    }

    private void GenerateHubFiles(string basePath)
    {
        var parser = new MarkdownParser();
        var scanner = new DocScanner(parser);
        var docs = scanner.ScanDirectory(basePath);

        var hubs = HubGenerator.GenerateAllHubs(basePath, docs);

        foreach (var (relativePath, content) in hubs)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }
    }

    public void CopyBuiltInTemplates(string basePath)
    {
        var destPath = Path.Combine(basePath, "_system", "templates");
        Directory.CreateDirectory(destPath);

        foreach (var templateName in TemplateGenerator.GetAllTemplateNames())
            WriteIfNotExists(
                Path.Combine(destPath, templateName),
                TemplateGenerator.ReadBuiltInTemplate(templateName));
    }

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
                    File.WriteAllBytes(destFile, content);
            }
        }
    }

    private void ScaffoldTemplateAdditions(string basePath)
    {
        var destPath = Path.Combine(basePath, "_system", "template-additions");
        Directory.CreateDirectory(destPath);

        WriteIfNotExists(
            Path.Combine(destPath, "_README.md"),
            TemplateGenerator.ReadBuiltInTemplate("template-additions-readme.md"));

        WriteIfNotExists(
            Path.Combine(destPath, "extra-verify.md.example"),
            TemplateGenerator.ReadBuiltInTemplate("extra-verify.example.md"));
    }

    public static void StoreInitialFrameworkHashes(string basePath, DydoConfig config)
    {
        foreach (var relativePath in TemplateCommand.FrameworkTemplateFiles
            .Concat(TemplateCommand.FrameworkDocFiles))
        {
            var fullPath = Path.Combine(basePath, relativePath);
            if (File.Exists(fullPath))
                config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(File.ReadAllText(fullPath));
        }

        foreach (var relativePath in TemplateCommand.FrameworkBinaryFiles)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            if (File.Exists(fullPath))
                config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHashBytes(File.ReadAllBytes(fullPath));
        }
    }

    private static void WriteIfNotExists(string path, string content)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, content);
    }

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
