namespace DynaDocs.Commands;

using System.CommandLine;
using System.CommandLine.Invocation;
using DynaDocs.Models;
using DynaDocs.Rules;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class CheckCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path", () => null, "Path to docs folder or file to check");

        var command = new Command("check", "Validate documentation and report violations")
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
                ConsoleOutput.WriteError("Could not find docs folder. Specify a path or ensure a 'docs' folder with index.md exists.");
                return ExitCodes.ToolError;
            }

            Console.WriteLine($"Checking {basePath}...");
            Console.WriteLine();

            var parser = new MarkdownParser();
            var scanner = new DocScanner(parser);
            var linkResolver = new LinkResolver();

            var docs = scanner.ScanDirectory(basePath);
            var folders = scanner.GetAllFolders(basePath);

            var rules = CreateRules(linkResolver);
            var result = new ValidationResult { TotalFilesChecked = docs.Count };

            foreach (var doc in docs)
            {
                foreach (var rule in rules)
                {
                    result.AddRange(rule.Validate(doc, docs, basePath));
                }
            }

            foreach (var folder in folders)
            {
                foreach (var rule in rules)
                {
                    result.AddRange(rule.ValidateFolder(folder, docs, basePath));
                }
            }

            ConsoleOutput.WriteViolations(result);

            return result.HasErrors ? ExitCodes.ValidationErrors : ExitCodes.Success;
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

    private static List<IRule> CreateRules(ILinkResolver linkResolver)
    {
        return
        [
            new NamingRule(),
            new RelativeLinksRule(),
            new FrontmatterRule(),
            new SummaryRule(),
            new BrokenLinksRule(linkResolver),
            new HubFilesRule(),
            new OrphanDocsRule()
        ];
    }
}
