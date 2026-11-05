namespace DynaDocs.Commands;

using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// The PR-ready rule (DYD-374, DYD-391): a PR shows the branch as the remote holds it, so a
/// PR-opening gh call (see <see cref="PrCallScanner"/>) is refused while the tree it shows is dirty
/// or HEAD differs from its upstream. That tree is the call's directory (the host's per-call cwd or
/// workdir, else the session's, else the project root), or for a named branch (<c>--head</c>,
/// <c>gh pr ready BRANCH</c>, a REST <c>head</c> field) every worktree with that branch checked out;
/// one git lists but cannot open is refused. A refusal names every tree it checked. A call acting on
/// another repository (host, owner and name) is refused, since its trees are not here, and a call
/// wrapped in a shell or eval is refused unread. It stops an accident, not a deliberate bypass. A
/// directory outside git is not this rule's business, wrapped or not.
/// </summary>
public static partial class GuardCommand
{
    // Windows paths ignore case; elsewhere two paths that differ only in case may be two trees.
    private static readonly StringComparer TreePaths =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// <paramref name="argv"/> is the command as an argv array when the host sent one;
    /// <paramref name="shell"/> is the shell that runs <paramref name="command"/> otherwise.
    /// </summary>
    internal static int? CheckPrReady(string command, IReadOnlyList<string>? argv, string? cwd, ShellDialect shell, GuardEnv env)
    {
        var inheritedRepo = Environment.GetEnvironmentVariable("GH_REPO");
        var calls = argv != null ? PrCallScanner.FindInArgv(argv, inheritedRepo) : PrCallScanner.Find(command, shell, inheritedRepo);
        if (calls.Count == 0)
            return null;

        var directory = CallDirectory(cwd, env);
        var directoryProblems = PrReadyCheck.TreeProblems(directory);
        if (directoryProblems == null)
            return null;

        if (calls.FirstOrDefault(c => c.Wrapper != null) is { } wrapped)
            return RefusePr($"BLOCKED: This command runs a PR call through {wrapped.Wrapper}, which the PR check does not read.",
                "  Wrapped PR calls are refused unread: run gh directly so the PR check can see it.");

        // A placeholder api path names the checkout's own owner or name for the part it leaves out.
        var originUrl = GitRunner.Run(directory, "remote", "get-url", "origin").Text;
        var origin = RepositoryId(originUrl);
        if (calls.Select(c => c.Repo).OfType<string>().Select(r => PrCallScanner.InRepository(r, originUrl))
                .FirstOrDefault(r => RepositoryId(r) is not { } named || named != origin) is { } foreign)
            return RefusePr($"BLOCKED: This PR names another repository, {foreign}; {directory} is a checkout of {origin ?? "no origin"}.",
                "  Its branch is not in these trees, so run it from that repo's checkout.");

        var lines = TreeLines(calls, directory, directoryProblems, cwd == null);
        return lines.Count == 0
            ? null
            : RefusePr(["BLOCKED: This PR would not match the branch in your tree.", .. lines, "  Run dydo pr-ready there and fix what it reports."]);
    }

    // Each checked tree's problems and its Checked line, or nothing when every tree is ready.
    private static List<string> TreeLines(List<PrCall> calls, string directory, List<string> directoryProblems, bool hostSentNoCwd)
    {
        List<string> lines = [];
        List<(string Path, string? Branch)> trees = [];
        var worktrees = calls.Any(c => c.Branch != null)
            ? WorktreeEntry.Parse(GitRunner.Run(directory, "worktree", "list", "--porcelain").Text)
            : [];
        foreach (var call in calls)
        {
            if (call.Branch == null)
            {
                trees.Add((directory, null));
                continue;
            }
            var holders = worktrees.Where(w => w.Branch == call.Branch).Select(w => (w.Path, (string?)call.Branch)).ToList();
            if (holders.Count == 0)
                lines.Add($"  {call.BranchForm} names branch {call.Branch}, which no worktree of this repository has checked out; check it out in a worktree and push it, then retry");
            trees.AddRange(holders);
        }

        var refused = lines.Count > 0;
        foreach (var (tree, branch) in trees.DistinctBy(t => t.Path, TreePaths))
        {
            var isDirectory = TreePaths.Equals(tree, directory);
            var problems = isDirectory ? directoryProblems : PrReadyCheck.TreeProblems(tree);
            if (problems == null)
            {
                lines.Add($"  {tree} holds branch {branch} but is not an available work tree; run git worktree prune or check the branch out again, then retry");
                refused = true;
                continue;
            }
            refused |= problems.Count > 0;
            lines.AddRange(problems.Select(p => $"  {p}"));
            lines.Add(CheckedLine(tree, problems.Count == 0, isDirectory && hostSentNoCwd));
        }
        return refused ? lines : [];
    }

    private static string CheckedLine(string tree, bool clean, bool projectRootFallback)
    {
        List<string> notes = [];
        if (projectRootFallback)
            notes.Add("the project root; the host sent no cwd");
        if (clean)
            notes.Add("clean, HEAD matches its upstream");
        return notes.Count == 0 ? $"  Checked: {tree}" : $"  Checked: {tree} ({string.Join("; ", notes)})";
    }

    private static int RefusePr(params string[] lines)
    {
        foreach (var line in lines)
            Console.Error.WriteLine(line);
        return ExitCodes.ToolError;
    }

