namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Services;
using DynaDocs.Services.Merge;
using DynaDocs.Utils;

/// <summary>
/// <c>dydo pr-ready</c>: check a tree and optional PR body are fit to open or ready a PR from.
/// The guard shares the tree checks in <see cref="PrReadyCheck"/>; merge shares the review parser.
/// </summary>
public static partial class PrReadyCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string?>("path")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "The tree to check (default: the current directory)"
        };
        var bodyOption = new Option<string?>("--body")
        {
            Description = "PR body file that must name its ticket and hold the reviewer's PASS block"
        };

        var command = new Command("pr-ready",
            "Check a PR can be opened: clean tree, HEAD equal to its upstream, and with --body a ticket key and PASS review");
        command.Arguments.Add(pathArgument);
        command.Options.Add(bodyOption);
        command.SetAction(parseResult => Run(
            Path.GetFullPath(parseResult.GetValue(pathArgument) ?? Environment.CurrentDirectory),
            parseResult.GetValue(bodyOption)));
        return command;
    }

    private static int Run(string directory, string? bodyFile)
    {
        var problems = PrReadyCheck.TreeProblems(directory) ?? [$"{directory} is not inside a git work tree"];
        if (bodyFile != null)
            problems = [.. problems, .. BodyFileProblems(bodyFile)];

        foreach (var problem in problems)
            Console.Error.WriteLine($"not ready: {problem}");
        if (problems.Count > 0)
            return ExitCodes.ValidationErrors;

        Console.WriteLine(bodyFile == null
            ? $"ready: {directory} is clean and HEAD matches its upstream"
            : $"ready: {directory} is clean, HEAD matches its upstream, and the body names its ticket and holds a PASS review");
        return ExitCodes.Success;
    }

    private static List<string> BodyFileProblems(string bodyFile)
    {
        if (!File.Exists(bodyFile))
            return [$"body file {bodyFile} not found"];

        var body = File.ReadAllText(bodyFile);
        List<string> problems = [];
        if (!TicketKeyRegex().IsMatch(body))
            problems.Add("the body names no ticket key, such as DYD-123");
        if (!IndependentReview.HasSection(body))
            problems.Add("the body has no '## Independent review' section");
        else if (!IndependentReview.Parse(body).Any(verdict => verdict is
                     { Pass: true, Rubric: "code" or "docs" or "merge", Candidate: not null, Base: not null }))
            problems.Add("the '## Independent review' section holds no reviewer 'PASS - rubric/type: ...' block with usable fields; "
                + "expected PASS - rubric/type: code; candidate <SHA>; base <SHA> "
                + "(code, docs or merge; 7–40 hex digits per SHA; optional candidate:/base: colons; hyphen, en dash or em dash after PASS)");
        return problems;
    }

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]*-\d+\b")]
    private static partial Regex TicketKeyRegex();
}
