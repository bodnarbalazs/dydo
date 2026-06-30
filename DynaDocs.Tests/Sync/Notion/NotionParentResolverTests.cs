namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;

/// <summary>Parent-page resolution: config wins over the environment, then the process env var. The
/// Windows User-registry fallback is not exercised here — it depends on machine state.</summary>
[Collection("ConsoleOutput")]
public class NotionParentResolverTests
{
    [Fact]
    public void ConfiguredValue_TakesPrecedence_OverEnv()
    {
        var saved = Environment.GetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar);
        Environment.SetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar, "env-page");
        try
        {
            Assert.Equal("config-page", NotionParentResolver.Resolve("config-page"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar, saved);
        }
    }

    [Fact]
    public void NoConfig_ReadsProcessEnvVar()
    {
        var saved = Environment.GetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar);
        Environment.SetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar, "env-page");
        try
        {
            Assert.Equal("env-page", NotionParentResolver.Resolve(null));
        }
        finally
        {
            Environment.SetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar, saved);
        }
    }

    [Fact]
    public void NoConfig_BlankEnv_ResolvesNullOnNonWindows()
    {
        var saved = Environment.GetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar);
        Environment.SetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar, "   ");
        try
        {
            var result = NotionParentResolver.Resolve(null);
            if (!OperatingSystem.IsWindows())
                Assert.Null(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(NotionParentResolver.ParentPageEnvVar, saved);
        }
    }
}
