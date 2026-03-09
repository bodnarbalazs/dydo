namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Models;
using DynaDocs.Services;

public static class CompleteCommand
{
    private static readonly string[] TopLevelCommands =
    [
        "check", "fix", "index", "init", "graph", "agent", "guard",
        "dispatch", "inbox", "task", "review", "clean", "workspace",
        "whoami", "audit", "version", "help", "completions"
    ];

    private static readonly string[] TaskSubcommands =
        ["approve", "create", "list", "ready-for-review", "reject"];

    private static readonly string[] AgentSubcommands =
        ["claim", "release", "status", "list", "role", "new", "rename", "remove", "reassign"];

    private static readonly string[] ReviewSubcommands = ["complete"];

    private static readonly string[] InboxSubcommands = ["list", "show", "clear"];

    private static readonly string[] WorkspaceSubcommands = ["init", "check"];

    private static readonly string[] GraphSubcommands = ["stats"];

    private static readonly string[] Roles =
        ["code-writer", "reviewer", "co-thinker", "docs-writer", "planner", "test-writer"];

    private static readonly string[] ReviewStatuses = ["pass", "fail"];

    private static readonly string[] GuardActions = ["edit", "write", "delete", "read"];

    private static readonly string[] Integrations = ["claude", "none"];

    private static readonly string[] Shells = ["bash", "zsh", "powershell"];

    public static Command Create()
    {
        var positionArgument = new Argument<int>("position")
        {
            Description = "0-based word index being completed"
        };

        var wordsArgument = new Argument<string[]>("words")
        {
            Description = "Full command line tokens",
            Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command("_complete", "Generate shell completions");
        command.Arguments.Add(positionArgument);
        command.Arguments.Add(wordsArgument);

        command.SetAction(parseResult =>
        {
            var position = parseResult.GetValue(positionArgument);
            var words = parseResult.GetValue(wordsArgument) ?? [];
            return Execute(position, words);
        });

        return command;
    }

    private static int Execute(int position, string[] words)
    {
        try
        {
            var completions = GetCompletions(position, words);
            foreach (var c in completions)
                Console.WriteLine(c);
        }
        catch
        {
            // Silent on errors — always exit 0
        }

        return 0;
    }

    public static IEnumerable<string> GetCompletions(int position, string[] words)
    {
        // Check if the previous word is an option that takes a value
        if (position >= 1 && position <= words.Length)
        {
            var prevWord = words[position - 1];
            var optionCompletions = GetOptionValueCompletions(prevWord);
            if (optionCompletions != null)
                return optionCompletions;
        }

        // Walk words to determine command context
        // words[0] is "dydo", subcommands start at words[1]
        if (position <= 1)
            return TopLevelCommands;

        if (words.Length < 2)
            return TopLevelCommands;

        var command = words[1].ToLowerInvariant();

        return command switch
        {
            "task" => GetTaskCompletions(position, words),
            "agent" => GetAgentCompletions(position, words),
            "review" => GetReviewCompletions(position, words),
            "dispatch" => [], // dispatch uses options, handled by option-value detection
            "clean" => position == 2 ? GetAgentNames() : [],
            "init" => position == 2 ? Integrations : [],
            "completions" => position == 2 ? Shells : [],
            "inbox" => position == 2 ? InboxSubcommands : [],
            "workspace" => position == 2 ? WorkspaceSubcommands : [],
            "graph" => position == 2 ? GraphSubcommands : [],
            _ => []
        };
    }

    private static IEnumerable<string>? GetOptionValueCompletions(string option)
    {
        return option switch
        {
            "--role" => Roles,
            "--task" => GetTaskNames(),
            "--area" => Frontmatter.ValidAreas,
            "--status" => ReviewStatuses,
            "--action" => GuardActions,
            "--to" => GetAgentNames(),
            _ => null
        };
    }

    private static IEnumerable<string> GetTaskCompletions(int position, string[] words)
    {
        if (position == 2)
            return TaskSubcommands;

        if (words.Length < 3)
            return [];

        var subcommand = words[2].ToLowerInvariant();

        if (position == 3)
        {
            return subcommand switch
            {
                "approve" or "reject" or "ready-for-review" => GetTaskNames(),
                _ => []
            };
        }

        return [];
    }

    private static IEnumerable<string> GetAgentCompletions(int position, string[] words)
    {
        if (position == 2)
            return AgentSubcommands;

        if (words.Length < 3)
            return [];

        var subcommand = words[2].ToLowerInvariant();

        if (position == 3)
        {
            return subcommand switch
            {
                "claim" => GetAgentNames().Prepend("auto"),
                "role" => Roles,
                "status" or "rename" or "remove" or "reassign" => GetAgentNames(),
                _ => []
            };
        }

        return [];
    }

    private static IEnumerable<string> GetReviewCompletions(int position, string[] words)
    {
        if (position == 2)
            return ReviewSubcommands;

        if (words.Length < 3)
            return [];

        var subcommand = words[2].ToLowerInvariant();

        if (position == 3 && subcommand == "complete")
            return GetTaskNames();

        return [];
    }

    private static IEnumerable<string> GetTaskNames()
    {
        try
        {
            var configService = new ConfigService();
            var tasksPath = configService.GetTasksPath();

            if (!Directory.Exists(tasksPath))
                return [];

            return Directory.GetFiles(tasksPath, "*.md")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null && !n.StartsWith('_'))
                .Cast<string>();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> GetAgentNames()
    {
        try
        {
            var configService = new ConfigService();
            var config = configService.LoadConfig();
            return config?.Agents.Pool ?? [];
        }
        catch
        {
            return [];
        }
    }
}
