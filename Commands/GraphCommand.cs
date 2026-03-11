namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class GraphCommand
{
    public static Command Create()
    {
        var fileArgument = new Argument<string>("file")
        {
            Description = "The doc file to analyze"
        };

        var incomingOption = new Option<bool>("--incoming")
        {
            Description = "Show docs that link TO this file (backlinks)"
        };

        var degreeOption = new Option<int>("--degree")
        {
            DefaultValueFactory = _ => 1,
            Description = "Show docs within n link-hops (default: 1)"
        };

        var command = new Command("graph", "Show graph connections for a documentation file");
        command.Arguments.Add(fileArgument);
        command.Options.Add(incomingOption);
        command.Options.Add(degreeOption);

        command.SetAction(parseResult =>
        {
            var file = parseResult.GetValue(fileArgument)!;
            var incoming = parseResult.GetValue(incomingOption);
            var degree = parseResult.GetValue(degreeOption);
            return Execute(file, incoming, degree);
        });

        command.Subcommands.Add(CreateStatsCommand());

        return command;
    }

    private static int Execute(string file, bool incoming, int degree)
    {
        try
        {
            var basePath = PathUtils.FindDocsFolder(Environment.CurrentDirectory);
            if (basePath == null)
            {
                ConsoleOutput.WriteError("Could not find docs folder. Ensure a 'docs' folder with index.md exists.");
                return ExitCodes.ToolError;
            }

            var parser = new MarkdownParser();
            var scanner = new DocScanner(parser);
            var docs = scanner.ScanDirectory(basePath)
                .Where(d => !PathUtils.NormalizePath(d.RelativePath)
                    .StartsWith("agents/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var graph = new DocGraph();
            graph.Build(docs, basePath);

            var targetPath = ResolveTargetFile(file, basePath, docs);
            if (targetPath == null)
            {
                ConsoleOutput.WriteError($"File not found: {file}");
                return ExitCodes.ToolError;
            }

            if (!graph.HasDoc(targetPath))
            {
                ConsoleOutput.WriteError($"File not in docs: {file}");
                return ExitCodes.ToolError;
            }

            var hasOutput = false;

            if (incoming)
            {
                GraphDisplayHandler.ShowIncoming(graph, targetPath, file);
                hasOutput = true;
            }

            if (degree > 0 && !incoming)
            {
                GraphDisplayHandler.ShowDegree(graph, targetPath, file, degree);
                hasOutput = true;
            }

            if (incoming && degree > 0)
            {
                GraphDisplayHandler.ShowCombined(graph, targetPath, degree);
            }

            if (!hasOutput)
            {
                Console.WriteLine("Use --incoming to see backlinks or --degree N to see connected docs");
            }

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }

    private static string? ResolveTargetFile(string file, string basePath, List<Models.DocFile> docs)
    {
        var normalizedInput = PathUtils.NormalizeForKey(file);

        if (!normalizedInput.EndsWith(".md"))
            normalizedInput += ".md";

        foreach (var doc in docs)
        {
            var normalizedDoc = PathUtils.NormalizeForKey(doc.RelativePath);

            if (normalizedDoc == normalizedInput)
                return doc.RelativePath;

            if (normalizedDoc.EndsWith(normalizedInput))
                return doc.RelativePath;

            if (Path.GetFileName(normalizedDoc) == Path.GetFileName(normalizedInput))
                return doc.RelativePath;
        }

        return null;
    }

    private static Command CreateStatsCommand()
    {
        var topOption = new Option<int>("--top")
        {
            DefaultValueFactory = _ => 100,
            Description = "Number of documents to show (default: 100)"
        };

        var statsCommand = new Command("stats", "Show document link statistics ranked by incoming links");
        statsCommand.Options.Add(topOption);

        statsCommand.SetAction(parseResult =>
        {
            var top = parseResult.GetValue(topOption);
            return ExecuteStats(top);
        });

        return statsCommand;
    }

    private static int ExecuteStats(int top)
    {
        try
        {
            var basePath = PathUtils.FindDocsFolder(Environment.CurrentDirectory);
            if (basePath == null)
            {
                ConsoleOutput.WriteError("Could not find docs folder. Ensure a 'docs' folder with index.md exists.");
                return ExitCodes.ToolError;
            }

            var parser = new MarkdownParser();
            var scanner = new DocScanner(parser);
            var docs = scanner.ScanDirectory(basePath)
                .Where(d => !PathUtils.NormalizePath(d.RelativePath)
                    .StartsWith("agents/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var graph = new DocGraph();
            graph.Build(docs, basePath);

            GraphDisplayHandler.ShowStats(graph, top);

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Error: {ex.Message}");
            return ExitCodes.ToolError;
        }
    }
}
