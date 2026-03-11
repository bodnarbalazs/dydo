namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;

public static class IssueCommand
{
    public static Command Create()
    {
        var command = new Command("issue", "Manage issues");

        command.Subcommands.Add(CreateCreateCommand());
        command.Subcommands.Add(CreateListCommand());
        command.Subcommands.Add(CreateResolveCommand());

        return command;
    }

    private static Command CreateCreateCommand()
    {
        var titleOption = new Option<string>("--title")
        {
            Description = "Issue title",
            Required = true
        };

        var areaOption = new Option<string>("--area")
        {
            Description = "Issue area (e.g., backend, frontend, general)",
            Required = true
        };

        var severityOption = new Option<string>("--severity")
        {
            Description = "Issue severity (low, medium, high, critical)",
            Required = true
        };

        var foundByOption = new Option<string?>("--found-by")
        {
            Description = "How the issue was found (inquisition, review, manual). Defaults to manual."
        };

        var command = new Command("create", "Create a new issue");
        command.Options.Add(titleOption);
        command.Options.Add(areaOption);
        command.Options.Add(severityOption);
        command.Options.Add(foundByOption);

        command.SetAction(parseResult =>
        {
            var title = parseResult.GetValue(titleOption)!;
            var area = parseResult.GetValue(areaOption)!;
            var severity = parseResult.GetValue(severityOption)!;
            var foundBy = parseResult.GetValue(foundByOption);
            return IssueCreateHandler.Execute(title, area, severity, foundBy);
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var areaOption = new Option<string?>("--area")
        {
            Description = "Filter by area"
        };

        var statusOption = new Option<string?>("--status")
        {
            Description = "Filter by status"
        };

        var allOption = new Option<bool>("--all")
        {
            Description = "Include resolved issues"
        };

        var command = new Command("list", "List issues");
        command.Options.Add(areaOption);
        command.Options.Add(statusOption);
        command.Options.Add(allOption);

        command.SetAction(parseResult =>
        {
            var area = parseResult.GetValue(areaOption);
            var status = parseResult.GetValue(statusOption);
            var all = parseResult.GetValue(allOption);
            return IssueListHandler.Execute(area, status, all);
        });

        return command;
    }

    private static Command CreateResolveCommand()
    {
        var idArgument = new Argument<int>("id")
        {
            Description = "Issue ID to resolve"
        };

        var summaryOption = new Option<string>("--summary")
        {
            Description = "Resolution summary (required)",
            Required = true
        };

        var command = new Command("resolve", "Resolve an issue");
        command.Arguments.Add(idArgument);
        command.Options.Add(summaryOption);

        command.SetAction(parseResult =>
        {
            var id = parseResult.GetValue(idArgument);
            var summary = parseResult.GetValue(summaryOption)!;
            return IssueResolveHandler.Execute(id, summary);
        });

        return command;
    }

    internal static string GetIssuesPath()
    {
        var configService = new ConfigService();
        return configService.GetIssuesPath();
    }
}
