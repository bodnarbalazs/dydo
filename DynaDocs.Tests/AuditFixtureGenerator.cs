namespace DynaDocs.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DynaDocs.Models;

/// <summary>
/// One-time generator that redacts proprietary audit data from temp/audit/2026/
/// into generic test fixtures at DynaDocs.Tests/Fixtures/audit-large/2026/.
/// Run: GENERATE_FIXTURES=1 dotnet test --filter GenerateRedactedFixtures
/// </summary>
public class AuditFixtureGenerator
{
    static readonly (string From, string To)[] Replacements =
    [
        // Absolute path prefixes (longer/more specific first)
        (@"C:\Users\User\Desktop\LC\", @"C:\Users\User\Desktop\SampleProject\"),
        (@"C:\Users\User\Desktop\LC", @"C:\Users\User\Desktop\SampleProject"),
        (@"C:/Users/User/Desktop/LC/", @"C:/Users/User/Desktop/SampleProject/"),
        (@"C:/Users/User/Desktop/LC", @"C:/Users/User/Desktop/SampleProject"),
        (@"/c/Users/User/Desktop/LC/", @"/c/Users/User/Desktop/SampleProject/"),
        (@"/c/Users/User/Desktop/LC", @"/c/Users/User/Desktop/SampleProject"),
        ("C--Users-User-Desktop-LC", "C--Users-User-Desktop-SampleProject"),

        // Project assembly/solution names
        ("LC.AppHost", "SampleApp.Host"),
        ("LC.Application", "SampleApp.Core"),
        ("LC.Infrastructure", "SampleApp.Infra"),
        ("LC.Domain", "SampleApp.Domain"),
        ("LC.Api", "SampleApp.Api"),
        ("LC.Tests", "SampleApp.Tests"),
        ("LC.sln", "SampleApp.sln"),

        // Domain-specific terms (longer compounds before shorter to avoid partial matches)
        ("TeaseEditor", "ContentEditor"),
        ("tease-editor", "content-editor"),
        ("Tease", "Content"),
        ("tease", "content"),

        ("PlayerCharacter", "UserProfile"),
        ("player-character", "user-profile"),

        ("StorageContainer", "DataStore"),
        ("storage-container", "data-store"),

        ("TokenLedger", "PointsLedger"),
        ("Token Economy", "Points System"),
        ("token-economy", "points-system"),
        ("token economy", "points system"),

        ("BlurHash", "Placeholder"),
        ("blurhash", "placeholder"),

        ("TmkComparison", "HashComparison"),

        ("CrapScore", "QualityScore"),
        ("crap-score", "quality-score"),
        ("CRAP", "QUALITY"),

        ("Ambience", "Background"),
        ("ambience", "background"),

        ("MultipartUpload", "ChunkedUpload"),

        ("PublishingEndpoints", "ReleaseEndpoints"),
        ("publishing-versioning", "release-versioning"),
        ("publishing-review", "release-review"),
        ("Publishing", "Release"),

        // Changelog/decision entry names
        ("admin-panel-architecture", "dashboard-design"),
        ("admin-panel", "dashboard"),
        ("kyc-compliance-design", "compliance-design"),
        ("kyc-compliance", "compliance-check"),
        ("payment-chargeback-policy", "payment-policy"),
        ("payment-chargeback", "payment-dispute"),
        ("takedown-process", "removal-process"),
        ("rating-architecture", "scoring-design"),
        ("gdpr-and-analytics", "privacy-analytics"),
        ("obfuscated-ids", "encoded-ids"),
        ("token-economy-revision", "points-system-revision"),
        ("roadmap-planning", "project-planning"),
        ("roadmap-review", "project-review"),
        ("roadmap-ordering", "priority-ordering"),
        ("fix-ownership-status-naming", "fix-access-status-naming"),
        ("ownership-status", "access-status"),

        ("asset_processing", "media_processing"),

        ("secrets.json", "local-config.json"),

        // Catch-all for remaining path variations (hybrid Unix root + backslash separator)
        (@"Desktop\LC\", @"Desktop\SampleProject\"),
        (@"Desktop\LC", @"Desktop\SampleProject"),
        ("Desktop/LC/", "Desktop/SampleProject/"),
        ("Desktop/LC", "Desktop/SampleProject"),
    ];

    readonly Dictionary<string, string> _hashMap = new();

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void GenerateRedactedFixtures()
    {
        if (Environment.GetEnvironmentVariable("GENERATE_FIXTURES") != "1")
            return;

        var repoRoot = FindRepoRoot();
        var sourceDir = Path.Combine(repoRoot, "temp", "audit", "2026");
        var outputDir = Path.Combine(repoRoot, "DynaDocs.Tests", "Fixtures", "audit-large", "2026");

        Assert.True(Directory.Exists(sourceDir), $"Source not found: {sourceDir}");
        Directory.CreateDirectory(outputDir);

        var files = Directory.GetFiles(sourceDir, "*.json").Order().ToArray();
        foreach (var file in files)
        {
            var session = JsonSerializer.Deserialize<AuditSession>(File.ReadAllText(file), JsonOpts)!;
            Redact(session);
            File.WriteAllText(
                Path.Combine(outputDir, Path.GetFileName(file)),
                JsonSerializer.Serialize(session, JsonOpts));
        }

        // Verify output
        var outputFiles = Directory.GetFiles(outputDir, "*.json");
        Assert.Equal(files.Length, outputFiles.Length);

        // Spot-check: no proprietary content in output
        foreach (var f in outputFiles)
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("Desktop\\\\LC", text);
            Assert.DoesNotContain("Desktop/LC", text);
            Assert.DoesNotContain("\"balazs\"", text);
        }
    }

    void Redact(AuditSession session)
    {
        if (session.Human != null) session.Human = "developer";
        if (session.GitHead != null) session.GitHead = FakeHash(session.GitHead);

        foreach (var evt in session.Events)
        {
            if (evt.Path != null) evt.Path = Apply(evt.Path);
            if (evt.Command != null) evt.Command = Apply(evt.Command);
            if (evt.Task != null) evt.Task = Apply(evt.Task);
            if (evt.CommitHash != null) evt.CommitHash = FakeHash(evt.CommitHash);
            if (evt.CommitMessage != null) evt.CommitMessage = Apply(evt.CommitMessage);
            if (evt.BlockReason != null) evt.BlockReason = Apply(evt.BlockReason);
        }

        if (session.Snapshot is not { } snap) return;

        snap.GitCommit = FakeHash(snap.GitCommit);
        for (var i = 0; i < snap.Files.Count; i++) snap.Files[i] = Apply(snap.Files[i]);
        for (var i = 0; i < snap.Folders.Count; i++) snap.Folders[i] = Apply(snap.Folders[i]);
        snap.DocLinks = snap.DocLinks.ToDictionary(
            kv => Apply(kv.Key),
            kv => kv.Value.Select(Apply).ToList());
    }

    static string Apply(string text)
    {
        foreach (var (from, to) in Replacements)
            text = text.Replace(from, to);
        return text;
    }

    string FakeHash(string original)
    {
        if (!_hashMap.TryGetValue(original, out var fake))
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("redact:" + original));
            fake = Convert.ToHexString(bytes).ToLowerInvariant();
            _hashMap[original] = fake;
        }
        return fake[..Math.Min(original.Length, fake.Length)];
    }

    static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "DynaDocs.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find DynaDocs.sln");
    }
}
