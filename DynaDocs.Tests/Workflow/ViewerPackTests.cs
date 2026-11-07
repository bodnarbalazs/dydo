namespace DynaDocs.Tests.Workflow;

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

public sealed class ViewerPackTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("dydo-viewer-pack-").FullName;

    public ViewerPackTests()
    {
        // Keep the real packaging configuration and viewer items, with a tiny dependency-free app.
        var source = ReleaseWorkflowTests.RepositoryFile("DynaDocs.csproj");
        var project = XDocument.Load(source);
        project.Root!.Elements("ItemGroup")
            .Where(group => !group.Elements("EmbeddedResource").Any(item =>
                ((string?)item.Attribute("Include"))?.StartsWith("viewer\\", StringComparison.Ordinal) == true))
            .Remove();
        project.Root.Elements("Import")
            .Where(import => (string?)import.Attribute("Project") == "Resources/Statechart/payload.items.props").Remove();
        foreach (var import in project.Root.Elements("Import"))
            import.SetAttributeValue("Project", Path.Combine(Path.GetDirectoryName(source)!, (string)import.Attribute("Project")!));
        project.Root.Elements("Target").Where(target => (string?)target.Attribute("Name") == "ValidateStatechartPayload").Remove();
        project.Descendants("PackageReadmeFile").Remove();
        project.Descendants("PublishAot").Single().Value = "false";
        project.Save(Path.Combine(_directory, "DynaDocs.csproj"));
        File.WriteAllText(Path.Combine(_directory, "Program.cs"), "System.Console.WriteLine(\"pack fixture\");");
        Succeeds("restore", "--source", _directory, "-p:NuGetAudit=false");
    }

    public void Dispose() => TestDirectory.Delete(_directory);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pack_WithoutViewer_FailsWithBuildInstructions(bool emptyDist)
    {
        if (emptyDist)
            Directory.CreateDirectory(Path.Combine(_directory, "viewer", "dist"));

        Succeeds("build", "--no-restore");
        var (exitCode, output) = Run("pack", "--no-restore");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("pnpm -C viewer install --frozen-lockfile", output);
        Assert.Contains("pnpm -C viewer run build", output);
        Assert.Empty(Directory.GetFiles(_directory, "*.nupkg", SearchOption.AllDirectories));
    }

    [Fact]
    public void Pack_NoBuild_RejectsAViewerlessAssemblyEvenWhenDistNowExists()
    {
        Succeeds("build", "--no-restore");
        WriteViewer();

        var (exitCode, output) = Run("pack", "--no-restore", "--no-build");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("rebuild", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without --no-build", output);
        Assert.Empty(Directory.GetFiles(_directory, "*.nupkg", SearchOption.AllDirectories));
    }

    [Fact]
    public void Pack_NoBuild_RejectsAViewerlessPublishOutputEvenWhenBuildOutputHasTheViewer()
    {
        Succeeds("build", "--no-restore");
        var output = Path.Combine(_directory, "bin", "Release", "net10.0");
        var publish = Directory.CreateDirectory(Path.Combine(output, "publish")).FullName;
        File.Copy(Path.Combine(output, "dydo.dll"), Path.Combine(publish, "dydo.dll"));
        WriteViewer();
        Succeeds("build", "--no-restore");

        var (exitCode, message) = Run("pack", "--no-restore", "--no-build", "-p:IsPublishable=false");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("DYDO002", message);
        Assert.Empty(Directory.GetFiles(_directory, "*.nupkg", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pack_WithViewer_EmbedsTheBundleInTheTool(bool noBuild)
    {
        WriteViewer();
        if (noBuild)
            Succeeds("build", "--no-restore");

        var arguments = noBuild ? new[] { "pack", "--no-restore", "--no-build" } : ["pack", "--no-restore"];
        Succeeds(arguments);

        using var package = ZipFile.OpenRead(Assert.Single(Directory.GetFiles(_directory, "*.nupkg", SearchOption.AllDirectories)));
        using var assembly = new MemoryStream();
        package.GetEntry("tools/net10.0/any/dydo.dll")!.Open().CopyTo(assembly);
        assembly.Position = 0;
        using var reader = new PEReader(assembly);
        var metadata = reader.GetMetadataReader();
        var resources = metadata.ManifestResources.Select(handle => metadata.GetString(metadata.GetManifestResource(handle).Name));
        Assert.Contains("viewer/dist/index.html", resources);
        Assert.Contains("viewer/dist/assets/app.js", resources);
    }

    private void WriteViewer()
    {
        var dist = Path.Combine(_directory, "viewer", "dist");
        Directory.CreateDirectory(Path.Combine(dist, "assets"));
        File.WriteAllText(Path.Combine(dist, "index.html"), "<script src=\"/assets/app.js\"></script>");
        File.WriteAllText(Path.Combine(dist, "assets", "app.js"), "console.log('viewer');");
    }

    private void Succeeds(params string[] arguments)
    {
        var (exitCode, output) = Run(arguments);
        Assert.True(exitCode == 0, output);
    }

    private (int ExitCode, string Output) Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.ArgumentList.Add("-p:Configuration=Release");
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["UseSharedCompilation"] = "false";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