    /// <summary>
    /// HOST/OWNER/REPO in lower case, without <c>.git</c>, from OWNER/REPO (github.com, gh's default
    /// host), HOST/OWNER/REPO or a remote URL (scheme or scp form); null when it names none.
    /// </summary>
    private static string? RepositoryId(string value)
    {
        var name = value.Trim().TrimEnd('/');
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        string? host = null;
        if (RemoteUrlRegex().Match(name) is { Success: true } remote)
            (host, name) = (remote.Groups["host"].Value, remote.Groups["path"].Value);
        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (host == null && parts.Length == 3)
            (host, parts) = (parts[0], parts[1..]);
        if (parts.Length < 2 || (host == null && parts.Length != 2))
            return null;
        return $"{host ?? "github.com"}/{parts[^2]}/{parts[^1]}".ToLowerInvariant();
    }

    // scheme://[user@]host[:port]/path, or scp's [user@]host:path.
    [GeneratedRegex(@"^(?:[a-z][a-z0-9+.-]*://(?:[^@/]*@)?(?<host>[^/:]+)(?::\d+)?/|(?:[^@/:]+@)?(?<host>[^/:]+):(?!/))(?<path>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex RemoteUrlRegex();

    /// <summary>Four times GitHub's 65,536-character PR body limit at one byte a character.</summary>
    internal const int PrBodyFileMaxBytes = 256 * 1024;

    private const int MaxPathLinks = 40;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// The PR body text that the command's <c>gh pr create</c> and <c>gh pr new</c> calls read from
    /// body files the guard may read (<see cref="ReadPrBodyFile"/>), named from the call directory.
    /// The text is empty when there is none (DYD-639).
    /// </summary>
    private static string PrBodyFileText(string command, IReadOnlyList<string>? argv, string? cwd, ShellDialect shell,
        GuardEnv env, IOffLimitsService offLimitsService)
    {
        var files = argv != null ? PrCallScanner.BodyFilesInArgv(argv) : PrCallScanner.BodyFiles(command, shell);
        var directory = CallDirectory(cwd, env);
        return string.Concat(files.Select(file => ReadPrBodyFile(file, directory, offLimitsService)));
    }

    /// <summary>
    /// The text of the PR body file <paramref name="path"/>, named from <paramref name="directory"/>,
    /// the call directory, or null when the guard may not read it. The guard may read a regular file
    /// that sits, once every link on its path resolves, in the same git work tree as the call
    /// directory. It must not be off-limits, must be at most <see cref="PrBodyFileMaxBytes"/>, and
    /// must be valid UTF-8. Anything else, or any I/O error, reads as null, so the caller keeps its
    /// warning. It stops accidents, not a deliberate bypass.
    /// </summary>
    internal static string? ReadPrBodyFile(string path, string directory, IOffLimitsService offLimits)
    {
        if (path is "" or "-")
            return null;
        try
        {
            // Windows takes .. by name before it opens a path; elsewhere .. follows the links before it.
            var named = Path.Combine(directory, path);
            if (RealPath(OperatingSystem.IsWindows() ? Path.GetFullPath(named) : named) is not { } file
                || !InWorkTreeOf(file, directory)
                || offLimits.IsPathOffLimits(Path.GetFullPath(named)) != null || offLimits.IsPathOffLimits(file) != null)
                return null;
            return BoundedText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException or SecurityException)
        {
            return null;
        }
    }

    // git names one work tree for the file's own directory and the call directory. Git resolves
    // links and short names itself, and a .git directory is no work tree.
    private static bool InWorkTreeOf(string file, string directory)
    {
        var tree = WorkTreeOf(directory);
        return tree != null && Path.GetDirectoryName(file) is { } parent && Directory.Exists(parent)
            && WorkTreeOf(parent) == tree;
    }

    private static string? WorkTreeOf(string directory)
    {
        var top = GitRunner.Run(directory, "rev-parse", "--show-toplevel");
        return top.Ok ? Path.GetFullPath(top.Text.Trim()) : null;
    }

    // An empty size is no body. A FIFO or device reports one too, so it is never opened. The cap
    // holds on the bytes read, so a file growing past it reads as over the cap.
    private static string? BoundedText(string file)
    {
        var info = new FileInfo(file);
        if (!info.Exists || info.Length == 0)
            return null;

        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var buffer = new byte[PrBodyFileMaxBytes + 1];
        var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        return read > PrBodyFileMaxBytes ? null : StrictUtf8.GetString(buffer, 0, read);
    }

    /// <summary>
    /// The rooted <paramref name="path"/> with every link on it resolved and each <c>..</c> taken
    /// after the links before it, as the OS opens it (realpath(3)); null past <see cref="MaxPathLinks"/> links.
    /// </summary>
    private static string? RealPath(string path)
    {
        var resolved = Path.GetPathRoot(path)!;
        var pending = new Stack<string>(PathSegments(path[resolved.Length..]).Reverse());
        var links = 0;
        while (pending.TryPop(out var segment))
        {
            if (segment == "..")
            {
                resolved = Path.GetDirectoryName(resolved) ?? resolved;
                continue;
            }

            var next = Path.Join(resolved, segment);
            if (new FileInfo(next).LinkTarget is not { } target)
            {
                resolved = next;
                continue;
            }
            if (++links > MaxPathLinks)
                return null;

            var targetRoot = Path.GetPathRoot(target)!;
            if (targetRoot.Length > 0)
                resolved = targetRoot;
            foreach (var part in PathSegments(target[targetRoot.Length..]).Reverse())
                pending.Push(part);
        }
        return resolved;
    }

    private static IEnumerable<string> PathSegments(string path) =>
        path.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries).Where(segment => segment != ".");
}
