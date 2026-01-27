namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class IndexCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path", () => null, "Path to docs folder");

        var command = new Command("index", "Regenerate Index.md from doc structure")
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

            Console.WriteLine("Generating index.md...");
            Console.WriteLine();

            var parser = new MarkdownParser();
            var scanner = new DocScanner(parser);
            var generator = new IndexGenerator();

            var docs = scanner.ScanDirectory(basePath);
            var content = generator.Generate(docs, basePath);

            var indexPath = Path.Combine(basePath, "index.md");
            File.WriteAllText(indexPath, content);

            var hubFolders = new[] { "understand", "guides", "reference", "project" };
            Console.WriteLine("Scanned top-level hubs:");
            foreach (var folder in hubFolders)
            {
                var docsInHub = docs.Count(d => d.RelativePath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
                var status = docsInHub > 0 ? $"({docsInHub} docs)" : "(not found)";
                Console.WriteLine($"  - {folder}/_index.md {status}");
            }

            Console.WriteLine();
            ConsoleOutput.WriteSuccess($"Generated {indexPath}");

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
            if (Directory.Exists(path))
                return Path.GetFullPath(path);
            return null;
        }
        return PathUtils.FindDocsFolder(Environment.CurrentDirectory);
    }
}
