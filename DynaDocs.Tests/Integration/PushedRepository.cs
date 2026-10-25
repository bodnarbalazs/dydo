namespace DynaDocs.Tests.Integration;

using DynaDocs.Services;

/// <summary>
/// Makes <c>tree</c> a git repository whose files are committed on <c>feature</c> and pushed with
/// <c>-u</c> to a bare origin at <c>origin</c>: the clean, pushed state a PR is opened from.
/// </summary>
internal static class PushedRepository
{
    public static void Create(string tree, string origin)
    {
        Directory.CreateDirectory(tree);
        Git(Path.GetDirectoryName(origin)!, "init", "--bare", "-b", "master", origin);
        Git(tree, "init", "-b", "feature");
        Git(tree, "config", "user.name", "Test");
        Git(tree, "config", "user.email", "test@example.com");
        Git(tree, "config", "commit.gpgsign", "false");
        File.AppendAllText(Path.Combine(tree, ".gitignore"), "\ndydo/_system/.local/\ndydo/agents/\n");
        File.WriteAllText(Path.Combine(tree, "readme.md"), "hello\n");
        Commit(tree, "seed");
        Git(tree, "remote", "add", "origin", origin);
        Git(tree, "push", "-u", "origin", "feature");
    }

    public static void Commit(string tree, string message)
    {
        Git(tree, "add", "-A");
        Git(tree, "commit", "-m", message);
    }

    public static GitResult Git(string directory, params string[] args)
    {
        var result = GitRunner.Run(directory, args);
        Assert.True(result.Ok, $"git {string.Join(' ', args)} failed: {result.Error}");
        return result;
    }
}
