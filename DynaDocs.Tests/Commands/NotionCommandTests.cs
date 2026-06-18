namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

[Collection("ConsoleOutput")]
public class NotionCommandTests
{
    [Fact]
    public void Sync_NoToken_ReportsNotConfigured_AndExitsZero()
    {
        var saved = Environment.GetEnvironmentVariable(NotionCommand.TokenEnvVar);
        Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, null);
        try
        {
            var (code, _, stderr) = ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("not configured", stderr);
            Assert.Contains(NotionCommand.TokenEnvVar, stderr);
        }
        finally
        {
            Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, saved);
        }
    }

    [Fact]
    public void Sync_WithToken_ReportsAdapterNotAvailable()
    {
        var saved = Environment.GetEnvironmentVariable(NotionCommand.TokenEnvVar);
        Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, "secret-token");
        try
        {
            var (code, _, stderr) = ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("not available", stderr);
        }
        finally
        {
            Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, saved);
        }
    }

    [Fact]
    public void Create_HasSyncSubcommand()
    {
        var cmd = NotionCommand.Create();
        Assert.Contains(cmd.Subcommands, c => c.Name == "sync");
    }
}
