namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;

/// <summary>
/// Tests that user-controlled data in audit sessions is properly escaped
/// when rendered into HTML, preventing XSS attacks.
/// </summary>
[Collection("Integration")]
public class AuditXssTests : IntegrationTestBase
{
    private string AuditPath => Path.Combine(TestDir, "dydo", "_system", "audit");

    private void CreateAuditSession(
        string sessionId,
        string? agent = "TestAgent",
        string? human = "tester",
        DateTime? started = null,
        List<AuditEvent>? events = null,
        ProjectSnapshot? snapshot = null)
    {
        started ??= new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var session = new AuditSession
        {
            SessionId = sessionId,
            AgentName = agent,
            Human = human,
            Started = started.Value,
            Events = events ?? [],
            Snapshot = snapshot
        };

        var year = started.Value.Year.ToString();
        var date = started.Value.ToString("yyyy-MM-dd");
        var yearDir = Path.Combine(AuditPath, year);
        Directory.CreateDirectory(yearDir);

        var json = JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AuditSession);
        File.WriteAllText(Path.Combine(yearDir, $"{date}-{sessionId}.json"), json);
    }

    private static ProjectSnapshot MakeSnapshot() => new()
    {
        GitCommit = "abc1234",
        Files = ["src/main.cs"],
        Folders = ["src"],
        DocLinks = []
    };

    private async Task<string> GenerateHtmlAsync()
    {
        var command = AuditCommand.Create();
        var result = await RunAsync(command);
        result.AssertSuccess();

        var replayPath = Path.Combine(AuditPath, "reports", "replay.html");
        Assert.True(File.Exists(replayPath));
        return File.ReadAllText(replayPath);
    }

    [Fact]
    public async Task AgentName_WithHtmlTags_IsEscapedInSessionsTable()
    {
        await InitProjectAsync();
        CreateAuditSession("xss-agent", agent: "<script>alert('xss')</script>",
            snapshot: MakeSnapshot());

        var html = await GenerateHtmlAsync();

        // The raw script tag must NOT appear in the HTML
        Assert.DoesNotContain("<script>alert('xss')</script>", html);
        // The escaped version should appear in the sessions table
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task HumanName_WithHtmlTags_IsEscapedInSessionsTable()
    {
        await InitProjectAsync();
        CreateAuditSession("xss-human", human: "<img src=x onerror=alert(1)>",
            snapshot: MakeSnapshot());

        var html = await GenerateHtmlAsync();

        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
        Assert.Contains("&lt;img", html);
    }

    [Fact]
    public async Task AgentName_WithQuotes_DoesNotBreakHtmlAttributes()
    {
        await InitProjectAsync();
        CreateAuditSession("xss-quotes", agent: "Agent\"onclick=\"alert(1)",
            snapshot: MakeSnapshot());

        var html = await GenerateHtmlAsync();

        // The raw double-quote injection should not appear unescaped in HTML context
        Assert.DoesNotContain("onclick=\"alert(1)\"", html);
    }

    [Fact]
    public async Task Visualization_InnerHtml_UsesTextContentForAgentNames()
    {
        // The JavaScript in the HTML should use textContent (or equivalent safe method)
        // instead of innerHTML for user-controlled data like agent names
        await InitProjectAsync();
        CreateAuditSession("js-check", snapshot: MakeSnapshot());

        var html = await GenerateHtmlAsync();

        // The buildAgentLegend function should not use innerHTML with unsanitized agent names
        // Check that the processEvent function doesn't directly concatenate event data into innerHTML
        // Either textContent is used, or a sanitize/escape function is applied
        Assert.DoesNotContain("agent + '</span>'", html);
    }
}
