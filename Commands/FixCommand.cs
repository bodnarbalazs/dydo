namespace DynaDocs.Commands;

using System.CommandLine;
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
            var basePath = ResolvePath(path);
            if (basePath == null)
            {
                ConsoleOutput.WriteError("Could not find docs folder.");
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Fixing {basePath}...");
            Console.WriteLine();

            var parser = new MarkdownParser();
            var scanner = new DocScanner(parser);
            var docs = scanner.ScanDirectory(basePath);

            // Fix naming issues
            Console.WriteLine("FIXED:");
            var (fixedCount, nameConflicts) = FixFileHandler.FixNaming(docs);

            // Re-scan after renames
            docs = scanner.ScanDirectory(basePath);

            // Fix wikilinks
            var (linksConverted, manualFixes) = FixFileHandler.FixWikilinks(docs);
            if (linksConverted > 0)
            {
                ConsoleOutput.WriteSuccess($"  ✓ Converted {linksConverted} wikilinks to relative paths");
                fixedCount += linksConverted;
            }

            // Regenerate hub files
            fixedCount += FixHubHandler.RegenerateHubs(basePath, scanner, docs);

            // Create missing meta files
            fixedCount += FixHubHandler.CreateMissingMetaFiles(basePath, scanner, docs);

            // Report manual fixes needed
            docs = scanner.ScanDirectory(basePath);
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
}
