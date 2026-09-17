using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.Json;

namespace GateMetrics;

public static class ProjectMetrics
{
    public static async Task<ProjectFacts> CollectAsync(string projectPath, string root)
    {
        root = Path.GetFullPath(root);
        using var workspace = MSBuildWorkspace.Create();
        var failures = new List<string>();
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                failures.Add(args.Diagnostic.Message);
        });
        var project = await workspace.OpenProjectAsync(Path.GetFullPath(projectPath));
        var compilation = await project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Missing C# compilation; check pinned workspace language services.");
        failures.AddRange(compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error)
            .Select(item => item.ToString()));
        if (failures.Count != 0)
            throw new InvalidOperationException("Incomplete C# compilation: " + string.Join("; ", failures));
        return await ProjectFactsAsync(project, compilation, root);
    }

    private static async Task<ProjectFacts> ProjectFactsAsync(Project project, Compilation compilation, string root)
    {
        var files = new List<FileFacts>();
        var trees = new List<SyntaxTree>();
        var generated = new List<string>();
        foreach (var document in project.Documents)
        {
            var identity = SourceIdentity(root, project.FilePath!, document.FilePath);
            var path = identity.Path;
            if (identity.Origin != "maintained")
            {
                generated.Add(path);
                continue;
            }
            var tree = await document.GetSyntaxTreeAsync()
                ?? throw new InvalidOperationException($"Missing C# source tree: {path}");
            trees.Add(tree);
            files.Add(new FileFacts(path, SourceMetrics.Measure(tree)));
        }
        if (files.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
            throw new InvalidOperationException("Duplicate evaluated C# source path");
        generated.AddRange(compilation.SyntaxTrees.Where(tree => !trees.Contains(tree))
            .Select(tree => SourceIdentity(root, project.FilePath!, tree.FilePath).Path)
            .Except(generated, StringComparer.Ordinal));
        return new ProjectFacts(true, RelativePath(root, project.FilePath), project.OutputFilePath,
            files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray(),
            NamespaceDependencies.Collect(compilation, trees), generated, SourceBehavior.Collect(compilation, trees, root));
    }

    private static string RelativePath(string root, string? path)
    {
        if (path == null)
            throw new InvalidOperationException("Missing evaluated C# source path");
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new InvalidOperationException($"C# source outside inventory root: {path}");
        return relative;
    }

    internal static (string Path, string Origin) SourceIdentity(
        string root, string projectPath, string? path, string description = "C# source")
    {
        if (path == null || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("Missing evaluated C# source path");
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            return (relative, relative.Split('/').Contains("obj", StringComparer.OrdinalIgnoreCase)
                ? "generated" : "maintained");

        var assetsPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "project.assets.json");
        if (File.Exists(assetsPath))
        {
            using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
            var libraries = assets.RootElement.GetProperty("libraries").EnumerateObject()
                .Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in assets.RootElement.GetProperty("packageFolders").EnumerateObject())
            {
                var packageRoot = Path.GetFullPath(folder.Name);
                var packageRelative = Path.GetRelativePath(packageRoot, path).Replace('\\', '/');
                if (packageRelative == ".." || packageRelative.StartsWith("../", StringComparison.Ordinal) ||
                    Path.IsPathRooted(packageRelative))
                    continue;
                var parts = packageRelative.Split('/');
                if (parts.Length >= 3 && libraries.Contains($"{parts[0]}/{parts[1]}"))
                    return ($"nuget:{parts[0].ToLowerInvariant()}/{string.Join('/', parts.Skip(1))}", "package");
            }
        }
        throw new InvalidOperationException($"{description} outside inventory root: {path}");
    }
}
