namespace DynaDocs.Commands;

using System.CommandLine;
using DynaDocs.Services;

public static class CompleteCommand
{
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
        => CompletionProvider.GetCompletions(position, words);
}
