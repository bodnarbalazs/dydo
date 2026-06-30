namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;

/// <summary>Token resolution via the process environment. The Windows User-registry fallback is not
/// exercised here — it depends on machine state and would mutate the user's registry.</summary>
[Collection("ConsoleOutput")]
public class NotionTokenResolverTests
{
    [Fact]
    public void Resolve_ReadsProcessEnvVar()
    {
        var saved = Environment.GetEnvironmentVariable(NotionTokenResolver.TokenEnvVar);
        Environment.SetEnvironmentVariable(NotionTokenResolver.TokenEnvVar, "tok-123");
        try
        {
            Assert.Equal("tok-123", NotionTokenResolver.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable(NotionTokenResolver.TokenEnvVar, saved);
        }
    }

    [Fact]
    public void Resolve_BlankProcessVar_FallsThroughToNullOnNonWindows_OrUserVarOnWindows()
    {
        var saved = Environment.GetEnvironmentVariable(NotionTokenResolver.TokenEnvVar);
        Environment.SetEnvironmentVariable(NotionTokenResolver.TokenEnvVar, "   ");
        try
        {
            // Process var is whitespace -> ignored. On non-Windows there is no fallback so the
            // result is null; on Windows it consults the User target (which is unset in CI).
            var result = NotionTokenResolver.Resolve();
            if (!OperatingSystem.IsWindows())
                Assert.Null(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(NotionTokenResolver.TokenEnvVar, saved);
        }
    }
}
