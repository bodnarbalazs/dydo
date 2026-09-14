namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class FixCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path")
        {
            DefaultValueFactory = _ => null,
            Description = "Path to docs folder or file to fix"
        };

        var command = new Command("fix", "Auto-fix documentation issues where possible");
        command.Arguments.Add(pathArgument);

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArgument);
            return Execute(path);
        });

        return command;
    }

    private static int Execute(string? path)
    {
        try
        {
            var scope = ResolveScope(path);
            if (scope == null)
            {
                ConsoleOutput.WriteError("Could not find docs folder.");
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Fixing {scope.FilePath ?? scope.CorpusRoot}...");
            Console.WriteLine();

            var configService = new ConfigService();
            var scanner = new DocScanner(new MarkdownParser(), configService);
            var resolutionCorpus = scanner.ScanDirectory(scope.CorpusRoot);
            var docs = SelectDocs(scope, resolutionCorpus);
            if (docs == null)
                return ExitCodes.ToolError;

            Console.WriteLine("FIXED:");

            var configFixCount = RestoreScanExcludeInvariants(configService, scope.CorpusRoot);
            var renamedFilePath = scope.FilePath == null ? null : GetKebabDestination(scope.FilePath);
            var (renamed, nameConflicts) = FixFileHandler.FixNaming(docs);
            var fixedCount = configFixCount + renamed;

            if (renamedFilePath != null && renamed == 1)
                scope = scope with { FilePath = renamedFilePath };

            resolutionCorpus = scanner.ScanDirectory(scope.CorpusRoot);
            docs = SelectDocs(scope, resolutionCorpus);
            if (docs == null)
                return ExitCodes.ToolError;

            var (linksConverted, manualFixes) = FixFileHandler.FixWikilinks(docs, resolutionCorpus);
            if (linksConverted > 0)
            {
                ConsoleOutput.WriteSuccess($"  ✓ Converted {linksConverted} wikilinks to relative paths");
                fixedCount += linksConverted;
            }

            if (scope.FilePath == null)
            {
                fixedCount += FixHubHandler.RegenerateHubs(scope.CorpusRoot, scanner, docs);
                fixedCount += FixHubHandler.CreateMissingMetaFiles(scope.CorpusRoot, scanner, docs);
            }

            resolutionCorpus = scanner.ScanDirectory(scope.CorpusRoot);
            docs = SelectDocs(scope, resolutionCorpus);
            if (docs == null)
                return ExitCodes.ToolError;

            var manualFixNeeded = manualFixes;
            manualFixNeeded.AddRange(nameConflicts);
            manualFixNeeded.AddRange(FixFileHandler.FindManualFixes(docs));

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

            return nameConflicts.Count > 0 ? ExitCodes.ValidationErrors : ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static FixScope? ResolveScope(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            if (Directory.Exists(path))
                return new FixScope(Path.GetFullPath(path), null);
            if (File.Exists(path))
            {
                var filePath = Path.GetFullPath(path);
                return new FixScope(FindContainingCorpusRoot(filePath), filePath);
            }
            return null;
        }

        var docsPath = PathUtils.FindDocsFolder(Environment.CurrentDirectory);
        return docsPath == null ? null : new FixScope(Path.GetFullPath(docsPath), null);
    }

    private static string FindContainingCorpusRoot(string filePath)
    {
        var parent = new FileInfo(filePath).Directory!;
        var fallback = parent.FullName;

        for (var cursor = parent; cursor != null; cursor = cursor.Parent)
        {
            var candidate = PathUtils.FindDocsFolder(cursor.FullName);
            if (candidate != null && CheckDocValidator.IsUnderScope(filePath, candidate))
                return candidate;
        }

        return fallback;
    }

    private static List<DocFile>? SelectDocs(FixScope scope, List<DocFile> resolutionCorpus)
    {
        if (scope.FilePath == null)
            return resolutionCorpus;

        var selected = resolutionCorpus.Where(doc =>
            PathUtils.NormalizePath(Path.GetFullPath(doc.FilePath)).Equals(
                PathUtils.NormalizePath(scope.FilePath), StringComparison.OrdinalIgnoreCase)).ToList();

        if (selected.Count == 1)
            return selected;

        ConsoleOutput.WriteError($"Could not find exactly one selected file '{scope.FilePath}' in corpus '{scope.CorpusRoot}'.");
        return null;
    }

    private static string GetKebabDestination(string filePath)
    {
        var fileName = PathUtils.ToKebabCase(Path.GetFileNameWithoutExtension(filePath)) + ".md";
        return Path.Combine(Path.GetDirectoryName(filePath)!, fileName);
    }

    private static int RestoreScanExcludeInvariants(IConfigService configService, string startPath)
    {
        var configPath = configService.FindConfigFile(startPath);
        if (configPath == null)
            return 0;

        var config = configService.LoadConfig(startPath);
        if (config == null)
            return 0;

        var added = ConfigFactory.EnsureDefaultScanExclude(config);
        if (added == 0)
            return 0;

        configService.SaveConfig(config, configPath);
        ConsoleOutput.WriteSuccess($"  ✓ Restored {added} scanExclude invariant(s) in dydo.json");
        return added;
    }

    private sealed record FixScope(string CorpusRoot, string? FilePath);
}
