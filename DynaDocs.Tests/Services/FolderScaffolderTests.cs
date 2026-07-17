namespace DynaDocs.Tests.Services;

using DynaDocs.Services;
using DynaDocs.Sync.Model;

public class FolderScaffolderTests : IDisposable
{
    private readonly string _testDir;
    private readonly FolderScaffolder _scaffolder;

    public FolderScaffolderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-scaffold-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _scaffolder = new FolderScaffolder();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Scaffold_CreatesExpectedFolderStructure()
    {
        _scaffolder.Scaffold(_testDir);

        Assert.True(Directory.Exists(Path.Combine(_testDir, "understand")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "guides")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "reference")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "tasks")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "decisions")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "changelog")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "pitfalls")));
        // The Notion PM sync spine (DR 025) — each object type maps to one of these project subfolders, so
        // init must scaffold them or e.g. the "dydo Releases" board has no source folder.
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "releases")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "campaigns")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "sprints")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "project", "slices")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "agents")));
    }

    [Fact]
    public void Scaffold_CreatesEverySyncModelSpineFolder()
    {
        // Drift guard: every object type in the default sync-model.json maps to a project subfolder, and init
        // must scaffold each — else a shipped project can't sync that type (a "Releases" board with no source
        // folder, the gap this fixed). Adding a spine object type without its scaffold folder fails here. The
        // folder counterpart of the skills/agents "project uses only what ships with dydo" consistency checks.
        _scaffolder.Scaffold(_testDir);

        var model = SyncModelLoader.Load(_testDir);
        foreach (var obj in model.Objects)
            Assert.True(Directory.Exists(Path.Combine(_testDir, obj.Dir)),
                $"scaffold is missing sync-model dir: {obj.Dir}");
    }

    [Fact]
    public void Scaffold_CreatesRootIndexMd()
    {
        _scaffolder.Scaffold(_testDir);

        var indexPath = Path.Combine(_testDir, "index.md");
        Assert.True(File.Exists(indexPath));

        var content = File.ReadAllText(indexPath);
        Assert.Contains("DynaDocs", content);
    }

    [Fact]
    public void Scaffold_CreatesHubIndexFiles()
    {
        _scaffolder.Scaffold(_testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, "understand", "_index.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "guides", "_index.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "reference", "_index.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "project", "_index.md")));
    }

    [Fact]
    public void Scaffold_CreatesFoundationDocs()
    {
        _scaffolder.Scaffold(_testDir);

        Assert.True(File.Exists(Path.Combine(_testDir, "welcome.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "glossary.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "understand", "about.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "understand", "architecture.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "guides", "coding-standards.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "guides", "how-to-use-docs.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "reference", "writing-docs.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "files-off-limits.md")));
    }

    [Fact]
    public void Scaffold_CreatesGlossaryMd()
    {
        _scaffolder.Scaffold(_testDir);

        var glossaryPath = Path.Combine(_testDir, "glossary.md");
        Assert.True(File.Exists(glossaryPath));

        var content = File.ReadAllText(glossaryPath);
        Assert.Contains("Glossary", content);
        Assert.Contains("area: general", content);
        Assert.Contains("type: reference", content);
    }

    [Fact]
    public void Scaffold_CreatesDydoGlossaryMd()
    {
        _scaffolder.Scaffold(_testDir);

        var glossaryPath = Path.Combine(_testDir, "reference", "dydo-glossary.md");
        Assert.True(File.Exists(glossaryPath), "dydo-glossary.md should be created in reference/");

        var content = File.ReadAllText(glossaryPath);
        Assert.Contains("# dydo Glossary", content);
        Assert.Contains("Slice", content);
    }

    [Fact]
    public void Scaffold_AboutMd_ContainsProjectPlaceholders()
    {
        _scaffolder.Scaffold(_testDir);

        var aboutPath = Path.Combine(_testDir, "understand", "about.md");
        var content = File.ReadAllText(aboutPath);

        Assert.Contains("About This Project", content);
        Assert.Contains("Describe the project in 2-3 sentences", content);
        Assert.Contains("architecture.md", content);
    }

    [Fact]
    public void Scaffold_CreatesAssetsFolder()
    {
        _scaffolder.Scaffold(_testDir);

        Assert.True(Directory.Exists(Path.Combine(_testDir, "_assets")));
    }

    [Fact]
    public void Scaffold_CopiesDydoDiagramToAssets()
    {
        _scaffolder.Scaffold(_testDir);

        var diagramPath = Path.Combine(_testDir, "_assets", "dydo-diagram.svg");
        Assert.True(File.Exists(diagramPath), "dydo-diagram.svg should be copied to _assets/");

        // Verify it has content (not empty)
        var content = File.ReadAllBytes(diagramPath);
        Assert.True(content.Length > 0, "Diagram file should not be empty");
    }

    [Fact]
    public void Scaffold_CreatesAboutDynadocsMd()
    {
        _scaffolder.Scaffold(_testDir);

        var aboutDynadocsPath = Path.Combine(_testDir, "reference", "about-dynadocs.md");
        Assert.True(File.Exists(aboutDynadocsPath), "about-dynadocs.md should be created in reference/");

        var content = File.ReadAllText(aboutDynadocsPath);
        Assert.Contains("DynaDocs (dydo)", content);
        // The diagram was replaced by a deliberate visual placeholder (screenshot pending from balazs).
        Assert.Contains("<!-- VISUAL:", content);
    }

    [Fact]
    public void Scaffold_AboutDynadocs_LinksToAssetsFolder()
    {
        _scaffolder.Scaffold(_testDir);

        var aboutDynadocsPath = Path.Combine(_testDir, "reference", "about-dynadocs.md");
        var content = File.ReadAllText(aboutDynadocsPath);

        // The visual placeholder points at the _assets folder where the pending screenshot will live.
        Assert.Contains("_assets", content);
    }

    [Fact]
    public void Scaffold_DoesNotOverwriteExistingAssets()
    {
        // Create _assets folder and custom diagram first
        var assetsPath = Path.Combine(_testDir, "_assets");
        Directory.CreateDirectory(assetsPath);
        var customContent = "custom-svg-content";
        File.WriteAllText(Path.Combine(assetsPath, "dydo-diagram.svg"), customContent);

        _scaffolder.Scaffold(_testDir);

        // Should not overwrite existing asset
        var content = File.ReadAllText(Path.Combine(assetsPath, "dydo-diagram.svg"));
        Assert.Equal(customContent, content);
    }
}
