namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

[Collection("ConsoleOutput")]
public class NotionCommandTests
{
    [Fact]
    public void Sync_NoProcessToken_ExitsCleanly_WithoutNetworkCall()
    {
        // Clear the process token AND run from a dir with no dydo.json above it, so neither a token
        // nor a project resolves. On a clean env this hits the "not configured" gate; on a machine
        // where DYDO_NOTION_TOKEN is set in the Windows User registry the resolver still finds it,
        // so we tolerate either graceful message — both exit 0 and make no network call.
        var saved = Environment.GetEnvironmentVariable(NotionCommand.TokenEnvVar);
        var savedCwd = Directory.GetCurrentDirectory();
        var temp = Path.Combine(Path.GetTempPath(), "dydo-notion-notoken-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, null);
        try
        {
            Directory.SetCurrentDirectory(temp);
            var (code, _, stderr) = ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke());

            Assert.Equal(0, code);
            Assert.True(stderr.Contains("not configured") || stderr.Contains("no dydo.json"), stderr);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, saved);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public void Sync_WithToken_NoProject_ReportsNoConfig()
    {
        // With a token present but no dydo.json on the path, the command reports cleanly and makes
        // no network call. Run from a temp dir with no dydo.json above it is impractical (the repo
        // root has one), so we drive the no-project branch by running from the system temp root.
        var saved = Environment.GetEnvironmentVariable(NotionCommand.TokenEnvVar);
        var savedCwd = Directory.GetCurrentDirectory();
        var temp = Path.Combine(Path.GetTempPath(), "dydo-notion-noproj-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, "secret-token");
        try
        {
            Directory.SetCurrentDirectory(temp);
            var (code, _, stderr) = ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("no dydo.json", stderr);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, saved);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public void Create_HasSyncSubcommand()
    {
        var cmd = NotionCommand.Create();
        Assert.Contains(cmd.Subcommands, c => c.Name == "sync");
    }

    [Fact]
    public void SyncCommand_HasDryRunOption()
    {
        var sync = NotionCommand.Create().Subcommands.First(c => c.Name == "sync");
        Assert.Contains(sync.Options, o => o.Name == "--dry-run");
    }
}
