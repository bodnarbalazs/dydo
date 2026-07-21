namespace DynaDocs.Tests.Sync.Notion.Live;

/// <summary>
/// Offline gate for the live-suite wiring (ns-9 success criteria): the fixture must FAIL loudly on a
/// misconfigured live run, never silently skip or no-op — the sprint watch-out that <c>NotionSyncService</c>'s
/// exit-success-on-missing-config must not leak into the live fixtures. These run in the normal (no-token)
/// suite: they drive <see cref="NotionLiveEnv"/>/<see cref="NotionLiveFactAttribute"/> directly with env vars
/// set in-process, and restore the process env in a finally so no other test sees a leaked value.
/// </summary>
public sealed class NotionLiveEnvTests
{
    private static void WithEnv(string? token, string? parent, Action body)
    {
        var savedToken = Environment.GetEnvironmentVariable(NotionLiveEnv.TokenVar);
        var savedParent = Environment.GetEnvironmentVariable(NotionLiveEnv.ParentVar);
        try
        {
            Environment.SetEnvironmentVariable(NotionLiveEnv.TokenVar, token);
            Environment.SetEnvironmentVariable(NotionLiveEnv.ParentVar, parent);
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(NotionLiveEnv.TokenVar, savedToken);
            Environment.SetEnvironmentVariable(NotionLiveEnv.ParentVar, savedParent);
        }
    }

    [Fact]
    public void BothVarsUnset_IsNotConfigured_AndLiveFactSkips()
    {
        WithEnv(null, null, () =>
        {
            Assert.True(NotionLiveEnv.NotConfigured);
            // A skip, not a failure — the fake suite stays green in CI where no token exists.
            Assert.NotNull(new NotionLiveFactAttribute().Skip);
        });
    }

    [Fact]
    public void OnlyToken_FailsLoudly_NotSkip()
    {
        WithEnv("secret_garbage", null, () =>
        {
            Assert.False(NotionLiveEnv.NotConfigured);
            // NOT skipped — a partial configuration runs and then throws, rather than silently passing.
            Assert.Null(new NotionLiveFactAttribute().Skip);
            Assert.Throws<InvalidOperationException>(() => NotionLiveEnv.RequireConfig());
        });
    }

    [Fact]
    public void OnlyParent_FailsLoudly_NotSkip()
    {
        WithEnv(null, "some-parent-page-id", () =>
        {
            Assert.False(NotionLiveEnv.NotConfigured);
            Assert.Null(new NotionLiveFactAttribute().Skip);
            Assert.Throws<InvalidOperationException>(() => NotionLiveEnv.RequireConfig());
        });
    }

    [Fact]
    public void BothVarsSet_ResolvesConfig_NoSkip()
    {
        WithEnv("secret_token", "parent-page-id", () =>
        {
            Assert.False(NotionLiveEnv.NotConfigured);
            Assert.Null(new NotionLiveFactAttribute().Skip);
            var (token, parent) = NotionLiveEnv.RequireConfig();
            Assert.Equal("secret_token", token);
            Assert.Equal("parent-page-id", parent);
        });
    }
}
