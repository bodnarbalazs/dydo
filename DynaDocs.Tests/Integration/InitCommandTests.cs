using System.Text.Json;
using System.Text.Json.Nodes;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

[Collection("Integration")]
public class InitCommandTests : IntegrationTestBase
{
    private const string CustomHooks = """
        {
          "hooks": {
            "PreToolUse": [{
              "matcher": "CustomTool",
              "hooks": [{ "type": "command", "command": "echo custom" }]
            }],
            "PostToolUse": [{
              "matcher": "AnyTool",
              "hooks": [{ "type": "command", "command": "echo post" }]
            }]
          },
          "otherSetting": true
        }
        """;

    [Fact]
    public async Task Join_PostWorkFailure_PreservesOriginalConfigBytes()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        var configPath = Path.Combine(TestDir, "dydo.json");
        AddLegacyModels(configPath);
        var original = File.ReadAllBytes(configPath);

        var (exitCode, stdout, stderr) = ConsoleCapture.All(() =>
            InitCommand.ExecuteJoin("codex", () => throw new IOException("injected post-work failure")));

        Assert.Equal(ExitCodes.ToolError, exitCode);
        Assert.Contains("injected post-work failure", stderr);
        // Hook wiring and the integration report precede the commit seam.
        AssertFileExists(".codex/hooks.json");
        Assert.Contains("Codex hooks configured", stdout);
        Assert.Contains("Recorded integration(s) in dydo.json: codex", stdout);
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Contains("\"models\"", File.ReadAllText(configPath));
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    [Fact]
    public async Task Join_AlreadyRecordedIntegration_LeavesConfigBytesUntouched()
    {
        (await InitProjectAsync("codex")).AssertSuccess();
        var configPath = Path.Combine(TestDir, "dydo.json");
        AddLegacyModels(configPath);
        var original = File.ReadAllBytes(configPath);

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        Assert.DoesNotContain("Recorded integration(s)", result.Stdout);
        Assert.Equal(original, File.ReadAllBytes(configPath));
        Assert.Contains("\"models\"", File.ReadAllText(configPath));
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    #region Adopted dydo version

    [Fact]
    public async Task Init_Fresh_RecordsRunningCliVersion()
    {
        (await InitProjectAsync("none")).AssertSuccess();

        Assert.Equal(DydoVersion.Current, ReadDydoVersion());
        Assert.Matches(@"^\d+\.\d+\.\d+", DydoVersion.Current);
    }

    [Fact]
    public async Task Join_ExistingDydoVersion_StaysUntouched()
    {
        (await InitProjectAsync("codex")).AssertSuccess();
        SetDydoVersion("1.2.3");

        (await JoinProjectAsync("codex")).AssertSuccess();

        Assert.Equal("1.2.3", ReadDydoVersion());
    }

    [Fact]
    public async Task Join_RecordingNewIntegration_PreservesDydoVersion()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        SetDydoVersion("1.2.3");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        Assert.Contains("Recorded integration(s) in dydo.json: codex", result.Stdout);
        Assert.Equal("1.2.3", ReadDydoVersion());
    }

