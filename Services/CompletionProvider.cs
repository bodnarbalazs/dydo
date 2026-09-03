namespace DynaDocs.Services;

using DynaDocs.Models;

public static class CompletionProvider
{
    private static readonly string[] TopLevelCommands =
    [
        "check", "fix", "index", "init", "graph", "guard",
        "sync",
        "completions", "complete", "template", "validate",
        "version", "help"
    ];

    private static readonly string[] GuardActions = ["edit", "write", "delete", "read"];

    // Position 2: command → list of subcommands
    private static readonly Dictionary<string, string[]> SubcommandLists = new()
    {
        ["init"] = ["claude", "codex", "all", "none"],
        ["completions"] = ["bash", "zsh", "powershell"],
        ["graph"] = ["stats"],
        ["template"] = ["update"],
    };

    private static readonly Dictionary<string, Func<IEnumerable<string>>> OptionValueHandlers = new()
    {
        ["--area"] = () => Frontmatter.ValidAreas,
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

        return [];
    }

    public static IEnumerable<string>? GetOptionValueCompletions(string option)
    {
        return OptionValueHandlers.TryGetValue(option, out var handler)
            ? handler()
            : null;
    }

}
