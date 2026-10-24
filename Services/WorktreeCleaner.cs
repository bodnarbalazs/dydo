namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// <c>dydo clean</c>: removes linked worktrees of the repository around <paramref name="currentDirectory"/>
/// without losing work. Uncommitted work is archived first (<see cref="CleanArchive"/>), links inside a
/// tree are unlinked before anything recursive runs (<see cref="LinkSafeDelete"/>), and a local branch
/// is deleted only when <see cref="MergedBranchJudge"/> finds its tip on origin's default branch. The main
/// checkout, the tree holding the current directory, locked trees, trees whose submodules hold work
/// (<see cref="SubmoduleWork"/>), trees whose dydo.json cannot be parsed, trees in use and trees under T3's root are skipped with the reason.
/// Prints one line per worktree, ending with what happened to its branch. A dry run changes nothing, so it runs no
/// busy check (the Windows probe moves the folder) and says so.
/// </summary>
internal sealed class WorktreeCleaner(string currentDirectory, string t3WorktreeRoot, Func<string, string?>? findUse = null)
{
    private readonly Func<string, string?> _findUse = findUse ?? WorktreeProcessProbe.FindUse;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string DefaultT3WorktreeRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".t3", "worktrees");

    public int Clean(IReadOnlyList<string> targets, bool all, bool dryRun)
    {
        if (all == targets.Count > 0)
            return Error("name the worktree to clean, by path or branch, and run from outside it: "
                + "dydo clean --dry-run <worktree>, then dydo clean <worktree>. A named worktree and --all do not combine.");
        if (!TryOpen(out var repository, out var error) || !TryLoadCap(out var capBytes, out error))
            return Error(error!);

        var run = new Run(repository!, new CleanArchive(repository!.CommonDirectory, capBytes),
            new MergedBranchJudge(currentDirectory, dryRun), dryRun, DateTime.UtcNow);
        var problem = false;
        foreach (var (target, tree) in Select(repository!.Worktrees, targets, all))
        {
            var outcome = tree == null
                ? new Outcome($"skipped {target}: not a worktree of this repository", Removed: false, Failed: false)
                : CleanOne(tree, run);
            Console.WriteLine(outcome.Line);
            // A named worktree that was not removed is a refusal; --all skips are expected.
            problem |= outcome.Failed || (!all && !outcome.Removed);
        }

        problem |= !Prune(run);
        return problem ? ExitCodes.ToolError : ExitCodes.Success;
    }

    public int ListArchive()
    {
        if (!TryOpen(out var repository, out var error))
            return Error(error!);
        try
        {
            new CleanArchive(repository!.CommonDirectory, 0).List(Console.Out);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Error($"archive not listed: {ex.Message}");
        }

        return ExitCodes.Success;
    }

    private static bool Prune(Run run)
    {
        try
        {
            foreach (var pruned in run.Archive.Prune(run.Now, run.Written, run.DryRun))
                Console.WriteLine($"{(run.DryRun ? "would prune" : "pruned")} archive entry {pruned}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"archive not pruned: {ex.Message}");
            return false;
        }
    }

    private Outcome CleanOne(WorktreeEntry tree, Run run)
    {
        try
        {
            var skip = SkipReason(tree, run, out var evidenceRoots);
            return skip == null ? Remove(tree, run, evidenceRoots) : new Outcome($"skipped {tree.Path}: {skip}{Kept(tree)}", Removed: false, Failed: false);
        }
        catch (ProbeStrandedException ex)
        {
            return Stranded(tree, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure(tree, ex.Message);
        }
    }

    // The lock keeps git worktree prune, later in this run or after it, from dropping the tree's
    // admin entry while its folder sits at the probe path.
    private Outcome Stranded(WorktreeEntry tree, ProbeStrandedException ex)
    {
        var locked = GitRunner.Run(currentDirectory, "worktree", "lock", "--reason",
            $"dydo clean's busy check left it at {ex.Probe}", tree.Path).Ok;
        var moveBack = $"Move-Item -LiteralPath '{ex.Probe}' -Destination '{tree.Path}'";
        return Failure(tree, locked
            ? $"{ex.Message}; it is locked so git worktree prune keeps it. Move it back with {moveBack}, then git worktree unlock '{tree.Path}'"
            : $"{ex.Message}; run no git worktree prune until it is back. Move it back with {moveBack}");
    }

    // A dydo.json that cannot be parsed leaves the evidence folders unknown, so the tree stays.
    private string? SkipReason(WorktreeEntry tree, Run run, out IReadOnlyList<string> evidenceRoots)
    {
        evidenceRoots = [];
        var repository = run.Repository;
        if (tree.IsMain)
            return "the main checkout is never removed";
        if (SamePath(tree.Path, repository.CurrentTop) || IsWithin(currentDirectory, tree.Path))
            return "the current directory is inside it";
        if (IsWithin(tree.Path, t3WorktreeRoot))
            return $"T3 owns worktrees under {t3WorktreeRoot}; archive the thread instead";
        if (tree.Locked)
            return "it is locked";
        if (!Directory.Exists(tree.Path))
            return null;
        var roots = ArchivePlan.EvidenceRoots(tree.Path, tree.Head, out var unknown);
        if (roots == null)
            return unknown;
        evidenceRoots = roots;
        return SubmoduleWork.Find(tree.Path) ?? (run.DryRun ? null : _findUse(tree.Path));
    }

    private Outcome Remove(WorktreeEntry tree, Run run, IReadOnlyList<string> evidenceRoots)
    {
        var verdict = tree.Branch == null ? (Delete: false, Reason: "detached HEAD") : run.Judge.Judge(tree.Head!);
        string? error = null;
        var plan = Directory.Exists(tree.Path)
            ? ArchivePlan.Collect(tree.Path, evidenceRoots, out error)
            : new ArchivePlan([], [], [], []);
        if (plan == null)
            return Failure(tree, error!);
        var entry = plan.IsEmpty ? null : run.Archive.NewEntryPath(tree.Label, run.Now);
        Warn(tree, plan, entry, run.DryRun);

        if (run.DryRun)
            return new Outcome(Describe(tree, entry, "would remove", "would archive and remove")
                + BranchNote(tree, verdict, verdict.Delete ? "would be deleted" : "would be kept") + "; busy check not run in a dry run",
                Removed: true, Failed: false);

        if (entry != null)
        {
            try
            {
                run.Archive.Write(entry, plan, tree, run.Now);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Failure(tree, $"archiving its work failed, so nothing was removed: {ex.Message}");
            }

            run.Written.Add(entry);
        }

        if (!DeleteWorktree(tree.Path))
            return Failure(tree, "the folder could not be deleted" + (entry == null ? "" : $"; its work is archived in {entry}"));
        var deleted = verdict.Delete && DeleteMergedBranch(currentDirectory, tree.Branch!, tree.Head!);
        return new Outcome(Describe(tree, entry, "removed", "archived and removed")
            + BranchNote(tree, verdict, deleted ? "deleted" : "kept"), Removed: true, Failed: false);
    }

    private static Outcome Failure(WorktreeEntry tree, string reason) =>
        new($"failed {tree.Path}: {reason}{Kept(tree)}", Removed: false, Failed: true);

    // A tree that stays keeps its branch too.
    private static string Kept(WorktreeEntry tree) => BranchNote(tree, (false, "worktree not removed"), "kept");

    private bool DeleteWorktree(string path)
    {
        if (Directory.Exists(path))
            LinkSafeDelete.UnlinkAll(path);
        GitRunner.Run(currentDirectory, "worktree", "remove", "--force", path);
        if (Directory.Exists(path))
            LinkSafeDelete.DeleteTree(path);
        GitRunner.Run(currentDirectory, "worktree", "prune");
        return !Directory.Exists(path);
    }

    // git branch -d also demands the branch be merged into HEAD (or its upstream), which a stale
    // main checkout fails. The judge proved verifiedTip is on origin's default branch, so the fallback
    // deletes the ref only while it still points there: a ref that moved since the verdict is kept.
    internal static bool DeleteMergedBranch(string repository, string branch, string verifiedTip) =>
        GitRunner.Run(repository, "branch", "-d", branch).Ok
        || GitRunner.Run(repository, "update-ref", "-d", $"refs/heads/{branch}", verifiedTip).Ok;

    private static string Describe(WorktreeEntry tree, string? entry, string plain, string archived) =>
        entry == null ? $"{plain} {tree.Path}" : $"{archived} {tree.Path} -> {entry}";

    private static string BranchNote(WorktreeEntry tree, (bool Delete, string Reason) verdict, string outcome) =>
        tree.Branch == null ? "; no branch (detached HEAD)" : $"; branch {tree.Branch} {outcome} ({verdict.Reason})";

    private static void Warn(WorktreeEntry tree, ArchivePlan plan, string? entry, bool dryRun)
    {
        if (entry == null)
            return;
        var error = Console.Error;
        error.WriteLine($"WARNING: {tree.Path} holds uncommitted work; dydo clean {(dryRun ? "would archive" : "archives")} it before removing the worktree:");
        foreach (var path in plan.Tracked)
            error.WriteLine($"  tracked    {path}");
        foreach (var path in plan.Untracked)
            error.WriteLine($"  untracked  {path}");
        foreach (var path in plan.Evidence)
            error.WriteLine($"  evidence   {path}");
        foreach (var link in plan.Links)
            error.WriteLine($"  link       {link.Path} -> {link.Target} (recorded, not followed)");
        error.WriteLine($"  Archive: {entry}");
    }

    private IEnumerable<(string Target, WorktreeEntry? Tree)> Select(
        List<WorktreeEntry> worktrees, IReadOnlyList<string> targets, bool all)
    {
        if (all)
            return worktrees.Select(tree => (tree.Path, (WorktreeEntry?)tree));
        return targets.Select(target => (target, (WorktreeEntry?)worktrees.FirstOrDefault(tree =>
            SamePath(tree.Path, Path.GetFullPath(target, currentDirectory)) || tree.Branch == target)));
    }

    private bool TryOpen(out Repository? repository, out string? error)
    {
        repository = null;
        var list = GitRunner.Run(currentDirectory, "worktree", "list", "--porcelain");
        var common = GitRunner.Run(currentDirectory, "rev-parse", "--path-format=absolute", "--git-common-dir");
        if (!list.Ok || !common.Ok)
        {
            error = $"not inside a git repository ({(list.Ok ? common : list).Error})";
            return false;
        }

        var top = GitRunner.Run(currentDirectory, "rev-parse", "--show-toplevel");
        repository = new Repository(WorktreeEntry.Parse(list.Text), Path.GetFullPath(common.Text.Trim()),
            top.Ok ? Path.GetFullPath(top.Text.Trim()) : null);
        error = null;
        return true;
    }

    private bool TryLoadCap(out long capBytes, out string? error)
    {
        capBytes = 0;
        DydoConfig? config;
        try
        {
            config = new ConfigService().LoadConfigStrict(currentDirectory);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }

        var megabytes = config?.Clean?.ArchiveSizeCapMb ?? CleanConfig.DefaultArchiveSizeCapMb;
        error = megabytes > 0 ? null : "dydo.json clean.archiveSizeCapMb must be a positive whole number of megabytes.";
        capBytes = megabytes * 1024L * 1024L;
        return error == null;
    }

    private static bool SamePath(string a, string? b) =>
        b != null && string.Equals(Trim(a), Trim(b), PathComparison);

    private static bool IsWithin(string path, string root)
    {
        var full = Trim(Path.GetFullPath(path));
        var top = Trim(Path.GetFullPath(root));
        return string.Equals(full, top, PathComparison)
            || full.StartsWith(top + Path.DirectorySeparatorChar, PathComparison);
    }

    private static string Trim(string path) => path.TrimEnd('\\', '/');

    private static int Error(string message)
    {
        Console.Error.WriteLine($"clean: {message}");
        return ExitCodes.ToolError;
    }

    private sealed record Outcome(string Line, bool Removed, bool Failed);

    private sealed record Repository(List<WorktreeEntry> Worktrees, string CommonDirectory, string? CurrentTop);

    private sealed record Run(Repository Repository, CleanArchive Archive, MergedBranchJudge Judge, bool DryRun, DateTime Now)
    {
        public HashSet<string> Written { get; } = [];
    }
}
