namespace DynaDocs.Tests.Sync.Notion.Live;

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself when the live suite is not configured (ns-9): with
/// NEITHER env var set the test is reported <em>skipped</em>, so the full fake-backed suite stays green in
/// CI where no token exists. xunit's reflection discoverer reads the <see cref="FactAttribute.Skip"/>
/// property off the constructed attribute instance, so setting it here is honoured.
/// <para>Crucially this only skips on BOTH vars absent. A partial configuration (one var set) is NOT skipped
/// — the test runs and <see cref="NotionLiveTestBase"/>'s fixture throws on it, so a half-configured live run
/// fails loudly rather than silently passing (the sprint watch-out). The <c>notion-live</c> category trait
/// lives on the test classes, so <c>dotnet test --filter Category=notion-live</c> selects the suite.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NotionLiveFactAttribute : FactAttribute
{
    public NotionLiveFactAttribute()
    {
        if (NotionLiveEnv.NotConfigured)
            Skip = $"notion-live: {NotionLiveEnv.TokenVar} and {NotionLiveEnv.ParentVar} unset — skipping the live suite.";
    }
}
