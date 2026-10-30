namespace DynaDocs.Services.Merge;

/// <summary>How <c>dydo merge</c> names a commit in what it prints: the first 12 characters of its SHA.</summary>
internal static class CommitSha
{
    public static string Short(string sha) => sha.Length > 12 ? sha[..12] : sha;
}
