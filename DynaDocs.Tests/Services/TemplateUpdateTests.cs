namespace DynaDocs.Tests.Services;

using DynaDocs.Commands;

public class TemplateUpdateTests
{
    [Fact]
    public void ComputeHash_ConsistentForSameContent()
    {
        var hash1 = TemplateCommand.ComputeHash("Hello, world!");
        var hash2 = TemplateCommand.ComputeHash("Hello, world!");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentContent_DifferentHash()
    {
        var hash1 = TemplateCommand.ComputeHash("Content A");
        var hash2 = TemplateCommand.ComputeHash("Content B");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsLowercaseHex()
    {
        var hash = TemplateCommand.ComputeHash("test");

        Assert.Matches("^[0-9a-f]+$", hash);
        Assert.Equal(64, hash.Length); // SHA256 = 32 bytes = 64 hex chars
    }
}
