namespace DynaDocs.Commands;

using System.Text.RegularExpressions;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// The PR-ready rule (DYD-374): a PR shows the branch as the remote holds it, so a plain
/// <c>gh pr create</c>, <c>gh pr new</c> or <c>gh pr ready</c> is refused while the tree is dirty or
/// HEAD differs from its upstream. It stops an accident, not a deliberate bypass: wrapped, encoded
/// or <c>-R</c>-redirected calls are not decoded. The call must start a shell segment as the bash
/// analyzer splits them, so a separator inside quoted text, such as a commit message, starts none. A
/// directory outside git is not this rule's business.
/// </summary>
public static partial class GuardCommand
{
    internal static int? CheckPrReady(string command, string? cwd, GuardEnv env)
    {
        if (!BashCommandAnalyzer.SplitCommand(command).Any(segment => PrOpenRegex().IsMatch(segment)))
            return null;

        var directory = cwd ?? env.ProjectRoot ?? Environment.CurrentDirectory;
        var problems = PrReadyCheck.TreeProblems(directory);
        if (problems is null or [])
            return null;

        Console.Error.WriteLine("BLOCKED: This PR would not match the branch in your tree.");
        foreach (var problem in problems)
            Console.Error.WriteLine($"  {problem}");
        Console.Error.WriteLine(cwd == null
            ? $"  Checked: {directory} (the project root; the host sent no cwd)"
            : $"  Checked: {directory}");
        Console.Error.WriteLine("  Run dydo pr-ready there and fix what it reports.");
        return ExitCodes.ToolError;
    }

    [GeneratedRegex(@"^\s*gh\s+pr\s+(?:create|new|ready)\b")]
    private static partial Regex PrOpenRegex();
}
