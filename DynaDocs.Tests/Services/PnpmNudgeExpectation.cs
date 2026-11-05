namespace DynaDocs.Tests.Services;

/// <summary>
/// DYD-553: this repository's pnpm warn nudges, restored from LC's Windows-era npm nudge
/// (TestData/downstream-lc/dydo.json, 5f342581) with the cd614a81 wording and scoped to plain
/// <c>npm install</c>, <c>npm i</c> and <c>npx</c>. <see cref="DydoJsonEntries"/> is the block the user
/// pastes into the repository's dydo.json "nudges" array (HITL DYD-643), byte for byte; agents never
/// write dydo.json. These are not shipped defaults, because defaults reach npm projects too.
/// A newline separates commands as <c>;</c> does, so a multi-line call's npm line is matched
/// too, as the install nudge matches it.
/// </summary>
internal static class PnpmNudgeExpectation
{
    public const string NpmPattern = """(?:^|[;&|\n]\s*)npm\s+((?:install|i)(?![\w-]).*)""";

    public const string NpmMessage = "Use pnpm, not npm. Try: pnpm $1";

    public const string NpxPattern = """(?:^|[;&|\n]\s*)npx\s+(?!-)(.*)""";

    public const string NpxMessage = "Use pnpm, not npx. Try: pnpm exec $1";

    public const string DydoJsonEntries = """
            {
              "pattern": "(?:^|[;&|\\n]\\s*)npm\\s+((?:install|i)(?![\\w-]).*)",
              "message": "Use pnpm, not npm. Try: pnpm $1",
              "severity": "warn"
            },
            {
              "pattern": "(?:^|[;&|\\n]\\s*)npx\\s+(?!-)(.*)",
              "message": "Use pnpm, not npx. Try: pnpm exec $1",
              "severity": "warn"
            }
        """;
}
