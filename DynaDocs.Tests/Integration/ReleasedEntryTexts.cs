namespace DynaDocs.Tests.Integration;

/// <summary>
/// CLAUDE.md bodies that released dydo versions wrote, copied from the tags (DYD-372).
/// Each sits under a title line that held the folder name at generation time.
/// </summary>
internal static class ReleasedEntryTexts
{
    // v1.1.0 .. v2.0.15: TemplateGenerator.GenerateClaudeMd.
    public const string V1 = """
        This project uses **DynaDocs** for documentation and AI agent workflow management.
        Before starting any task, read [dydo/index.md](dydo/index.md) and follow the onboarding process.
        """;

    // v2.1.0 .. v2.2.3: Templates/entry-point.template.md, blob b9d23af4.
    public const string V2_1 = """
        <!-- Describe the project here in 2-3 sentences: what it is, who it's for. -->

        This project's knowledge lives in [dydo/](dydo/index.md) — the docs are the source of
        truth. Read what bears on your task before acting.

        Work happens through skills. Whatever you're about to do, a skill most likely defines how it's done
        here — invoke it and follow it.
        """;

    // v3.0.0 .. v3.4.1: Scaffold/entry-point.md, blob 93c0ddb0.
    public const string V3_0 = """
        Linear owns live work. Git and dydo own durable knowledge and evidence.
        [dydo/index.md](dydo/index.md) is the root of the documentation.

        ## Sort yourself

        - Opened or spawned with a role? Keep it: an officer holds its Project, Issue or board to the end; crew does its one job and returns.
        - Thinking with the human? Load `co-thinker`.
        - Load any other skill when the work needs it; each skill names the next.

        The human hands out the officer roles by name: an issue-captain by default, an admiral only when invoked.
        """;

    // 1629c8a0, the 3.5 base tree: Scaffold/entry-point.md, blob f904f57f.
    public const string V3_5Base = """
        Linear owns live work. Git and dydo own durable knowledge and evidence.
        [dydo/index.md](dydo/index.md) is the root of the documentation.

        ## Sort yourself

        - Opened or spawned with a role? Keep it: an officer holds its Project, ticket or board to the end; crew does its one job and returns.
        - Thinking with the human? Load `co-thinker`.
        - Load any other skill when the work needs it; each skill names the next.

        The human hands out the officer roles by name: a ticket-captain by default, an admiral only when invoked.
        """;

    /// <summary>A generated file: title, blank line, body, with the given line endings.</summary>
    public static string Generated(string title, string body, string newline) =>
        $"# {title}\n\n{body}".ReplaceLineEndings(newline);
}
