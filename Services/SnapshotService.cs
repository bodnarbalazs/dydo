namespace DynaDocs.Services;

using System.Diagnostics;
using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Service for capturing project state snapshots at session claim time.
/// Uses git to get tracked files and DocScanner/DocGraph for doc links.
/// </summary>
public class SnapshotService : ISnapshotService
{
    private readonly IConfigService _configService;

    public SnapshotService(IConfigService? configService = null)
    {
        _configService = configService ?? new ConfigService();
    }

    /// <summary>
    /// Captures a complete snapshot of the project state.
    /// </summary>
    public ProjectSnapshot CaptureSnapshot(string basePath)
    {
        var projectRoot = _configService.GetProjectRoot(basePath) ?? basePath;

        var snapshot = new ProjectSnapshot
        {
            GitCommit = GetFullGitHead(projectRoot) ?? "unknown"
        };

        // Get all git-tracked files
        snapshot.Files = GetGitTrackedFiles(projectRoot);

        // Derive folders from file paths
        snapshot.Folders = DeriveFolders(snapshot.Files);

        // Extract doc links from markdown files in the dydo folder
        var dydoRoot = _configService.GetDydoRoot(basePath);
        if (Directory.Exists(dydoRoot))
        {
            snapshot.DocLinks = ExtractDocLinks(dydoRoot, projectRoot);
        }

        return snapshot;
    }

    /// <summary>
    /// Gets the full git HEAD commit hash.
    /// </summary>
    private static string? GetFullGitHead(string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all files tracked by git using 'git ls-files'.
    /// </summary>
    private static List<string> GetGitTrackedFiles(string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files --full-name",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return [];

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10000);

            if (process.ExitCode != 0)
                return [];

            return output
                .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(PathUtils.NormalizePath)
                .OrderBy(f => f)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Derives all folder paths from the list of file paths.
    /// </summary>
    private static List<string> DeriveFolders(List<string> files)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var parts = file.Split('/');
            var current = "";

            // Build all parent folder paths
            for (int i = 0; i < parts.Length - 1; i++)
            {
                current = string.IsNullOrEmpty(current)
                    ? parts[i]
                    : current + "/" + parts[i];
                folders.Add(current);
            }
        }

        return folders.OrderBy(f => f).ToList();
    }

    /// <summary>
    /// Extracts doc-to-doc links from all markdown files in the dydo folder.
    /// </summary>
    private static Dictionary<string, List<string>> ExtractDocLinks(string dydoRoot, string projectRoot)
    {
        var links = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parser = new MarkdownParser();
            var scanner = new DocScanner(parser);
            var graph = new DocGraph();

            // Scan all docs in dydo folder
            var docs = scanner.ScanDirectory(dydoRoot);

            // Build the graph (this resolves all links)
            graph.Build(docs, dydoRoot);

            // Extract outgoing links for each doc
            foreach (var doc in docs)
            {
                var sourcePath = PathUtils.NormalizeForKey(doc.RelativePath);

                var outgoingLinks = doc.Links
                    .Where(l => l.Type != LinkType.External)
                    .Select(l => ResolveLink(doc, l))
                    .Where(t => t != null && graph.HasDoc(t))
                    .Select(t => PathUtils.NormalizeForKey(t!))
                    .Distinct()
                    .ToList();

                if (outgoingLinks.Count > 0)
                {
                    // Prefix with dydo folder relative to project root
                    var dydoRelative = GetDydoRelativePath(dydoRoot, projectRoot);
                    var fullSourcePath = string.IsNullOrEmpty(dydoRelative)
                        ? sourcePath
                        : dydoRelative + "/" + sourcePath;

                    var fullTargets = outgoingLinks
                        .Select(t => string.IsNullOrEmpty(dydoRelative) ? t : dydoRelative + "/" + t)
                        .ToList();

                    links[fullSourcePath] = fullTargets;
                }
            }
        }
        catch
        {
            // Silently fail - doc link extraction is not critical
        }

        return links;
    }

    /// <summary>
    /// Gets the dydo folder path relative to project root.
    /// </summary>
    private static string GetDydoRelativePath(string dydoRoot, string projectRoot)
    {
        try
        {
            var relative = Path.GetRelativePath(projectRoot, dydoRoot);
            return PathUtils.NormalizePath(relative);
        }
        catch
        {
            return "dydo";
        }
    }

    /// <summary>
    /// Resolves a link target to a normalized path.
    /// </summary>
    private static string? ResolveLink(DocFile sourceDoc, LinkInfo link)
    {
        var target = link.Target;

        // Remove anchor if present
        var anchorIndex = target.IndexOf('#');
        if (anchorIndex >= 0)
            target = target[..anchorIndex];

        if (string.IsNullOrEmpty(target))
            return null;

        // Get the directory of the source doc
        var sourceDir = Path.GetDirectoryName(sourceDoc.RelativePath) ?? "";

        // Resolve relative path
        string resolved;
        if (target.StartsWith("./"))
            resolved = Path.Combine(sourceDir, target[2..]);
        else if (target.StartsWith("../"))
            resolved = Path.Combine(sourceDir, target);
        else
            resolved = Path.Combine(sourceDir, target);

        // Normalize (handle .. segments)
        var parts = PathUtils.NormalizePath(resolved)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var normalized = new List<string>();

        foreach (var part in parts)
        {
            if (part == "..")
            {
                if (normalized.Count > 0)
                    normalized.RemoveAt(normalized.Count - 1);
            }
            else if (part != ".")
            {
                normalized.Add(part);
            }
        }

        return string.Join("/", normalized);
    }
}
