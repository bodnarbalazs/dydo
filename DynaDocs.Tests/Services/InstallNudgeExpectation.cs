namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

/// <summary>
/// DYD-499: the library-install warn nudge. The expectation below is the interim per-project copy
/// (dydo.json of dydo#223, LC#543, HRSI#6, TheGame#57), byte for byte, so the default never
/// duplicates it: <see cref="ConfigFactory.SameNudge"/> keys on the pattern.
/// </summary>
internal static class InstallNudgeExpectation
{
    public const string Pattern = """(?:^|[;&|\n(][ \t]*)(?:sudo[ \t]+(?:-\S+[ \t]+)*)?(?:env[ \t]+)?(?:[A-Za-z_][A-Za-z0-9_]*=[^\s;&|]*[ \t]+)*((?:pnpm|npm|yarn|bun)[ \t]+(?:(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])[ \t]+[^\s;&|<>]+[ \t]+|(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])=[^\s;&|<>]+[ \t]+|(?:-r|--recursive|-w|--workspace-root)[ \t]+)*(?:add|dlx|create|update|up|upgrade|link)\b|(?:pnpm|npm|yarn|bun)[ \t]+(?:(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])[ \t]+[^\s;&|<>]+[ \t]+|(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])=[^\s;&|<>]+[ \t]+|(?:-r|--recursive|-w|--workspace-root)[ \t]+)*(?:install|i)\b(?=(?:[ \t]+(?:(?:--(?:filter|dir|prefix|workspace|reporter|config|registry)|-[CFw])[ \t]+[^\s;&|<>]+|-(?!-?(?:filter|dir|prefix|workspace|reporter|config|registry)\b)(?![CFw](?:[ \t]|$))[^\s;&|<>]+))*[ \t]+(?![0-9]*[<>])[^-\s;&|<>()#\'"])|(?:npx|pnpx|bunx)[ \t]+(?:-y|--yes|-p|--package)\b|(?:[\w.~/-]*/)?uv[ \t]+(?:add|remove|pip[ \t]+install|tool[ \t]+install)\b|(?:[\w.~/-]*/)?uv[ \t]+run\b(?=[^\n;&|]*[ \t]--with\b)|(?:[\w.~/-]*/)?(?:uvx|pipx)\b|(?:[\w.~/-]*/)?(?:pip[0-9.]*[ \t]+install|(?:python[0-9.]*|py)[ \t]+-m[ \t]+pip[ \t]+install)\b|dotnet[ \t]+(?:add\b|tool[ \t]+(?:install|update)\b|workload[ \t]+install\b|new[ \t]+install\b)|(?:apt(?:-get)?[ \t]+(?:install|reinstall)|snap[ \t]+install|flatpak[ \t]+install|(?:cargo|go|gem|brew)[ \t]+install)\b)""";

    public const string Message = """Installing or adding a library or tool needs the user's permission ($1). Retry this exact command only if the user explicitly approved this install, in this conversation or in the approved plan/ticket you are executing that names it. Otherwise stop and ask the user. Restoring locked dependencies (pnpm install --frozen-lockfile, npm ci, uv sync --frozen, dotnet restore) needs no permission.""";
}
