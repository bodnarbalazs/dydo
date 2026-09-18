namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

/// <summary>
/// The scaffold ships as embedded resources, so a file can be added to `Scaffold/` in the
/// repository and silently never reach a scaffolded project. These tests bind the two together:
/// the tree on disk and the tree inside the assembly must be the same set of names.
/// </summary>
public class ScaffoldTreeTests
{
    [Fact]
    public void EmbeddedScaffoldResources_AreExactlyTheTreeOnDisk()
    {
        var onDisk = OnDiskResourceNames();
        Assert.NotEmpty(onDisk);

        var embedded = ScaffoldTree.ResourceNames.ToHashSet(StringComparer.Ordinal);
        var notEmbedded = onDisk.Where(name => !embedded.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal).ToList();
        var notOnDisk = embedded.Where(name => !onDisk.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal).ToList();

        Assert.True(notEmbedded.Count == 0 && notOnDisk.Count == 0,
            $"these Scaffold/ files are not embedded, so `dydo init` would not write them:\n  {string.Join("\n  ", notEmbedded)}\n"
            + $"these embedded resources have no file under Scaffold/:\n  {string.Join("\n  ", notOnDisk)}");
    }

    [Fact]
    public void EveryScaffoldFileOnDisk_ReadsBackThroughItsResourceName()
    {
        foreach (var file in ScaffoldFiles())
        {
            Assert.Equal(
                File.ReadAllText(file).ReplaceLineEndings("\n"),
                ScaffoldTree.Read(ResourceName(file)).ReplaceLineEndings("\n"));
        }
    }

    [Fact]
    public void WriteDydoTree_WithoutEmbeddedScaffold_Throws()
    {
        var target = Path.Combine(Path.GetTempPath(), $"dydo-scaffold-{Guid.NewGuid():N}");

        try
        {
            var error = Assert.Throws<FileNotFoundException>(
                () => ScaffoldTree.WriteDydoTree(target, []));

            Assert.Contains("Scaffold/dydo/", error.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(target));
        }
        finally
        {
            TestDirectory.Delete(target);
        }
    }

    private static string ScaffoldDirectory => Path.Combine(RepositoryRoot(), "Scaffold");

    private static IEnumerable<string> ScaffoldFiles() =>
        Directory.GetFiles(ScaffoldDirectory, "*", SearchOption.AllDirectories);

    // The one place the embedded-resource naming convention lives: repository-relative, forward slashes.
    private static string ResourceName(string file) =>
        "Scaffold/" + Path.GetRelativePath(ScaffoldDirectory, file).Replace('\\', '/');

    private static HashSet<string> OnDiskResourceNames() =>
        ScaffoldFiles().Select(ResourceName).ToHashSet(StringComparer.Ordinal);

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
