namespace DynaDocs.Services;

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// The local archive <c>dydo clean</c> writes uncommitted work into before it removes a worktree:
/// <c>&lt;git common dir&gt;/dydo/clean-archive/</c>, one <c>&lt;UTC timestamp&gt;-&lt;branch&gt;</c> folder per
/// archived worktree holding <c>tracked.patch</c>, <c>untracked/</c>, <c>evidence/</c> and
/// <c>manifest.json</c>. Inside the git directory it outlives the worktree and is never committed.
/// When <c>dydo/</c> or <c>clean-archive/</c> there is a link, the archive is not used at all: no entry is
/// written, listed or pruned behind it, so retention never reaches outside the git directory.
/// </summary>
internal sealed partial class CleanArchive(string gitCommonDirectory, long capBytes)
{
    private readonly string root = Path.Combine(gitCommonDirectory, "dydo", "clean-archive");

    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";

    /// <summary>A fresh entry path for <paramref name="label"/>; nothing is created.</summary>
    public string NewEntryPath(string label, DateTime utcNow)
    {
        var name = utcNow.ToString(StampFormat, CultureInfo.InvariantCulture) + "-" + UnsafeNameCharacters().Replace(label, "-");
        var path = Path.Combine(root, name);
        for (var suffix = 2; Directory.Exists(path); suffix++)
            path = Path.Combine(root, $"{name}-{suffix}");
        return path;
    }

    public void Write(string entry, ArchivePlan plan, WorktreeEntry tree, DateTime utcNow)
    {
        ThrowIfLinked();
        Directory.CreateDirectory(entry);
        if (plan.Tracked.Count > 0)
        {
            var patch = GitRunner.Run(tree.Path, "diff", "HEAD", "--binary");
            if (!patch.Ok)
                throw new IOException($"git diff HEAD --binary failed: {patch.Error}");
            File.WriteAllBytes(Path.Combine(entry, "tracked.patch"), patch.Output);
        }

        var files = new List<(string Path, string Kind, long Bytes)>();
        foreach (var path in plan.Tracked)
            files.Add((path, "tracked", SizeOf(Path.Combine(tree.Path, path))));
        foreach (var path in plan.Untracked)
            files.Add((path, "untracked", Copy(tree.Path, path, Path.Combine(entry, "untracked"))));
        foreach (var path in plan.Evidence)
            files.Add((path, "evidence", Copy(tree.Path, path, Path.Combine(entry, "evidence"))));
        WriteManifest(Path.Combine(entry, "manifest.json"), tree, utcNow, files, plan.Links);
    }

    /// <summary>
    /// Entries older than <see cref="Retention"/> go, then the oldest until the archive fits the cap.
    /// <paramref name="keep"/> (this run's entries) is never pruned. Returns each pruned entry with why;
    /// throws <see cref="IOException"/>, pruning nothing, when the archive sits behind a link.
    /// </summary>
    public List<string> Prune(DateTime utcNow, IReadOnlySet<string> keep, bool dryRun)
    {
        var pruned = new List<string>();
        var remaining = new List<(string Path, long Bytes)>();
        foreach (var (path, stamp) in Entries())
        {
            if (!keep.Contains(path) && utcNow - stamp > Retention)
                pruned.Add(Remove(path, "older than 90 days", dryRun));
            else
                remaining.Add((path, SizeOf(path)));
        }

        var total = remaining.Sum(entry => entry.Bytes);
        foreach (var (path, bytes) in remaining.Where(entry => !keep.Contains(entry.Path)))
        {
            if (total <= capBytes)
                break;
            pruned.Add(Remove(path, $"archive over its {capBytes / (1024 * 1024)} MB cap", dryRun));
            total -= bytes;
        }

        return pruned;
    }

    /// <summary>Prints every entry, newest first, with what it holds.</summary>
    public void List(TextWriter output)
    {
        var entries = Entries().Reverse().ToList();
        if (entries.Count == 0)
        {
            output.WriteLine($"No archive entries in {root}");
            return;
        }

        output.WriteLine($"Archive: {root}");
        foreach (var (path, _) in entries)
            output.WriteLine($"  {Path.GetFileName(path)}  {Megabytes(SizeOf(path))}  {Describe(path)}");
        output.WriteLine("Restore: in a checkout of the entry's HEAD run git apply <entry>/tracked.patch, then copy untracked/ and evidence/ back, "
            + "and recreate each link manifest.json lists under links at its path, pointing to its target.");
    }

