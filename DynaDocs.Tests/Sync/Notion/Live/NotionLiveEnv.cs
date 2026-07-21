namespace DynaDocs.Tests.Sync.Notion.Live;

/// <summary>
/// The two environment variables the token-gated live suite reads (ns-9) and the one place their
/// presence is judged. BOTH unset ⇒ the suite is "not configured" and every live test skips (CI stays
/// green). Either one set but the pair incomplete ⇒ <see cref="RequireConfig"/> throws LOUDLY rather than
/// silently no-op — the sprint watch-out: <c>NotionSyncService</c> exits success on missing config, but the
/// live fixtures must NOT inherit that, so a half-configured live run fails visibly instead of quietly
/// passing zero real tests. A wrong-but-complete pair (bad token/parent) is caught later by the real API
/// call in <see cref="NotionLiveTestBase"/>, which surfaces as a test failure — also loud.
/// </summary>
internal static class NotionLiveEnv
{
    public const string TokenVar = "DYDO_NOTION_TEST_TOKEN";
    public const string ParentVar = "DYDO_NOTION_TEST_PARENT";

    public static (string? Token, string? Parent) Read() =>
        (Environment.GetEnvironmentVariable(TokenVar), Environment.GetEnvironmentVariable(ParentVar));

    /// <summary>True only when NEITHER var is set — the single skip condition the live suite honours. The
    /// <see cref="NotionLiveFactAttribute"/> reads this to decide whether a live test is reported skipped.</summary>
    public static bool NotConfigured
    {
        get
        {
            var (token, parent) = Read();
            return string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(parent);
        }
    }

    /// <summary>Resolve the token+parent for a live run, or throw. A partial configuration (exactly one var
    /// set) is a hard error, never a skip: the operator asked for a live run and the suite must not silently
    /// do nothing. Both-unset also throws here, but that path is unreachable in practice — a both-unset run
    /// is skipped by <see cref="NotionLiveFactAttribute"/> before any fixture is constructed.</summary>
    public static (string Token, string Parent) RequireConfig()
    {
        var (token, parent) = Read();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(parent))
            throw new InvalidOperationException(
                $"notion-live: refusing to run against a partial configuration — set BOTH {TokenVar} and {ParentVar} "
                + "(or neither, to skip the live suite). Half-configured runs must fail loudly, not silently pass.");
        return (token, parent);
    }
}
