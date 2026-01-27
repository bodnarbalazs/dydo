namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class FixCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path", () => null, "Path to docs folder or file to fix");

        var command = new Command("fix", "Auto-fix documentation issues where possible")
        {
            pathArgument
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            var path = ctx.ParseResult.GetValueForArgument(pathArgument);
            ctx.ExitCode = Execute(path);
        });

        return command;
    }

    private static int Execute(string? path)
    {
        try
        {
            var basePath = ResolvePath(path);
            if (basePath == null)
            {
                ConsoleOutput.WriteError("Could not find docs folder.");
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Fixing {basePath}...");
            Console.WriteLine();

            var fixedCount = 0;
            var manualFixNeeded = new List<string>();

            var parser = new MarkdownParser();
            var scanner = new DocScanner(parser);
            var docs = scanner.ScanDirectory(basePath);

            // Fix naming issues
            Console.WriteLine("FIXED:");
            foreach (var doc in docs.ToList())
            {
                if (!PathUtils.IsKebabCase(doc.FileName))
                {
                    var newName = PathUtils.ToKebabCase(Path.GetFileNameWithoutExtension(doc.FileName)) + ".md";
                    var newPath = Path.Combine(Path.GetDirectoryName(doc.FilePath)!, newName);

                    File.Move(doc.FilePath, newPath);
                    ConsoleOutput.WriteSuccess($"  ✓ Renamed {doc.FileName} -> {newName}");
                    fixedCount++;
                }
            }

            // Re-scan after renames
            docs = scanner.ScanDirectory(basePath);

            // Fix wikilinks
            var linkResolver = new LinkResolver();
            var linksConverted = 0;

            foreach (var doc in docs)
            {
                var wikilinks = doc.Links.Where(l => l.Type == LinkType.Wikilink).ToList();
                if (wikilinks.Count == 0) continue;

                var content = doc.Content;

                foreach (var link in wikilinks)
                {
                    var resolvedPath = linkResolver.FindFileByName(link.Target + ".md", docs);
                    if (resolvedPath != null)
                    {
                        var relativePath = PathUtils.GetRelativePath(doc.RelativePath, resolvedPath);
                        var newLink = $"[{link.DisplayText}]({relativePath})";
                        content = content.Replace(link.RawText, newLink);
                        linksConverted++;
                    }
                    else
                    {
                        manualFixNeeded.Add($"{doc.RelativePath} - Ambiguous wikilink: {link.RawText}");
                    }
                }

                if (content != doc.Content)
                {
                    File.WriteAllText(doc.FilePath, content);
                }
            }

            if (linksConverted > 0)
            {
                ConsoleOutput.WriteSuccess($"  ✓ Converted {linksConverted} wikilinks to relative paths");
                fixedCount += linksConverted;
            }

            // Create missing hub files
            var folders = scanner.GetAllFolders(basePath);
            foreach (var folder in folders)
            {
                var relativeFolderPath = PathUtils.NormalizePath(Path.GetRelativePath(basePath, folder));

                if (relativeFolderPath == ".") continue;

                var docsInFolder = docs.Where(d =>
                {
                    var docDir = Path.GetDirectoryName(d.RelativePath) ?? "";
                    docDir = PathUtils.NormalizePath(docDir);
                    return docDir.Equals(relativeFolderPath, StringComparison.OrdinalIgnoreCase);
                }).ToList();

                if (docsInFolder.Count > 0 && !docsInFolder.Any(d => d.FileName == "_index.md"))
                {
                    var indexPath = Path.Combine(folder, "_index.md");
                    var folderName = Path.GetFileName(folder);
                    var content = GenerateSkeletonHub(folderName);
                    File.WriteAllText(indexPath, content);
                    ConsoleOutput.WriteSuccess($"  ✓ Created {Path.GetRelativePath(basePath, indexPath)} (skeleton)");
                    fixedCount++;
                }
            }

            // Report manual fixes needed
            docs = scanner.ScanDirectory(basePath);
            foreach (var doc in docs)
            {
                if (!doc.HasFrontmatter)
                    manualFixNeeded.Add($"{doc.RelativePath} - Add frontmatter");
                else if (string.IsNullOrEmpty(doc.SummaryParagraph))
                    manualFixNeeded.Add($"{doc.RelativePath} - Add summary paragraph");
            }

            Console.WriteLine();
            Console.WriteLine($"Fixed {fixedCount} issues automatically.");

            if (manualFixNeeded.Count > 0)
            {
                Console.WriteLine();
                ConsoleOutput.WriteWarning("NEEDS MANUAL FIX:");
                foreach (var item in manualFixNeeded.Distinct())
                {
                    Console.WriteLine($"  ✗ {item}");
                }
                Console.WriteLine();
                Console.WriteLine($"{manualFixNeeded.Distinct().Count()} issues require manual attention.");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static string? ResolvePath(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            if (File.Exists(path) || Directory.Exists(path))
                return Path.GetFullPath(path);
            return null;
        }
        return PathUtils.FindDocsFolder(Environment.CurrentDirectory);
    }

    private static string GenerateSkeletonHub(string folderName)
    {
        var title = char.ToUpper(folderName[0]) + folderName[1..].Replace("-", " ");
        return $"""
            ---
            area: general
            type: hub
            ---

            # {title}

            Overview of documents in this section.

            ## Contents

            *TODO: Add links to documents in this folder*
            """;
    }
}