    [Fact]
    public async Task Join_WithoutDydoVersion_WritesNone()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        SetDydoVersion(null);

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        Assert.Contains("Recorded integration(s) in dydo.json: codex", result.Stdout);
        Assert.False(ReadConfigJson().ContainsKey("dydoVersion"));
    }

    private JsonObject ReadConfigJson() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(TestDir, "dydo.json")))!.AsObject();

    private string? ReadDydoVersion() => ReadConfigJson()["dydoVersion"]?.GetValue<string>();

    private void SetDydoVersion(string? version)
    {
        var raw = ReadConfigJson();
        raw.Remove("dydoVersion");
        if (version != null)
            raw["dydoVersion"] = version;
        File.WriteAllText(Path.Combine(TestDir, "dydo.json"), raw.ToJsonString());
    }

    #endregion

    private static void AddLegacyModels(string configPath)
    {
        var raw = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        raw["models"] = JsonNode.Parse("""{"agents":{"reviewer":"strong"},"tiers":{"openai":{"strong":"legacy"}}}""");
        raw["unrelated"] = JsonNode.Parse("""{"sentinel":[3,1,4]}""");
        File.WriteAllText(configPath, raw.ToJsonString());
    }

    #region Init None

    [Fact]
    public async Task Init_None_CreatesBasicStructure()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileExists("dydo.json");
        AssertFileExists("AGENTS.md");
        AssertFileNotExists("CLAUDE.md");
        AssertDirectoryExists("dydo");
        AssertDirectoryExists("dydo/understand");
        AssertDirectoryExists("dydo/guides");
        AssertDirectoryExists("dydo/reference");
        AssertDirectoryExists("dydo/project");
        AssertDirectoryExists("dydo/agents");
    }

    [Fact]
    public async Task Init_None_CreatesFoundationDocs()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileExists("dydo/index.md");
        AssertFileExists("dydo/welcome.md");
        AssertFileExists("dydo/files-off-limits.md");
        AssertFileExists("dydo/understand/about.md");
        AssertFileExists("dydo/understand/architecture.md");
        AssertFileExists("dydo/guides/coding-standards.md");
        AssertFileExists("dydo/reference/writing-docs.md");
    }

    [Fact]
    public async Task Init_None_CreatesEmptyAgentsWorkspaceRoot()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();

        AssertDirectoryExists("dydo/agents");
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/agents/sample")));
    }

    [Fact]
    public async Task Init_None_CreatesCorrectConfig()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileContains("dydo.json", "\"version\": 1");
        // The nested models.agents tier bindings are a different key and must not trip this.
        var config = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile("dydo.json")));
        Assert.False(config.ContainsKey("agents"));
    }

    [Fact]
    public async Task Init_None_NextStepsDoNotTeachARemovedConfigKey()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        Assert.DoesNotContain("\"paths\"", result.Stdout);
        Assert.DoesNotContain("Source and test paths", result.Stdout);
    }

    [Fact]
    public async Task Init_None_UpdatesGitignore()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileExists(".gitignore");
        AssertFileContains(".gitignore", "dydo/agents/");
        AssertFileContains(".gitignore", "dydo/_system/.local/");
    }

    [Fact]
    public async Task Init_None_ScaffoldsSystemLocalDir()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertDirectoryExists("dydo/_system/.local");
    }

    [Fact]
    public async Task Init_None_CreatesDurableProjectKnowledgeWithoutLegacyWorkHierarchy()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();

        foreach (var retired in new[] { "tasks", "issues", "campaigns", "sprints", "slices", "backlog" })
            Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo", "project", retired)));

        AssertFileExists("dydo/project/future-features/_future-features.md");
        AssertFileContains("dydo/project/future-features/_future-features.md",
            "repo-native ideas, not scheduled work");
        AssertFileContains("dydo/project/future-features/_future-features.md",
            "User promotion creates exactly one appropriately shaped Linear Initiative, Project, or ticket");
        AssertFileContains("dydo/project/future-features/_future-features.md",
            "only in Linear; the FutureFeature remains provenance");

        var futureFeatureFiles = Directory.GetFiles(
                Path.Combine(TestDir, "dydo", "project", "future-features"), "*.md")
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToArray();
        Assert.Equal(new[] { "_future-features.md" }, futureFeatureFiles);

        AssertFileNotExists("dydo/project/decisions/_index.md");
        AssertFileExists("dydo/project/decisions/_decisions.md");

        AssertFileNotExists("dydo/project/changelog/_index.md");
        AssertFileExists("dydo/project/changelog/_changelog.md");

        AssertFileNotExists("dydo/project/pitfalls/_index.md");
        AssertFileExists("dydo/project/pitfalls/_pitfalls.md");
    }

    [Fact]
    public async Task Init_None_MetaFilesHaveMeaningfulContent()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();

        AssertFileContains("dydo/project/decisions/_decisions.md", "Decision Records");
        AssertFileContains("dydo/project/decisions/_decisions.md", "proposed");
        AssertFileContains("dydo/project/decisions/_decisions.md", "accepted");

        AssertFileContains("dydo/project/changelog/_changelog.md", "Chronological record");
        AssertFileContains("dydo/project/changelog/_changelog.md", "Files Changed");

        AssertFileContains("dydo/project/pitfalls/_pitfalls.md", "Known gotchas");
        AssertFileContains("dydo/project/pitfalls/_pitfalls.md", "Symptoms");
    }

    #endregion

    #region Init Claude

    [Fact]
    public async Task Init_Claude_CreatesHooksConfig()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        // The guard is committed project wiring; the local file carries only the allow list.
        AssertFileContains(".claude/settings.json", "PreToolUse");
        AssertFileContains(".claude/settings.json", "dydo guard");
        AssertFileExists(".claude/settings.local.json");
        Assert.DoesNotContain("dydo guard", ReadFile(".claude/settings.local.json"));
    }

    [Fact]
    public async Task Init_Codex_CreatesProjectAndHooksWithoutClaudeSettings()
    {
        var result = await InitProjectAsync("codex");

        result.AssertSuccess();
        AssertFileExists("dydo.json");
        AssertFileExists("dydo/index.md");
        AssertFileExists("AGENTS.md");
        AssertFileExists(".codex/hooks.json");
        AssertFileContains("AGENTS.md", "dydo/index.md");
        AssertFileContains("dydo.json", "\"codex\": true");
        result.AssertStdoutContains("AGENTS.md -> dydo/index.md");
        AssertFileNotExists(".claude/settings.local.json");
    }

    [Fact]
    public async Task Init_All_WritesHostAgentSettings()
    {
        var result = await InitProjectAsync("all");

        result.AssertSuccess();
        var claude = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".claude/settings.json")));
        var env = Assert.IsType<JsonObject>(claude["env"]);
        Assert.Equal("3", env["CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH"]?.GetValue<string>());
        Assert.False(claude["autoMemoryEnabled"]?.GetValue<bool>());
        Assert.Equal("[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n\n[features]\nmemories = false\n",
            ReadFile(".codex/config.toml"));
        Assert.DoesNotContain("Turned native memory off", result.Stdout);
    }

    [Fact]
    public async Task Join_All_TurnsNativeMemoryOffOverTrueAndSaysSo()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".claude/settings.json", "{ \"other\": \"keep\", \"autoMemoryEnabled\": true }");
        WriteFile(".codex/config.toml",
            "# keep\n[features]\nother = true\nmemories = true  # was on\n\n[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n");

        var result = await JoinProjectAsync("all");

        result.AssertSuccess();
        var claude = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".claude/settings.json")));
        Assert.False(claude["autoMemoryEnabled"]?.GetValue<bool>());
        Assert.Equal("keep", claude["other"]?.GetValue<string>());
        Assert.Equal(
            "# keep\n[features]\nother = true\nmemories = false  # was on\n\n[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n",
            ReadFile(".codex/config.toml"));
        result.AssertStdoutContains("Turned native memory off: .claude/settings.json had autoMemoryEnabled = true");
        result.AssertStdoutContains("Turned native memory off: .codex/config.toml had memories = true  # was on");
    }

    [Fact]
    public async Task Join_Codex_AddsMemoriesIntoExistingFeaturesTable()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/config.toml", "[features]\nother = true\n\n[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        Assert.Equal(
            "[features]\nother = true\nmemories = false\n\n[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n",
            ReadFile(".codex/config.toml"));
        Assert.DoesNotContain("Turned native memory off", result.Stdout);
    }

    [Theory]
    [InlineData("features = { memories = true }\n")]
    [InlineData("features.memories = true\n")]
    [InlineData("[features]\n\"memories\" = true\n")]
    public async Task Init_Codex_RejectsMemoriesFormItCannotEditBeforeAnySideEffect(string toml)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/config.toml", toml);
        var original = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        var result = await InitProjectAsync("codex");

        result.AssertExitCode(2);
        result.AssertStderrContains(".codex/config.toml");
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        AssertFileNotExists("dydo.json");
    }

    [Fact]
    public async Task Init_HostAgentSettings_PreserveSatisfyingValuesAndRepeatBytes()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".claude/settings.json", "{\n  \"other\": true,\n  \"env\": { \"CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH\": \"4\" },\n  \"autoMemoryEnabled\": false\n}\n");
        WriteFile(".codex/config.toml", "# retain this\ncustom = true\n\n[agents]\nmax_depth = +5\nmax_concurrent_threads_per_session = +20\n\n[features]\nmemories = false\n");
        var claudeOriginal = ReadFile(".claude/settings.json");
        var codex = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        (await InitProjectAsync("all")).AssertSuccess();
        // The satisfying values stay as written; the only addition is the committed guard hook.
        var claudeAfterInit = ReadFile(".claude/settings.json");
        PreservedText.AssertLinesKeptInOrder(claudeOriginal, claudeAfterInit);
        var preToolUse = Assert.IsType<JsonArray>(JsonNode.Parse(claudeAfterInit)?["hooks"]?["PreToolUse"]);
        Assert.Equal(1, HookTestAssertions.Count(preToolUse, "dydo guard --host claude"));
        Assert.Equal(codex, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        var claude = File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json"));

        (await JoinProjectAsync("all")).AssertSuccess();
        Assert.Equal(claude, File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json")));
        Assert.Equal(codex, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
    }

    [Fact]
    public async Task Join_Codex_BomAgentsTable_IsReadAndTheBomKept()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(".codex/config.toml", "﻿[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        Assert.Equal("﻿[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n\n[features]\nmemories = false\n",
            PreservedText.Read(Path.Combine(TestDir, ".codex/config.toml")));
    }

    [Fact]
    public async Task Join_Codex_BomFeaturesTable_GetsMemoriesInsideItsOneTable()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(".codex/config.toml", "﻿[features]\nother = true\n");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        Assert.Equal("﻿[features]\nother = true\nmemories = false\n\n[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n",
            PreservedText.Read(Path.Combine(TestDir, ".codex/config.toml")));
    }

    [Theory]
    [InlineData("\"03\"")]
    [InlineData("\"+3\"")]
    [InlineData("\" 3\"")]
    [InlineData("\"2\"")]
    [InlineData("3")]
    public async Task Init_ClaudeHostSetting_RejectsNoncanonicalOrIncompatibleValue(string value)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.json", $$"""{ "env": { "CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH": {{value}} } }""");
        var original = File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json"));

        var result = await InitProjectAsync("claude");

        result.AssertExitCode(2);
        result.AssertStderrContains(".claude/settings.json");
        result.AssertStderrContains("CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH");
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json")));
        AssertFileNotExists("dydo.json");
        AssertFileNotExists(".claude/settings.local.json");
    }

    [Theory]
    [InlineData("max_depth = 2\nmax_concurrent_threads_per_session = 16")]
    [InlineData("max_depth = 3\nmax_concurrent_threads_per_session = 15")]
    [InlineData("enabled = false\nmax_depth = 3\nmax_concurrent_threads_per_session = 16")]
    [InlineData("enabled = \"true\"\nmax_depth = 3\nmax_concurrent_threads_per_session = 16")]
    [InlineData("max_depth = \"3\"\nmax_concurrent_threads_per_session = 16")]
    [InlineData("max_depth = 3.0\nmax_concurrent_threads_per_session = 16")]
    [InlineData("max_depth = true\nmax_concurrent_threads_per_session = 16")]
    public async Task Init_CodexHostSetting_RejectsConflictWithoutPartialInit(string agents)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/config.toml", "[agents]\n" + agents + "\n");
        var original = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        var result = await InitProjectAsync("codex");

        result.AssertExitCode(2);
        result.AssertStderrContains(".codex/config.toml");
        result.AssertStderrContains("agents");
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        AssertFileNotExists("dydo.json");
        AssertFileNotExists(".codex/hooks.json");
        AssertFileNotExists("AGENTS.md");
    }

    [Theory]
    [InlineData("[agents]\nmax_depth = 3\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n")]
    [InlineData("agents = { max_depth = 3, max_concurrent_threads_per_session = 16 }\n")]
    [InlineData("agents = 3\n")]
    [InlineData("agents = []\n")]
    [InlineData("[[agents]]\nmax_depth = 3\n")]
    [InlineData("agents.max_depth = 3\n")]
    [InlineData("[ agents ]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n")]
    [InlineData("[\"agents\"]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n")]
    public async Task Init_CodexHostSetting_RejectsAmbiguousTomlBeforePartialInit(string toml)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/config.toml", toml);
        var original = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        var result = await InitProjectAsync("codex");

        result.AssertExitCode(2);
        result.AssertStderrContains(".codex/config.toml");
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        AssertFileNotExists("dydo.json");
        AssertFileNotExists(".codex/hooks.json");
        AssertFileNotExists("AGENTS.md");
    }

    [Theory]
    [InlineData("{ \"env\": null }")]
    [InlineData("{ \"env\": { \"CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH\": null } }")]
    public async Task Init_ClaudeHostSetting_RejectsNullManagedMembersBeforePartialInit(string json)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.json", json);
        var original = File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json"));

        var result = await InitProjectAsync("claude");

        result.AssertExitCode(2);
        result.AssertStderrContains(".claude/settings.json");
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json")));
        AssertFileNotExists("dydo.json");
        AssertFileNotExists(".claude/settings.local.json");
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public async Task Init_All_PreflightsBothHostsBeforeAnyProjectMutation(string invalidHost)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".claude/settings.json", invalidHost == "claude"
            ? "{ \"env\": { \"CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH\": \"02\" } }"
            : "{ \"env\": { \"custom\": \"preserve\" } }");
        WriteFile(".codex/config.toml", invalidHost == "codex"
            ? "[agents\nmax_depth = 3\n"
            : "# existing\n[agents]\nmax_depth = 3\nmax_concurrent_threads_per_session = 16\n");
        var claude = File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json"));
        var codex = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        var result = await InitProjectAsync("all");

        result.AssertExitCode(2);
        Assert.Equal(claude, File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json")));
        Assert.Equal(codex, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        AssertFileNotExists("dydo.json");
        AssertFileNotExists("CLAUDE.md");
        AssertFileNotExists("AGENTS.md");
        AssertFileNotExists(".claude/settings.local.json");
        AssertFileNotExists(".codex/hooks.json");
        AssertFileNotExists(".gitignore");
    }

    [Fact]
    public async Task Join_All_PreflightFailurePreservesExistingProjectAndBothHosts()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".claude/settings.json", "{ \"env\": { \"custom\": \"preserve\" } }");
        WriteFile(".codex/config.toml", "[agents]\nmax_depth = 2\nmax_concurrent_threads_per_session = 16\n");
        // A generated CLAUDE.md that a successful join would remove must survive the failed one.
        WriteFile("CLAUDE.md", ReadFile("AGENTS.md"));
        var config = File.ReadAllBytes(Path.Combine(TestDir, "dydo.json"));
        var claude = File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json"));
        var codex = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));
        var preserved = Snapshot("AGENTS.md", "CLAUDE.md");

        var result = await JoinProjectAsync("all");

        result.AssertExitCode(2);
        Assert.Equal(config, File.ReadAllBytes(Path.Combine(TestDir, "dydo.json")));
        Assert.Equal(claude, File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json")));
        Assert.Equal(codex, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        AssertUnchanged(preserved);
        AssertFileNotExists(".claude/settings.local.json");
        AssertFileNotExists(".codex/hooks.json");
    }

    [Theory]
    [InlineData("unrelated = true\nunrelated = false\n")]
    [InlineData("unrelated = \"unterminated\n")]
    [InlineData("[agents]\nmax_depth = 9223372036854775808\n")]
    public async Task Init_All_MalformedCodexTomlPreservesBothHostsAndAllSideEffects(string toml)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".claude/settings.json", "{ \"env\": { \"custom\": \"preserve\" } }");
        WriteFile(".codex/config.toml", toml);
        var claude = File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json"));
        var codex = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        var result = await InitProjectAsync("all");

        result.AssertExitCode(2);
        result.AssertStderrContains(".codex/config.toml");
        result.AssertStderrContains("malformed");
        Assert.Equal(claude, File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json")));
        Assert.Equal(codex, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        AssertFileNotExists("dydo.json");
        AssertFileNotExists("CLAUDE.md");
        AssertFileNotExists("AGENTS.md");
        AssertFileNotExists(".claude/settings.local.json");
        AssertFileNotExists(".codex/hooks.json");
        AssertFileNotExists(".gitignore");
    }

    [Theory]
    [InlineData("unrelated = true\nunrelated = false\n")]
    [InlineData("unrelated = \"unterminated\n")]
    [InlineData("[agents]\nmax_depth = 9223372036854775808\n")]
    public async Task Join_All_MalformedCodexTomlPreservesBothHostsAndAllSideEffects(string toml)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".claude/settings.json", "{ \"env\": { \"custom\": \"preserve\" } }");
        WriteFile(".codex/config.toml", toml);
        var preserved = Snapshot("dydo.json", ".gitignore", "AGENTS.md",
                                 ".claude/settings.json", ".codex/config.toml");

        var result = await JoinProjectAsync("all");

        result.AssertExitCode(2);
        result.AssertStderrContains(".codex/config.toml");
        result.AssertStderrContains("malformed");
        AssertUnchanged(preserved);
        AssertFileNotExists("CLAUDE.md");
        AssertFileNotExists(".claude/settings.local.json");
        AssertFileNotExists(".codex/hooks.json");
    }

    [Theory]
    [InlineData("agents . max_depth = 3\n")]
    [InlineData("\"agents\".max_depth = 3\n")]
    public async Task Init_All_RejectsSemanticDottedAgentsBeforeAnySideEffect(string toml)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".claude/settings.json", "{ \"env\": { \"custom\": \"preserve\" } }");
        WriteFile(".codex/config.toml", toml);
        var claude = File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json"));
        var codex = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        var result = await InitProjectAsync("all");

        result.AssertExitCode(2);
        result.AssertStderrContains(".codex/config.toml");
        Assert.Equal(claude, File.ReadAllBytes(Path.Combine(TestDir, ".claude/settings.json")));
        Assert.Equal(codex, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        AssertFileNotExists("dydo.json");
        AssertFileNotExists("CLAUDE.md");
        AssertFileNotExists("AGENTS.md");
        AssertFileNotExists(".claude/settings.local.json");
        AssertFileNotExists(".codex/hooks.json");
        AssertFileNotExists(".gitignore");
    }

    [Theory]
    [InlineData("agents . max_depth = 3\n")]
    [InlineData("\"agents\".max_depth = 3\n")]
    public async Task Join_All_RejectsSemanticDottedAgentsBeforeAnySideEffect(string toml)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".claude/settings.json", "{ \"env\": { \"custom\": \"preserve\" } }");
        WriteFile(".codex/config.toml", toml);
        var preserved = Snapshot("dydo.json", ".gitignore", "AGENTS.md",
                                 ".claude/settings.json", ".codex/config.toml");

        var result = await JoinProjectAsync("all");

        result.AssertExitCode(2);
        result.AssertStderrContains(".codex/config.toml");
        AssertUnchanged(preserved);
        AssertFileNotExists("CLAUDE.md");
        AssertFileNotExists(".claude/settings.local.json");
        AssertFileNotExists(".codex/hooks.json");
    }

    [Fact]
    public async Task Init_Codex_PreservesUnrelatedAgentsHistoryArrayAndRepeatBytes()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/config.toml", "[[agents_history]]\nname = \"retain\"\n");

        (await InitProjectAsync("codex")).AssertSuccess();
        var config = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        (await JoinProjectAsync("codex")).AssertSuccess();
        Assert.Equal(config, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        Assert.Contains("[[agents_history]]", ReadFile(".codex/config.toml"));
    }

    [Theory]
    [InlineData("3", "16")]
    [InlineData("+3", "+16")]
    [InlineData("0x3", "0x10")]
    [InlineData("0o3", "0o20")]
    [InlineData("0b11", "0b10000")]
    public async Task Init_Codex_RetainsIntegerAgentCapacityValuesAndRepeatBytes(string depth, string concurrency)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/config.toml", $"[agents]\nmax_depth = {depth}\nmax_concurrent_threads_per_session = {concurrency}\n\n[features]\nmemories = false\n");
        var original = File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml"));

        (await InitProjectAsync("codex")).AssertSuccess();
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
        (await JoinProjectAsync("codex")).AssertSuccess();
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(TestDir, ".codex/config.toml")));
    }

    [Fact]
    public async Task Init_Codex_HooksContainPreToolUseDydoGuard()
    {
        var result = await InitProjectAsync("codex");

        result.AssertSuccess();

        var settings = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".codex/hooks.json")));
        Assert.Null(settings["PreToolUse"]);

        var hooks = Assert.IsType<JsonObject>(settings["hooks"]);
        var preToolUse = Assert.IsType<JsonArray>(hooks["PreToolUse"]);
        var preToolUseEntry = Assert.Single(preToolUse);
        Assert.NotNull(preToolUseEntry);
        var matcher = preToolUseEntry["matcher"]?.GetValue<string>();
        // The documented Codex tool names first, then the shell names from issue 0295 (without
        // them the guard never sees Codex shell commands, so off-limits and dangerous-bash don't
        // bind on that lane). Claude-only names are left out: Codex never emits them.
        Assert.Equal("Bash|apply_patch|Edit|Write|Agent|shell_command|exec|local_shell|unified_exec", matcher);

        var guardHooks = Assert.IsType<JsonArray>(preToolUseEntry["hooks"]);
        var guardHook = Assert.Single(guardHooks);
        Assert.NotNull(guardHook);
        Assert.Equal("command", guardHook["type"]?.GetValue<string>());
        Assert.Equal("dydo guard", guardHook["command"]?.GetValue<string>());

        Assert.False(hooks.ContainsKey("Stop"));
    }

    [Fact]
    public async Task Init_Codex_PreservesExistingHooks()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/hooks.json", CustomHooks);

        var result = await InitProjectAsync("codex");

        result.AssertSuccess();
        var content = ReadFile(".codex/hooks.json");
        Assert.Contains("CustomTool", content);
        Assert.Contains("echo custom", content);
        Assert.Contains("PostToolUse", content);
        Assert.Contains("echo post", content);
        Assert.Contains("otherSetting", content);
        Assert.Contains("dydo guard", content);
        Assert.DoesNotContain("dydo guard --stop", content);
    }

    [Fact]
    public async Task Init_Codex_PreservesCustomHooksWhenRemovingDydoHooks()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/hooks.json", """
            {
              "hooks": {
                "PreToolUse": [
                  {
                    "matcher": "CustomSubstring",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo before dydo guard after"
                      }
                    ]
                  },
                  {
                    "matcher": "Mixed",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "dydo guard"
                      },
                      {
                        "type": "command",
                        "command": "echo custom"
                      }
                    ]
                  },
                  {
                    "matcher": "ManagedOnly",
                    "hooks": [
                      {
                        "type": "command",
                        "command": "dydo guard"
                      }
                    ]
                  }
                ],
                "Stop": [
                  {
                    "hooks": [
                      {
                        "type": "command",
                        "command": "dydo guard --stop"
                      },
                      {
                        "type": "command",
                        "command": "echo stop custom"
                      }
                    ]
                  },
                  {
                    "hooks": [
                      {
                        "type": "command",
                        "command": "echo before dydo guard --stop after"
                      }
                    ]
                  }
                ]
              }
            }
            """);

        var result = await InitProjectAsync("codex");

        result.AssertSuccess();

        var settings = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".codex/hooks.json")));
        var hooks = Assert.IsType<JsonObject>(settings["hooks"]);
        var preToolUse = Assert.IsType<JsonArray>(hooks["PreToolUse"]);
        Assert.Contains(preToolUse, entry =>
            entry?["matcher"]?.GetValue<string>() == "CustomSubstring" &&
            HookTestAssertions.Commands(entry).Contains("echo before dydo guard after"));
        Assert.Contains(preToolUse, entry =>
            entry?["matcher"]?.GetValue<string>() == "Mixed" &&
            HookTestAssertions.Commands(entry).SequenceEqual(["echo custom"]));
        Assert.DoesNotContain(preToolUse, entry =>
            entry?["matcher"]?.GetValue<string>() == "ManagedOnly");
        Assert.Equal(1, HookTestAssertions.Count(preToolUse, "dydo guard"));

        var stop = Assert.IsType<JsonArray>(hooks["Stop"]);
        Assert.Contains(stop, entry => HookTestAssertions.Commands(entry).SequenceEqual(["echo stop custom"]));
        Assert.Contains(stop, entry => HookTestAssertions.Commands(entry).Contains("echo before dydo guard --stop after"));
        Assert.Equal(0, HookTestAssertions.Count(stop, "dydo guard --stop"));
    }

    [Fact]
    public async Task Join_Codex_RemovesTheDydoStopHook_KeepingForeignStopEntries()
    {
        (await InitProjectAsync("codex")).AssertSuccess();
        WriteFile(".codex/hooks.json", """
            {
              "hooks": {
                "Stop": [
                  { "hooks": [ { "type": "command", "command": "dydo guard --stop" } ] },
                  { "hooks": [ { "type": "command", "command": "echo before dydo guard --stop after" } ] }
                ]
              }
            }
            """);

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        var hooks = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".codex/hooks.json"))?["hooks"]);
        var stop = Assert.IsType<JsonArray>(hooks["Stop"]);
        Assert.Equal(["echo before dydo guard --stop after"], HookTestAssertions.Commands(Assert.Single(stop)));
    }

    [Fact]
    public async Task Init_Codex_DoesNotDuplicateDydoHook()
    {
        await InitProjectAsync("codex");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        var content = ReadFile(".codex/hooks.json");
        Assert.Equal(1, content.Split("\"dydo guard\"").Length - 1);
        Assert.DoesNotContain("dydo guard --stop", content);
    }

    [Fact]
    public async Task Init_Claude_HooksMatchEditWriteReadBash()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.json");
        Assert.Contains("Edit|Write|Read|Bash", content);
    }

    [Fact]
    public async Task Init_Claude_MatcherIncludesPlanModeTools()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.json");
        Assert.Contains("EnterPlanMode", content);
        Assert.Contains("ExitPlanMode", content);
    }

    [Fact]
    public async Task Init_Claude_MatcherIncludesPowerShell()
    {
        // Without PowerShell in the matcher, Claude Code's PowerShell calls bypass every guard layer.
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.json");
        Assert.Contains("PowerShell", content);
    }

    [Fact]
    public async Task Init_Claude_PreservesExistingHooks()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", CustomHooks);
        WriteFile(".claude/settings.json", CustomHooks);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var local = ReadFile(".claude/settings.local.json");
        PreservedText.AssertLinesKeptInOrder(CustomHooks, local);
        Assert.DoesNotContain("dydo guard", local);

        var content = ReadFile(".claude/settings.json");
        Assert.Contains("CustomTool", content);
        Assert.Contains("echo custom", content);

        Assert.Contains("dydo guard", content);
        Assert.Contains("Edit|Write|Read|Bash", content);

        Assert.Contains("PostToolUse", content);
        Assert.Contains("echo post", content);

        Assert.Contains("otherSetting", content);
    }

    [Fact]
    public async Task Join_Claude_MovesDydosGuardOutOfTheLocalSettings_KeepingForeignHooks()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {
                "PreToolUse": [
                  { "matcher": "Edit|AskUserQuestion", "hooks": [ { "type": "command", "command": "dydo guard" } ] },
                  { "matcher": "Bash", "hooks": [ { "type": "command", "command": "dydo guard --host claude" }, { "type": "command", "command": "echo mine" } ] }
                ],
                "Stop": [ { "hooks": [ { "type": "command", "command": "dydo guard --stop" } ] } ]
              }
            }
            """);

        (await JoinProjectAsync("claude")).AssertSuccess();

        var local = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".claude/settings.local.json")));
        var localHooks = Assert.IsType<JsonObject>(local["hooks"]);
        Assert.False(localHooks.ContainsKey("Stop"));
        var localPreToolUse = Assert.IsType<JsonArray>(localHooks["PreToolUse"]);
        Assert.Equal(["echo mine"], HookTestAssertions.Commands(Assert.Single(localPreToolUse)));
        Assert.Contains("Bash(dydo:*)", ReadFile(".claude/settings.local.json"));

        var committed = Assert.IsType<JsonArray>(JsonNode.Parse(ReadFile(".claude/settings.json"))?["hooks"]?["PreToolUse"]);
        Assert.Equal(1, HookTestAssertions.Count(committed, "dydo guard --host claude"));
    }

    [Fact]
    public async Task Join_Claude_LeavesExactlyOneDydoGuardInTheCommittedSettings()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(".claude/settings.json", """
            {
              "hooks": {
                "PreToolUse": [
                  { "matcher": "Edit|AskUserQuestion", "hooks": [ { "type": "command", "command": "dydo guard" } ] },
                  { "matcher": "Bash", "hooks": [ { "type": "command", "command": "echo mine" } ] },
                  { "matcher": "Edit", "hooks": [ { "type": "command", "command": "dydo guard --host claude" } ] }
                ],
                "Stop": [ { "hooks": [ { "type": "command", "command": "dydo guard --stop" } ] } ]
              }
            }
            """);

        (await JoinProjectAsync("claude")).AssertSuccess();

        var content = ReadFile(".claude/settings.json");
        var hooks = Assert.IsType<JsonObject>(JsonNode.Parse(content)?["hooks"]);
        Assert.False(hooks.ContainsKey("Stop"));
        var preToolUse = Assert.IsType<JsonArray>(hooks["PreToolUse"]);
        Assert.Equal(2, preToolUse.Count);
        var guard = Assert.Single(preToolUse, entry => HookTestAssertions.Commands(entry).Contains("dydo guard --host claude"));
        Assert.Equal("Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit",
            guard?["matcher"]?.GetValue<string>());
        Assert.Equal(["dydo guard --host claude"], HookTestAssertions.Commands(guard));
        Assert.Equal(0, HookTestAssertions.Count(preToolUse, "dydo guard"));
        Assert.Contains("{ \"matcher\": \"Bash\", \"hooks\": [ { \"type\": \"command\", \"command\": \"echo mine\" } ] }", content);
    }

    private const string ClaudeGuard = "dydo guard --host claude";
    private const string CodexGuard = "dydo guard";

    private JsonArray JoinedPreToolUse(string file, string guard)
    {
        var preToolUse = Assert.IsType<JsonArray>(JsonNode.Parse(ReadFile(file))?["hooks"]?["PreToolUse"]);
        Assert.Equal(1, HookTestAssertions.Count(preToolUse, guard));
        Assert.Equal(1, HookTestAssertions.Count(preToolUse, CodexGuard) + HookTestAssertions.Count(preToolUse, ClaudeGuard));
        return preToolUse;
    }

    [Theory]
    [InlineData("claude", ".claude/settings.json", ClaudeGuard)]
    [InlineData("codex", ".codex/hooks.json", CodexGuard)]
    public async Task Join_MixedGuardEntry_KeepsTheForeignHookBytes(string host, string file, string guard)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(file, """{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"dydo guard"},{"type":"command","command":"echo it's"}]}]}}""");

        (await JoinProjectAsync(host)).AssertSuccess();

        Assert.Contains("""{"matcher":"Bash","hooks":[{"type":"command","command":"echo it's"}]}""", ReadFile(file));
        Assert.Equal(2, JoinedPreToolUse(file, guard).Count);
    }

    [Theory]
    [InlineData("claude", ".claude/settings.json", ClaudeGuard)]
    [InlineData("codex", ".codex/hooks.json", CodexGuard)]
    public async Task Join_GuardDuplicatesAroundForeignEntries_KeepTheForeignEntriesBytes(string host, string file, string guard)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(file, """{"hooks":{"PreToolUse":[{"matcher":"Edit","hooks":[{"type":"command","command":"dydo guard"}]}, {"matcher":"Bash", "hooks":[ {"type":"command","command":"echo it's"} ]},{"matcher":"Mixed","hooks":[{"type":"command","command":"dydo guard --host claude"},{"type":"command","command":"echo  two"}]},{"matcher":"Edit","hooks":[{"type":"command","command":"dydo guard"}]}]}}""");

        (await JoinProjectAsync(host)).AssertSuccess();

        var content = ReadFile(file);
        Assert.Contains("""{"matcher":"Bash", "hooks":[ {"type":"command","command":"echo it's"} ]}""", content);
        Assert.Contains("""{"matcher":"Mixed","hooks":[{"type":"command","command":"echo  two"}]}""", content);
        var preToolUse = JoinedPreToolUse(file, guard);
        Assert.Equal(3, preToolUse.Count);
        Assert.Equal([guard], HookTestAssertions.Commands(preToolUse[0]));
    }

    [Theory]
    [InlineData("claude", ".claude/settings.json", ClaudeGuard)]
    [InlineData("codex", ".codex/hooks.json", CodexGuard)]
    public async Task Join_AlreadyEmptyForeignEntry_StaysBeforeTheGuard(string host, string file, string guard)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(file, """{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[]},{"matcher":"Edit","hooks":[{"type":"command","command":"dydo guard"}]}]}}""");

        (await JoinProjectAsync(host)).AssertSuccess();

        Assert.Contains("""{"matcher":"Bash","hooks":[]}""", ReadFile(file));
        var preToolUse = JoinedPreToolUse(file, guard);
        Assert.Equal(2, preToolUse.Count);
        Assert.Empty(HookTestAssertions.Commands(preToolUse[0]));
        Assert.Equal([guard], HookTestAssertions.Commands(preToolUse[1]));
    }

    [Fact]
    public async Task Join_Claude_KeepsAnAlreadyEmptyForeignEntryInTheLocalSettings()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(".claude/settings.local.json", """{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[]},{"matcher":"Edit","hooks":[{"type":"command","command":"dydo guard"}]}]}}""");

        (await JoinProjectAsync("claude")).AssertSuccess();

        var local = ReadFile(".claude/settings.local.json");
        Assert.Contains("""{"matcher":"Bash","hooks":[]}""", local);
        Assert.DoesNotContain("dydo guard", local);
    }

    // A malformed hooks file starts fresh; one root followed by another is malformed.
    [Theory]
    [InlineData("claude", ".claude/settings.local.json")]
    [InlineData("codex", ".codex/hooks.json")]
    public async Task Join_TrailingRootInAHooksFile_StartsItFresh(string host, string file)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(file, "{} {}");

        (await JoinProjectAsync(host)).AssertSuccess();

        Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(file)));
    }

    [Fact]
    public async Task Join_Claude_TrailingRootInTheCommittedSettings_IsRefusedAndLeftUntouched()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(".claude/settings.json", "{} {}");

        var result = await JoinProjectAsync("claude");

        Assert.NotEqual(0, result.ExitCode);
        result.AssertStderrContains("malformed");
        Assert.Equal("{} {}", ReadFile(".claude/settings.json"));
    }

    [Fact]
    public async Task Init_Claude_DoesNotDuplicateDydoHook()
    {
        await InitProjectAsync("claude");

        // Init refuses an initialized project, so join is the re-run.
        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.json");

        var guardCount = content.Split("\"dydo guard --host claude\"").Length - 1;
        Assert.Equal(1, guardCount);
    }

    [Fact]
    public async Task Init_Claude_GuardMatcherIsExact()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var settings = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".claude/settings.json")));
        var entry = Assert.Single(Assert.IsType<JsonArray>(settings["hooks"]?["PreToolUse"]));
        Assert.Equal("Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode|PowerShell|NotebookEdit",
            entry?["matcher"]?.GetValue<string>());
    }

    [Fact]
    public async Task Init_Claude_GuardHookCarriesTheHostMarker()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var settings = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".claude/settings.json")));
        var preToolUse = Assert.IsType<JsonArray>(settings["hooks"]?["PreToolUse"]);
        Assert.Equal(1, HookTestAssertions.Count(preToolUse, "dydo guard --host claude"));
        Assert.Equal(0, HookTestAssertions.Count(preToolUse, "dydo guard"));
    }

    [Fact]
    public async Task Init_Claude_ReplacesAnUnmarkedGuardHook()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.json", """
            {
              "hooks": {
                "PreToolUse": [
                  { "matcher": "Edit", "hooks": [ { "type": "command", "command": "dydo guard" } ] }
                ]
              }
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var settings = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".claude/settings.json")));
        var preToolUse = Assert.IsType<JsonArray>(settings["hooks"]?["PreToolUse"]);
        Assert.Equal(0, HookTestAssertions.Count(preToolUse, "dydo guard"));
        Assert.Equal(1, HookTestAssertions.Count(preToolUse, "dydo guard --host claude"));
    }

    [Fact]
    public async Task Init_All_OnlyTheClaudeHookCarriesTheHostMarker()
    {
        var result = await InitProjectAsync("all");

        result.AssertSuccess();
        Assert.Contains("\"dydo guard --host claude\"", ReadFile(".claude/settings.json"));
        Assert.DoesNotContain("--host", ReadFile(".codex/hooks.json"));
    }

    [Fact]
    public async Task Init_Claude_WritesNoStopHook()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var settings = Assert.IsType<JsonObject>(JsonNode.Parse(ReadFile(".claude/settings.json")));
        Assert.False(Assert.IsType<JsonObject>(settings["hooks"]).ContainsKey("Stop"));
    }

    [Fact]
    public async Task Init_Claude_KeepsForeignStopEntries_AddingNoDydoOne()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {
                "Stop": [
                  { "hooks": [ { "type": "command", "command": "echo my-stop" } ] }
                ]
              }
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var stop = Assert.IsType<JsonArray>(JsonNode.Parse(ReadFile(".claude/settings.local.json"))?["hooks"]?["Stop"]);
        Assert.Equal(["echo my-stop"], HookTestAssertions.Commands(Assert.Single(stop)));
    }

    [Fact]
    public async Task Init_Claude_RemovesAnExistingDydoStopHook_DroppingTheEmptiedArray()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {
                "Stop": [
                  { "hooks": [ { "type": "command", "command": "dydo guard --stop" } ] }
                ]
              }
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.DoesNotContain("dydo guard --stop", content);
        Assert.DoesNotContain("\"Stop\"", content);
    }

    [Fact]
    public async Task Join_Claude_RemovesAnExistingDydoStopHook_KeepingForeignHooksInTheEntry()
    {
        (await InitProjectAsync("claude")).AssertSuccess();
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {
                "Stop": [
                  { "hooks": [
                    { "type": "command", "command": "dydo guard --stop" },
                    { "type": "command", "command": "echo my-stop" }
                  ] }
                ]
              }
            }
            """);

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        var stop = Assert.IsType<JsonArray>(JsonNode.Parse(ReadFile(".claude/settings.local.json"))?["hooks"]?["Stop"]);
        Assert.Equal(["echo my-stop"], HookTestAssertions.Commands(Assert.Single(stop)));
    }

    [Fact]
    public async Task Init_Claude_KeepsAnAlreadyEmptyStopArray()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """{ "hooks": { "Stop": [] } }""");

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var stop = Assert.IsType<JsonArray>(JsonNode.Parse(ReadFile(".claude/settings.local.json"))?["hooks"]?["Stop"]);
        Assert.Empty(stop);
    }

    [Fact]
    public async Task Init_Claude_AddsDydoAllowEntry()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(dydo:*)", content);
    }

    [Fact]
    public async Task Init_Claude_AddsPowerShellDydoAllowEntry()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("PowerShell(dydo:*)", content);
    }

    [Theory]
    [InlineData("Bash(dydo:*)")]
    [InlineData("PowerShell(dydo:*)")]
    public async Task Init_Claude_AllowMergesWithExistingEntries(string expectedEntry)
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "permissions": {
                "allow": [
                  "Bash(git:*)",
                  "Read(**)"
                ]
              }
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(git:*)", content);
        Assert.Contains("Read(**)", content);
        Assert.Contains(expectedEntry, content);
    }

    [Fact]
    public async Task Init_Claude_DoesNotDuplicateAllowEntry()
    {
        await InitProjectAsync("claude");

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        var count = content.Split("Bash(dydo:*)").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Init_Claude_DoesNotDuplicatePowerShellAllowEntry()
    {
        await InitProjectAsync("claude");

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        var count = content.Split("PowerShell(dydo:*)").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Init_Claude_AddsNarrowDydoCleanAllowEntries_OnceAcrossJoin()
    {
        (await InitProjectAsync("claude")).AssertSuccess();
        (await JoinProjectAsync("claude")).AssertSuccess();

        var allow = Assert.IsType<JsonArray>(JsonNode.Parse(ReadFile(".claude/settings.local.json"))!["permissions"]!["allow"]);
        var entries = allow.Select(entry => entry!.GetValue<string>()).ToList();
        Assert.Equal(1, entries.Count(entry => entry == "Bash(dydo clean:*)"));
        Assert.Equal(1, entries.Count(entry => entry == "PowerShell(dydo clean:*)"));
        Assert.Contains("Bash(dydo:*)", entries);
    }

    [Theory]
    [InlineData("codex")]
    [InlineData("all")]
    public async Task Init_Codex_WritesTheDydoCleanExecPolicyRule(string integration)
    {
        (await InitProjectAsync(integration)).AssertSuccess();

        Assert.Equal(InitCommand.CodexRules, ReadFile(".codex/rules/dydo.rules"));
        Assert.Contains("pattern = [\"dydo\", \"clean\"]", InitCommand.CodexRules);
        Assert.Contains("decision = \"allow\"", InitCommand.CodexRules);
    }

    [Fact]
    public async Task Join_Codex_RestoresTheDydoCleanRuleIdempotently()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        (await JoinProjectAsync("codex")).AssertSuccess();
        WriteFile(".codex/rules/dydo.rules", "stale\n");

        (await JoinProjectAsync("codex")).AssertSuccess();
        (await JoinProjectAsync("codex")).AssertSuccess();

        Assert.Equal(InitCommand.CodexRules, ReadFile(".codex/rules/dydo.rules"));
    }

    [Fact]
    public void ThisRepository_CommitsTheCodexRuleInitWrites()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, ".codex", "hooks.json")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);

        var committed = File.ReadAllText(Path.Combine(dir, ".codex", "rules", "dydo.rules"));

        Assert.Equal(InitCommand.CodexRules, committed.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Init_Claude_WritesNoCodexRule()
    {
        (await InitProjectAsync("claude")).AssertSuccess();

        AssertFileNotExists(".codex/rules/dydo.rules");
    }

    [Fact]
    public async Task Init_Claude_CreatesAllowArrayWhenMissing()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {}
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(dydo:*)", content);
        Assert.Contains("permissions", content);
    }

    [Fact]
    public async Task Init_Claude_BothShellEntriesWhenAllowArrayMissing()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, ".claude"));
        WriteFile(".claude/settings.local.json", """
            {
              "hooks": {}
            }
            """);

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        var content = ReadFile(".claude/settings.local.json");
        Assert.Contains("Bash(dydo:*)", content);
        Assert.Contains("PowerShell(dydo:*)", content);
        Assert.Contains("permissions", content);
    }

    [Fact]
    public async Task Init_DoesNotScaffoldTheRemovedRoleLayer()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        Assert.False(Directory.Exists(Path.Combine(TestDir, "dydo/_system/roles")),
            "init must not scaffold the removed dydo/_system/roles layer");
    }

    [Fact]
    public async Task Init_None_DoesNotCreateAllowEntry()
    {
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        AssertFileNotExists(".claude/settings.local.json");
        AssertFileNotExists(".codex/hooks.json");
        AssertFileExists("AGENTS.md");
    }

    #endregion

    #region Init All

    [Fact]
    public async Task Init_All_WiresBothIntegrations()
    {
        var result = await InitProjectAsync("all");

        result.AssertSuccess();
        AssertFileNotExists("CLAUDE.md");
        AssertFileExists("AGENTS.md");
        AssertFileExists(".claude/settings.local.json");
        AssertFileExists(".codex/hooks.json");
        AssertFileContains("dydo.json", "\"claude\": true");
        AssertFileContains("dydo.json", "\"codex\": true");
        result.AssertStdoutContains("AGENTS.md -> dydo/index.md");
        result.AssertStdoutContains("Claude Code 2.1.281 or later reads AGENTS.md");
    }

    [Fact]
    public async Task Init_Claude_GitignoresClaudeSettings()
    {
        // The Claude guard wiring is personal scope (issue 0303): init must gitignore it so
        // it never rides a commit, unlike the committed .codex/hooks.json.
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        AssertFileContains(".gitignore", ".claude/settings.local.json");
    }

    [Fact]
    public async Task Init_Codex_DoesNotGitignoreClaudeSettings()
    {
        var result = await InitProjectAsync("codex");

        result.AssertSuccess();
        Assert.DoesNotContain(".claude/settings.local.json", ReadFile(".gitignore"));
    }

    [Fact]
    public async Task Init_Claude_GitignoresBothSkillProjections()
    {
        // setup-skills.mjs always creates BOTH .claude/skills/ and .agents/skills/ regardless of
        // which host is wired; that's host-local discovery state, not source, and must never ride
        // a commit - for either host.
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        AssertFileContains(".gitignore", "/.claude/skills/");
        AssertFileContains(".gitignore", "/.agents/skills/");
    }

    [Fact]
    public async Task Init_Codex_GitignoresBothSkillProjections()
    {
        // setup-skills.mjs always creates BOTH .claude/skills/ and .agents/skills/ regardless of
        // which host is wired, so both must be gitignored even on a codex-only init.
        var result = await InitProjectAsync("codex");

        result.AssertSuccess();
        AssertFileContains(".gitignore", "/.agents/skills/");
        AssertFileContains(".gitignore", "/.claude/skills/");
    }

    [Fact]
    public async Task Init_All_GitignoresBothSkillProjections()
    {
        var result = await InitProjectAsync("all");

        result.AssertSuccess();
        AssertFileContains(".gitignore", "/.claude/skills/");
        AssertFileContains(".gitignore", "/.agents/skills/");
    }

    [Fact]
    public async Task Init_None_GitignoresBothSkillProjections()
    {
        // setup-skills.mjs always creates both projection folders even when no host is wired, so
        // 'none' mode must still gitignore both - otherwise whichever host a developer later uses
        // locally leaves an untracked folder easy to commit by accident.
        var result = await InitProjectAsync("none");

        result.AssertSuccess();
        var gitignore = ReadFile(".gitignore");
        Assert.Contains("/.claude/skills/", gitignore);
        Assert.Contains("/.agents/skills/", gitignore);
    }

    #endregion

    #region Init Join

    [Fact]
    public async Task Init_Join_WithoutExistingProject_Fails()
    {
        var command = InitCommand.Create();
        var result = await RunAsync(command, "none", "--join");

        result.AssertExitCode(2);
        result.AssertStderrContains("No DynaDocs project found");
    }

    [Fact]
    public async Task Init_Join_ExistingProject_Succeeds()
    {
        await InitProjectAsync("none");

        var result = await JoinProjectAsync("none");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Init_Join_Codex_RecordsIntegrationInConfig()
    {
        // Issue 0300: a claude-inited project joined with codex must end up recording BOTH.
        await InitProjectAsync("claude");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        AssertFileContains("dydo.json", "\"claude\": true");
        AssertFileContains("dydo.json", "\"codex\": true");
        AssertFileExists(".codex/hooks.json");
        AssertFileExists("AGENTS.md");
    }

    [Fact]
    public async Task Init_Join_All_WiresBothAndRecordsBoth()
    {
        await InitProjectAsync("none");

        var result = await JoinProjectAsync("all");

        result.AssertSuccess();
        AssertFileExists(".claude/settings.local.json");
        AssertFileExists(".codex/hooks.json");
        AssertFileNotExists("CLAUDE.md");
        AssertFileExists("AGENTS.md");
        AssertFileContains("dydo.json", "\"claude\": true");
        AssertFileContains("dydo.json", "\"codex\": true");
    }

    [Fact]
    public async Task Init_Join_Claude_AddsGitignoreEntry()
    {
        // A codex-inited project later joined from a Claude machine: the personal settings
        // file must get gitignored at join time (issue 0303).
        await InitProjectAsync("codex");

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        AssertFileContains(".gitignore", ".claude/settings.local.json");
    }

    [Fact]
    public async Task Init_Join_Codex_KeepsBothGitignoreEntriesAfterJoin()
    {
        // Init writes both skill-projection entries in every mode, so a later codex --join must
        // neither duplicate nor drop either line.
        await InitProjectAsync("claude");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        AssertFileContains(".gitignore", "/.agents/skills/");
        AssertFileContains(".gitignore", "/.claude/skills/");
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Init_InvalidIntegration_Fails()
    {
        var command = InitCommand.Create();
        var result = await RunAsync(command, "invalid");

        result.AssertExitCode(2);
        result.AssertStderrContains("Unknown integration");
        result.AssertStderrContains("Valid options: claude, codex, all, none");
    }

    [Fact]
    public async Task Init_AlreadyInitialized_Fails()
    {
        await InitProjectAsync("none");

        var command = InitCommand.Create();
        var result = await RunAsync(command, "none");

        result.AssertExitCode(2);
        result.AssertStderrContains("already initialized");
    }

    #endregion

    #region Idempotency

    [Fact]
    public async Task Init_DoesNotOverwriteExistingDocs()
    {
        await InitProjectAsync("none");

        WriteFile("dydo/understand/architecture.md", "# Custom Architecture\n\nMy custom content");

        var command = InitCommand.Create();
        var result = await RunAsync(command, "none");

        var content = ReadFile("dydo/understand/architecture.md");
        Assert.Contains("Custom Architecture", content);
    }

    [Fact]
    public async Task Init_Join_TwiceDoesNotDuplicateGitignoreEntries()
    {
        await InitProjectAsync("claude");
        await JoinProjectAsync("codex");

        // Re-joining both integrations must not duplicate any line UpdateGitignore already wrote.
        var result = await JoinProjectAsync("all");

        result.AssertSuccess();
        var gitignore = ReadFile(".gitignore");
        Assert.Equal(1, gitignore.Split("/.claude/skills/").Length - 1);
        Assert.Equal(1, gitignore.Split("/.agents/skills/").Length - 1);
        Assert.Equal(1, gitignore.Split(".claude/settings.local.json").Length - 1);
        Assert.Equal(1, gitignore.Split("dydo/agents/").Length - 1);
        Assert.Equal(1, gitignore.Split("dydo/_system/.local/").Length - 1);
    }

    [Fact]
    public async Task Init_Join_None_TwiceDoesNotDuplicateGitignoreEntries()
    {
        // The skill-projection entries are written in 'none' mode too; repeated joins must not
        // duplicate them.
        await InitProjectAsync("none");
        await JoinProjectAsync("none");

        var result = await JoinProjectAsync("none");

        result.AssertSuccess();
        var gitignore = ReadFile(".gitignore");
        Assert.Equal(1, gitignore.Split("/.claude/skills/").Length - 1);
        Assert.Equal(1, gitignore.Split("/.agents/skills/").Length - 1);
    }

    #endregion

    #region Entry file

    [Fact]
    public async Task Init_Claude_WritesAgentsMdAndNoClaudeMd()
    {
        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        AssertFileExists("AGENTS.md");
        AssertFileNotExists("CLAUDE.md");
        Assert.Equal(ScaffoldTree.EntryPoint(Path.GetFileName(TestDir)), ReadFile("AGENTS.md"));
        result.AssertStdoutContains("AGENTS.md (entry point)");
        result.AssertStdoutContains("AGENTS.md -> dydo/index.md");
        result.AssertStdoutContains("Claude Code 2.1.281 or later reads AGENTS.md");
    }

    [Theory]
    [InlineData("none")]
    [InlineData("codex")]
    public async Task Init_WithoutClaude_WritesAgentsMdAndNoClaudeMd(string integration)
    {
        var result = await InitProjectAsync(integration);

        result.AssertSuccess();
        AssertFileExists("AGENTS.md");
        AssertFileNotExists("CLAUDE.md");
        result.AssertStdoutContains("AGENTS.md -> dydo/index.md");
        Assert.DoesNotContain("Claude Code 2.1.281", result.Stdout);
    }

    [Fact]
    public async Task Join_Claude_RemovesClaudeMdIdenticalToAgentsMd()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile("AGENTS.md", "# Custom entry\n\nSee dydo/index.md.\n");
        WriteFile("CLAUDE.md", "# Custom entry\r\n\r\nSee dydo/index.md.\r\n\r\n");

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        AssertFileNotExists("CLAUDE.md");
        Assert.Equal("# Custom entry\n\nSee dydo/index.md.\n", ReadFile("AGENTS.md"));
        result.AssertStdoutContains("Removed CLAUDE.md");
    }

    [Fact]
    public async Task Join_Claude_RemovesClaudeMdMatchingTheTemplateEvenWhenAgentsMdDiffers()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile("AGENTS.md", "# Custom entry\n");
        WriteFile("CLAUDE.md", ScaffoldTree.EntryPoint(Path.GetFileName(TestDir)) + "\n");

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        AssertFileNotExists("CLAUDE.md");
        Assert.Equal("# Custom entry\n", ReadFile("AGENTS.md"));
        result.AssertStdoutContains("Removed CLAUDE.md");
    }

    [Fact]
    public async Task Join_Claude_KeepsUserWrittenClaudeMdAndWarns()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile("CLAUDE.md", "# My own rules\n\nAlways run the tests.\n");
        var preserved = Snapshot("CLAUDE.md");

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        AssertUnchanged(preserved);
        result.AssertStdoutContains("CLAUDE.md hides AGENTS.md from Claude Code");
        result.AssertStdoutContains("@AGENTS.md");
        Assert.DoesNotContain("Removed CLAUDE.md", result.Stdout);
    }

    public static TheoryData<string, string> ReleasedClaudeMds => new()
    {
        { "3.5 base tree, LF", ReleasedEntryTexts.Generated("Old Clone", ReleasedEntryTexts.V3_5Base, "\n") },
        { "3.5 base tree, CRLF", ReleasedEntryTexts.Generated("Old Clone", ReleasedEntryTexts.V3_5Base, "\r\n") },
        { "v3.0.0..v3.4.1", ReleasedEntryTexts.Generated("Old Clone", ReleasedEntryTexts.V3_0, "\n") + "\n" },
        { "v2.1.0..v2.2.3, CRLF", ReleasedEntryTexts.Generated("Old Clone", ReleasedEntryTexts.V2_1, "\r\n") },
        { "v2.2.4..v2.2.5", ReleasedEntryTexts.Generated("Old Clone", ReleasedEntryTexts.V2_2_4, "\n") },
        { "v2.2.6..v2.2.9", ReleasedEntryTexts.Generated("Old Clone", ReleasedEntryTexts.V2_2_6, "\n") },
        { "v1.1.0..v2.0.15", ReleasedEntryTexts.Generated("Old Clone", ReleasedEntryTexts.V1, "\n") },
    };

    [Theory]
    [MemberData(nameof(ReleasedClaudeMds))]
    public async Task Join_Claude_RemovesClaudeMdAnOlderReleaseGenerated(string release, string claudeMd)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile("CLAUDE.md", claudeMd);

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        Assert.False(File.Exists(Path.Combine(TestDir, "CLAUDE.md")), $"CLAUDE.md from {release} was kept");
        result.AssertStdoutContains("Removed CLAUDE.md");
    }

    [Fact]
    public async Task Join_Claude_KeepsCustomisedCopyOfAnOlderReleaseAndWarns()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile("CLAUDE.md", ReleasedEntryTexts.Generated("Old Clone", ReleasedEntryTexts.V3_0 + "\n\nAlways run the tests.", "\n"));
        var preserved = Snapshot("CLAUDE.md");

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        AssertUnchanged(preserved);
        result.AssertStdoutContains("CLAUDE.md hides AGENTS.md from Claude Code");
        Assert.DoesNotContain("Removed CLAUDE.md", result.Stdout);
    }

    [Fact]
    public async Task Join_Claude_KeepsReleasedBodyUnderANonTitleFirstLine()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile("CLAUDE.md", ("Notes for Claude\n\n" + ReleasedEntryTexts.V3_0).ReplaceLineEndings("\n"));
        var preserved = Snapshot("CLAUDE.md");

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        AssertUnchanged(preserved);
        result.AssertStdoutContains("CLAUDE.md hides AGENTS.md from Claude Code");
    }

    [Fact]
    public async Task Init_Claude_KeepsUserWrittenClaudeMdAndWarns()
    {
        WriteFile("CLAUDE.md", "# My own rules\n");
        var preserved = Snapshot("CLAUDE.md");

        var result = await InitProjectAsync("claude");

        result.AssertSuccess();
        AssertUnchanged(preserved);
        result.AssertStdoutContains("CLAUDE.md hides AGENTS.md from Claude Code");
    }

    [Theory]
    [InlineData("CLAUDE.local.md", "CLAUDE.local.md stops Claude Code reading AGENTS.md")]
    [InlineData(".claude/CLAUDE.md", ".claude/CLAUDE.md stops Claude Code reading AGENTS.md. Put @../AGENTS.md at its top.")]
    public async Task Join_Claude_WarnsAboutOtherFilesThatHideAgentsMd(string path, string warning)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile(path, ReadFile("AGENTS.md"));
        var preserved = Snapshot(path);

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        AssertUnchanged(preserved);
        result.AssertStdoutContains(warning);
    }

    [Fact]
    public async Task Join_Codex_LeavesClaudeMdAlone()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        WriteFile("CLAUDE.md", ReadFile("AGENTS.md"));
        var preserved = Snapshot("CLAUDE.md");

        var result = await JoinProjectAsync("codex");

        result.AssertSuccess();
        AssertUnchanged(preserved);
        Assert.DoesNotContain("CLAUDE.md", result.Stdout);
    }

    [Fact]
    public async Task Join_Claude_MigratesLegacyClaudeOnlyProject()
    {
        await MakeLegacyClaudeOnlyProjectAsync();

        var result = await JoinProjectAsync("claude");

        result.AssertSuccess();
        AssertFileExists("AGENTS.md");
        Assert.Equal(ScaffoldTree.EntryPoint(Path.GetFileName(TestDir)), ReadFile("AGENTS.md"));
        AssertFileNotExists("CLAUDE.md");
        result.AssertStdoutContains("Removed CLAUDE.md");
    }

    [Fact]
    public async Task Join_All_FailedPreflightKeepsLegacyClaudeMdAndCreatesNoAgentsMd()
    {
        await MakeLegacyClaudeOnlyProjectAsync();
        Directory.CreateDirectory(Path.Combine(TestDir, ".codex"));
        WriteFile(".codex/config.toml", "[agents\nmax_depth = 3\n");
        var preserved = Snapshot("CLAUDE.md");

        var result = await JoinProjectAsync("all");

        result.AssertExitCode(2);
        AssertUnchanged(preserved);
        AssertFileNotExists("AGENTS.md");
    }

    private async Task MakeLegacyClaudeOnlyProjectAsync()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        var generated = ReadFile("AGENTS.md");
        File.Delete(Path.Combine(TestDir, "AGENTS.md"));
        WriteFile("CLAUDE.md", generated);
    }

    #endregion

    private Dictionary<string, byte[]> Snapshot(params string[] paths) =>
        paths.ToDictionary(path => path, path => File.ReadAllBytes(Path.Combine(TestDir, path)));

    private void AssertUnchanged(Dictionary<string, byte[]> snapshot)
    {
        foreach (var (path, expected) in snapshot)
            Assert.Equal(expected, File.ReadAllBytes(Path.Combine(TestDir, path)));
    }
}
