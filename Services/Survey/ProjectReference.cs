namespace DynaDocs.Services.Survey;

using System.Text.RegularExpressions;

/// <summary>
/// Reads the Project argument of <c>dydo survey</c>: a Project UUID, a slugId (Linear's 12 hex
/// characters), or a Linear Project URL, whose slug segment ends in the slugId.
/// </summary>
internal static partial class ProjectReference
{
    /// <summary>The id Linear's <c>project(id:)</c> accepts, or null when the argument is none of the three.</summary>
    public static string? Parse(string argument)
    {
        if (Guid.TryParseExact(argument, "D", out _) || SlugId().IsMatch(argument))
            return argument;
        if (!Uri.TryCreate(argument, UriKind.Absolute, out var url) || url.Host != "linear.app")
            return null;

        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3 || segments[1] != "project")
            return null;
        var slugId = segments[2][(segments[2].LastIndexOf('-') + 1)..];
        return SlugId().IsMatch(slugId) ? slugId : null;
    }

    [GeneratedRegex("^[0-9a-f]{12}$")]
    private static partial Regex SlugId();
}
