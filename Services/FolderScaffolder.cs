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
        new("project/releases", "Release records — top of the Notion PM sync spine (DR 025)", "project"),
        new("project/campaigns", "Campaign records — Notion PM sync spine (DR 025)", "project"),
        new("project/sprints", "Sprint records — Notion PM sync spine (DR 025)", "project"),
        new("project/sprint-tasks", "Sprint task records — Notion PM sync spine (DR 025)", "project"),
        new("project/backlog", "Identified, scoped work not yet in flight", "project"),
        new("project/future-features", "Ideas not in scope for current version", "project"),
        new("_system", "System configuration (committed)", "_system"),
        new("_system/roles", "Role definition files", "_system"),
        new("_system/templates", "Project-local template overrides", "_system"),
        new("_system/.local", "Machine-local runtime state (not committed)", "_system"),
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
        ("reference/dydo-commands.md", TemplateGenerator.GenerateDydoCommandsMd),
        ("reference/writing-docs.md", TemplateGenerator.GenerateWritingDocsMd),
        ("reference/about-dynadocs.md", TemplateGenerator.GenerateAboutDynadocsMd),
        ("files-off-limits.md", TemplateGenerator.GenerateFilesOffLimitsMd),
        ("understand/_understand.md", TemplateGenerator.GenerateUnderstandMetaMd),
        ("guides/_guides.md", TemplateGenerator.GenerateGuidesMetaMd),
        ("reference/_reference.md", TemplateGenerator.GenerateReferenceMetaMd),
        ("project/_project.md", TemplateGenerator.GenerateProjectMetaMd),
        ("project/tasks/_tasks.md", TemplateGenerator.GenerateTasksMetaMd),
        ("project/decisions/_decisions.md", TemplateGenerator.GenerateDecisionsMetaMd),
        ("project/changelog/_changelog.md", TemplateGenerator.GenerateChangelogMetaMd),
        ("project/pitfalls/_pitfalls.md", TemplateGenerator.GeneratePitfallsMetaMd),
        ("project/issues/_issues.md", TemplateGenerator.GenerateIssuesMetaMd),
        ("project/backlog/_backlog.md", TemplateGenerator.GenerateBacklogMetaMd),
        ("project/future-features/_future-features.md", TemplateGenerator.GenerateFutureFeaturesMetaMd),
    ];

    public void Scaffold(string basePath)
    {
        foreach (var folder in Folders)
            Directory.CreateDirectory(Path.Combine(basePath, folder.Path));

        // Empty, gitignored workspace root. The 26-agent roster was removed (DR-041); the guard
        // creates this on demand for its global warn-nudge markers, but scaffolding it keeps the
        // directory the .gitignore entry references present.
        Directory.CreateDirectory(Path.Combine(basePath, "agents"));

        CopyBuiltInTemplates(basePath);
        ScaffoldTemplateAdditions(basePath);
        ScaffoldTypesJson(basePath);
        new RoleDefinitionService().WriteBaseRoleDefinitions(basePath);
        CopyBuiltInAssets(basePath);

        WriteIfNotExists(
            Path.Combine(basePath, "index.md"),
            TemplateGenerator.GenerateIndexMd());

        ScaffoldDocFiles(basePath);
        GenerateHubFiles(basePath);
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

    private void ScaffoldTypesJson(string basePath)
    {
        var path = Path.Combine(basePath, FrontmatterTypesService.TypesJsonRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteIfNotExists(path, TemplateGenerator.ReadBuiltInTemplate("types.json.template"));
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
}
