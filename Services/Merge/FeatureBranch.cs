namespace DynaDocs.Services.Merge;

using System.Text.RegularExpressions;

/// <summary>
/// The only bases <c>dydo merge</c> merges into, fixed in code rather than config: <c>feature/</c> and
/// one or more name segments, each starting with a letter or digit. git's own branch-name rule then
/// refuses what the pattern lets through, such as <c>feature/a..b</c> or <c>feature/x.lock</c>.
/// </summary>
internal static partial class FeatureBranch
{
    private static readonly Dictionary<string, string> NoEnvironment = [];

    public static bool IsValid(string name, string root) =>
        PatternRegex().IsMatch(name)
        && GitRunner.RunIsolated(root, NoEnvironment, "check-ref-format", "--branch", name).Ok;

    [GeneratedRegex(@"\Afeature/[A-Za-z0-9][A-Za-z0-9._-]*(/[A-Za-z0-9][A-Za-z0-9._-]*)*\z")]
    private static partial Regex PatternRegex();
}