    // Every folder from the git directory down to the root is real, so what is enumerated is inside it.
    private void ThrowIfLinked()
    {
        foreach (var folder in new[] { Path.GetDirectoryName(root)!, root })
        {
            var info = new FileInfo(LinkSafeDelete.Extended(folder));
            // A missing path reports every attribute bit (-1), so existence is checked first.
            if ((int)info.Attributes != -1 && LinkSafeDelete.IsLink(info))
                throw new IOException($"{folder} is a link, and dydo clean does not follow it out of the git directory; "
                    + "replace it with a real folder");
        }
    }

    private IEnumerable<(string Path, DateTime Stamp)> Entries()
    {
        ThrowIfLinked();
        if (!Directory.Exists(root))
            return [];
        return Directory.EnumerateDirectories(root)
            .Select(path => (Path: path, Match: EntryName().Match(Path.GetFileName(path))))
            .Where(entry => entry.Match.Success)
            .Select(entry => (entry.Path, DateTime.ParseExact(entry.Match.Groups["stamp"].Value, StampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)))
            .OrderBy(entry => entry.Item2)
            .ThenBy(entry => entry.Path, StringComparer.Ordinal);
    }

    private static string Remove(string path, string reason, bool dryRun)
    {
        try
        {
            if (!dryRun)
                LinkSafeDelete.DeleteTree(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            reason += $"; deleting it failed: {ex.Message}";
        }

        return $"{Path.GetFileName(path)} ({reason})";
    }

    private static string Describe(string entry)
    {
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(entry, "manifest.json")));
            var rootElement = manifest.RootElement;
            var links = rootElement.TryGetProperty("links", out var linkArray) ? $", {linkArray.GetArrayLength()} link(s)" : "";
            return $"{rootElement.GetProperty("files").GetArrayLength()} file(s){links} from {rootElement.GetProperty("worktree").GetString()} at {rootElement.GetProperty("head").GetString()}";
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return "(no readable manifest.json)";
        }
    }

    private static long Copy(string worktree, string relativePath, string destinationRoot)
    {
        var source = LinkSafeDelete.Extended(Path.Combine(worktree, relativePath));
        var destination = LinkSafeDelete.Extended(Path.Combine(destinationRoot, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
        return new FileInfo(destination).Length;
    }

    private static void WriteManifest(string path, WorktreeEntry tree, DateTime utcNow,
        List<(string Path, string Kind, long Bytes)> files, IReadOnlyList<ArchiveLink> links)
    {
        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteString("worktree", tree.Path);
        json.WriteString("branch", tree.Branch);
        json.WriteString("head", tree.Head);
        json.WriteString("archivedUtc", utcNow.ToString("o", CultureInfo.InvariantCulture));
        json.WriteStartArray("files");
        foreach (var (filePath, kind, bytes) in files)
        {
            json.WriteStartObject();
            json.WriteString("path", filePath);
            json.WriteString("kind", kind);
            json.WriteNumber("bytes", bytes);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("links");
        foreach (var link in links)
        {
            json.WriteStartObject();
            json.WriteString("path", link.Path);
            json.WriteString("linksTo", link.LinksTo);
            json.WriteString("target", link.Target);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static long SizeOf(string path)
    {
        var extended = LinkSafeDelete.Extended(path);
        if (File.Exists(extended))
            return new FileInfo(extended).Length;
        if (!Directory.Exists(extended))
            return 0;
        return new DirectoryInfo(extended).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
    }

    private static string Megabytes(long bytes) =>
        (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

    [GeneratedRegex(@"^(?<stamp>\d{8}T\d{6}Z)-")]
    private static partial Regex EntryName();

    [GeneratedRegex(@"[^A-Za-z0-9._-]+")]
    private static partial Regex UnsafeNameCharacters();
}
