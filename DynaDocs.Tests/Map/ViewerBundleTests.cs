namespace DynaDocs.Tests.Map;

using System.Text;
using DynaDocs.Services.Map;

public class ViewerBundleTests
{
    private static readonly ViewerBundle Empty = new(_ => null);

    [Theory]
    [InlineData("index.html", "text/html; charset=utf-8")]
    [InlineData("assets/index-1a2b.js", "text/javascript; charset=utf-8")]
    [InlineData("assets/worker.mjs", "text/javascript; charset=utf-8")]
    [InlineData("assets/index-1a2b.CSS", "text/css; charset=utf-8")]
    [InlineData("assets/index.js.map", "application/json; charset=utf-8")]
    [InlineData("data.json", "application/json; charset=utf-8")]
    [InlineData("favicon.svg", "image/svg+xml")]
    [InlineData("logo.png", "image/png")]
    [InlineData("favicon.ico", "image/x-icon")]
    [InlineData("font.woff", "font/woff")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("robots.txt", "text/plain; charset=utf-8")]
    [InlineData("blob.bin", "application/octet-stream")]
    public void ContentType_FollowsTheExtension(string name, string contentType) =>
        Assert.Equal(contentType, ViewerBundle.ContentType(name));

    [Fact]
    public void Root_WithoutABundle_IsTheNotBuiltPage()
    {
        var page = Empty.Find("/");

        Assert.False(Empty.IsBuilt);
        Assert.Equal("text/html; charset=utf-8", page!.Value.ContentType);
        Assert.Contains("viewer was not built", Encoding.UTF8.GetString(page.Value.Body));
        Assert.Null(Empty.Find("/assets/app.js"));
    }

    [Fact]
    public void Root_IsIndexHtml_WhenBuilt()
    {
        var bundle = new ViewerBundle(name => name == "index.html" ? [1, 2] : null);

        Assert.True(bundle.IsBuilt);
        var (contentType, body) = bundle.Find("/")!.Value;
        Assert.Equal("text/html; charset=utf-8", contentType);
        Assert.Equal([1, 2], body);
    }

    [Fact]
    public void Resources_ReadEmbeddedFilesByRepositoryPath()
    {
        var read = ViewerBundle.Resources("Scaffold/");

        Assert.Contains("{{PROJECT_NAME}}", Encoding.UTF8.GetString(read("entry-point.md")!));
        Assert.Null(read("no-such-file.md"));
    }
}
