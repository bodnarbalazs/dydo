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
    public void EveryScaffoldFileOnDisk_ShipsAsAnEmbeddedResource()
    {
        var scaffoldDirectory = Path.Combine(RepositoryRoot(), "Scaffold");

        var onDisk = Directory.GetFiles(scaffoldDirectory, "*", SearchOption.AllDirectories)
            .Select(file => "Scaffold/" + Path.GetRelativePath(scaffoldDirectory, file).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(onDisk);

        var embedded = ScaffoldTree.ResourceNames.ToHashSet(StringComparer.Ordinal);
        var missing = onDisk.Where(name => !embedded.Contains(name)).ToList();

        Assert.True(missing.Count == 0,
            $"these Scaffold/ files are not embedded, so `dydo init` would not write them:\n  {string.Join("\n  ", missing)}");
    }

    [Fact]
    public void EmbeddedScaffoldResources_AreExactlyTheTreeOnDisk()
    {
        var scaffoldDirectory = Path.Combine(RepositoryRoot(), "Scaffold");

        var onDisk = Directory.GetFiles(scaffoldDirectory, "*", SearchOption.AllDirectories)
            .Select(file => "Scaffold/" + Path.GetRelativePath(scaffoldDirectory, file).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        var embedded = ScaffoldTree.ResourceNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(onDisk.OrderBy(name => name, StringComparer.Ordinal), embedded);
    }

    [Fact]
    public void EveryEmbeddedScaffoldResource_ReadsBackItsFileOnDisk()
    {
        var scaffoldDirectory = Path.Combine(RepositoryRoot(), "Scaffold");

        foreach (var name in ScaffoldTree.ResourceNames)
        {
            var onDisk = Path.Combine(scaffoldDirectory,
                name["Scaffold/".Length..].Replace('/', Path.DirectorySeparatorChar));

            Assert.Equal(
                File.ReadAllText(onDisk).ReplaceLineEndings("\n"),
                ScaffoldTree.Read(name).ReplaceLineEndings("\n"));
        }
    }

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
