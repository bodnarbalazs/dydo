namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public sealed class LinkSafeDeleteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-linksafe-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => LinkSafeDelete.DeleteTree(_root);

    [Fact]
    public void DeleteTree_LinkInside_RemovesTheLinkAndKeepsItsTarget()
    {
        var (tree, target) = TreeWithLinkTo("keep.txt");

        LinkSafeDelete.DeleteTree(tree);

        Assert.False(Directory.Exists(tree));
        Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
    }

    [Fact]
    public void UnlinkAll_RemovesOnlyLinks()
    {
        var (tree, target) = TreeWithLinkTo("keep.txt");
        File.WriteAllText(Path.Combine(tree, "real.txt"), "real");

        LinkSafeDelete.UnlinkAll(tree);

        Assert.False(Directory.Exists(Path.Combine(tree, "nested", "link")));
        Assert.True(File.Exists(Path.Combine(tree, "real.txt")));
        Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
    }

    [Fact]
    public void DeleteTree_ReadOnlyFilesAndLongPaths_AreDeleted()
    {
        var tree = Path.Combine(_root, "tree");
        var deep = Path.Combine(tree, string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat("forty-character-folder-name-xxxxxxxxxxxx", 8)));
        Directory.CreateDirectory(LinkSafeDelete.Extended(deep));
        var file = LinkSafeDelete.Extended(Path.Combine(deep, "locked.txt"));
        File.WriteAllText(file, "x");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        LinkSafeDelete.DeleteTree(tree);

        Assert.False(Directory.Exists(LinkSafeDelete.Extended(tree)));
    }

    [Fact]
    public void DeleteTree_RootThatIsALink_RemovesOnlyTheLink()
    {
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "keep");
        var link = Path.Combine(_root, "link");
        DirectoryLinks.Create(link, target);

        LinkSafeDelete.DeleteTree(link);

        Assert.False(Directory.Exists(link));
        Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
    }

    [Fact]
    public void DeleteTree_MissingRoot_IsANoOp()
    {
        LinkSafeDelete.DeleteTree(Path.Combine(_root, "missing"));

        Assert.False(Directory.Exists(Path.Combine(_root, "missing")));
    }

    private (string Tree, string Target) TreeWithLinkTo(string file)
    {
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, file), "keep");
        var tree = Path.Combine(_root, "tree");
        DirectoryLinks.Create(Path.Combine(tree, "nested", "link"), target);
        return (tree, target);
    }
}
