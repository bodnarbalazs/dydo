namespace DynaDocs.Services;

using DynaDocs.Models;

public static class CompletionProvider
{
    private static readonly string[] TopLevelCommands =
    [
        "check", "fix", "index", "init", "graph", "guard",
        "task", "issue",
        "review", "sync", "notion",
        "completions", "complete", "template", "roles", "validate",
        "watchdog", "version", "help"
    ];

    private static readonly string[] Roles =
        ["code-writer", "reviewer", "co-thinker", "chief-of-staff", "docs-writer", "test-writer", "orchestrator"];

    private static readonly string[] ReviewStatuses = ["pass", "fail"];

    private static readonly string[] GuardActions = ["edit", "write", "delete", "read"];

    // Position 2: command → list of subcommands
    private static readonly Dictionary<string, string[]> SubcommandLists = new()
    {
        ["task"] = ["create", "done", "list", "ready-for-review"],
        ["review"] = ["complete"],
        ["init"] = ["claude", "none"],
        ["completions"] = ["bash", "zsh", "powershell"],
        ["graph"] = ["stats"],
        ["issue"] = ["create", "list", "resolve"],
        ["roles"] = ["list", "create", "reset"],
        ["template"] = ["update"],
        ["watchdog"] = ["start", "stop", "run"],
        ["notion"] = ["sync"],
    };

    // Position 3: (command, subcommand) → dynamic completion source
    private static readonly Dictionary<(string Cmd, string Sub), Func<IEnumerable<string>>> ArgCompletions = new()
    {
        [("task", "done")] = GetTaskNames,
        [("task", "ready-for-review")] = GetTaskNames,
        [("review", "complete")] = GetTaskNames,
    };

    private static readonly Dictionary<string, Func<IEnumerable<string>>> OptionValueHandlers = new()
    {
        ["--role"] = () => Roles,
        ["--task"] = GetTaskNames,
        ["--subject"] = GetTaskNames,
        ["--area"] = () => Frontmatter.ValidAreas,
        ["--status"] = () => ReviewStatuses,
        ["--action"] = () => GuardActions,
    };

    public static IEnumerable<string> GetCompletions(int position, string[] words)
    {
        if (position >= 1 && position <= words.Length)
        {
            var optionCompletions = GetOptionValueCompletions(words[position - 1]);
            if (optionCompletions != null)
                return optionCompletions;
        }

        if (position <= 1 || words.Length < 2)
            return TopLevelCommands;

        return GetSubcommandCompletions(words[1].ToLowerInvariant(), position, words);
    }

    public static IEnumerable<string> GetSubcommandCompletions(string command, int position, string[] words)
    {
        if (!SubcommandLists.TryGetValue(command, out var subcommands))
            return [];

        if (position == 2)
            return subcommands;

        if (position == 3 && words.Length >= 3)
        {
            var key = (command, words[2].ToLowerInvariant());
            return ArgCompletions.TryGetValue(key, out var handler) ? handler() : [];
        }

        return [];
    }

    public static IEnumerable<string>? GetOptionValueCompletions(string option)
    {
        return OptionValueHandlers.TryGetValue(option, out var handler)
            ? handler()
            : null;
    }

    public static IEnumerable<string> GetTaskNames()
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

}
