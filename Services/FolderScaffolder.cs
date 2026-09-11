namespace DynaDocs.Services;

using DynaDocs.Models;

public class FolderScaffolder : IFolderScaffolder
{
    private readonly record struct FolderSpec(string Path);

    private static readonly FolderSpec[] Folders =
    [
        new("understand"),
        new("guides"),
        new("reference"),
        new("project"),
        new("project/decisions"),
        new("project/changelog"),
        new("project/pitfalls"),
        new("project/releases"),
        new("project/future-features"),
        new("_system"),
        new("_system/templates"),
        new("_system/.local"),
        new("_assets")
    ];

    private static readonly (string RelativePath, Func<string> Generate)[] DocFiles =
    [
        ("welcome.md", TemplateGenerator.GenerateWelcomeMd),
        ("glossary.md", TemplateGenerator.GenerateGlossaryMd),
        ("understand/about.md", TemplateGenerator.GenerateAboutMd),
        ("understand/architecture.md", TemplateGenerator.GenerateArchitectureMd),
        ("guides/coding-standards.md", TemplateGenerator.GenerateCodingStandardsMd),
        ("guides/working-tree-contract.md", TemplateGenerator.GenerateWorkingTreeContractMd),
        ("reference/dydo-commands.md", TemplateGenerator.GenerateDydoCommandsMd),
        ("reference/dydo-glossary.md", TemplateGenerator.GenerateDydoGlossaryMd),
        ("reference/linear-workspace-standard.md", TemplateGenerator.GenerateLinearWorkspaceStandardMd),
        ("reference/writing-docs.md", TemplateGenerator.GenerateWritingDocsMd),
        ("reference/about-dynadocs.md", TemplateGenerator.GenerateAboutDynadocsMd),
        ("files-off-limits.md", TemplateGenerator.GenerateFilesOffLimitsMd),
        ("understand/_understand.md", TemplateGenerator.GenerateUnderstandMetaMd),
        ("guides/_guides.md", TemplateGenerator.GenerateGuidesMetaMd),
        ("reference/_reference.md", TemplateGenerator.GenerateReferenceMetaMd),
        ("project/_project.md", TemplateGenerator.GenerateProjectMetaMd),
        ("project/decisions/_decisions.md", TemplateGenerator.GenerateDecisionsMetaMd),
        ("project/changelog/_changelog.md", TemplateGenerator.GenerateChangelogMetaMd),
        ("project/pitfalls/_pitfalls.md", TemplateGenerator.GeneratePitfallsMetaMd),
        ("project/future-features/_future-features.md", TemplateGenerator.GenerateFutureFeaturesMetaMd),
    ];

    public void Scaffold(string basePath)
    {
        foreach (var folder in Folders)
            Directory.CreateDirectory(Path.Combine(basePath, folder.Path));

        // The single SHARED scratch folder (gitignored): agents drop temporary work products
        // here instead of polluting the repo root. No per-agent subfolders (DR-041 — identity
        // is assigned at spawn, nothing owns a named workspace).
        Directory.CreateDirectory(Path.Combine(basePath, "agents", "workspace"));

        ScaffoldTemplateAdditions(basePath);
        ScaffoldSkillTemplates(basePath);
        ScaffoldTypesJson(basePath);
        CopyBuiltInAssets(basePath);

        WriteIfNotExists(
            Path.Combine(basePath, "index.md"),
            TemplateGenerator.GenerateIndexMd());

        ScaffoldDocFiles(basePath);
    }

    private void ScaffoldDocFiles(string basePath)
    {
        foreach (var (relativePath, generate) in DocFiles)
            WriteIfNotExists(Path.Combine(basePath, relativePath), generate());
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

    private static void ScaffoldSkillTemplates(string basePath)
    {
        var templateRoot = Path.Combine(basePath, "_system", "templates");
        Directory.CreateDirectory(templateRoot);
        foreach (var templateName in TemplateGenerator.GetAllTemplateNames())
            WriteIfNotExists(
                Path.Combine(templateRoot, templateName),
                TemplateGenerator.ReadBuiltInTemplate(templateName));
    }

    public static void StoreInitialFrameworkHashes(string basePath, DydoConfig config)
    {
        foreach (var relativePath in FrameworkCatalog.DocumentFiles)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            if (File.Exists(fullPath))
                config.FrameworkHashes[relativePath] = FrameworkCatalog.ComputeHash(File.ReadAllText(fullPath));
        }


        foreach (var templateName in TemplateGenerator.GetAllTemplateNames())
        {
            var relativePath = $"_system/templates/{templateName}";
            var fullPath = Path.Combine(basePath, "_system", "templates", templateName);
            config.FrameworkHashes[relativePath] = FrameworkCatalog.ComputeHash(File.ReadAllText(fullPath));
        }

        foreach (var skill in SkillTemplateService.DiscoverSkills())
        {
            var resources = TemplateGenerator.GetSkillResourceTemplateNames(skill.Name)
                .Select(name => name[$"resource-{skill.Name}-resource-".Length..^".template.md".Length])
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            config.Skills[skill.Name] = new SkillSwitchConfig
            {
                Enabled = true,
                Origin = "shipped",
                EmitAgent = skill.EmitAgent,
                CodexMetadata = skill.ExplicitInvocation || skill.ArgumentHint != null,
                Resources = resources
            };
        }
    }

    private static void WriteIfNotExists(string path, string content)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, content);
    }
}
