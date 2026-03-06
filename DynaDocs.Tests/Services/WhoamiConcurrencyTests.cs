namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class WhoamiConcurrencyTests : IDisposable
{
    private readonly string _testDir;

    public WhoamiConcurrencyTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-concurrency-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    private string AgentsPath => Path.Combine(_testDir, "dydo", "agents");

    private void SetupConfig(string[] agents)
    {
        var agentsJson = string.Join(", ", agents.Select(a => $"\"{a}\""));
        var config = $$"""
            {
              "version": 1,
              "agents": {
                "pool": [{{agentsJson}}],
                "assignments": {
                  "testuser": [{{agentsJson}}]
                }
              }
            }
            """;
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), config);
    }

    private void CreateAgent(string name, string sessionId)
    {
        var workspace = Path.Combine(AgentsPath, name);
        Directory.CreateDirectory(workspace);

        var sessionJson = $$"""
            {"Agent":"{{name}}","SessionId":"{{sessionId}}","Claimed":"{{DateTime.UtcNow:o}}"}
            """;
        File.WriteAllText(Path.Combine(workspace, ".session"), sessionJson);

        File.WriteAllText(Path.Combine(workspace, "state.md"), $"""
            ---
            status: working
            assigned: testuser
            ---
            """);
    }

    private void WriteAgentHint(string agentName)
    {
        var hintPath = Path.Combine(AgentsPath, ".session-agent");
        Directory.CreateDirectory(AgentsPath);
        File.WriteAllText(hintPath, agentName);
    }

    [Fact]
    public async Task ConcurrentWhoami_AllReturnCorrectAgent()
    {
        var agents = new[] { "Adele", "Brian", "Charlie", "Dexter", "Emma" };
        SetupConfig(agents);

        for (var i = 0; i < agents.Length; i++)
            CreateAgent(agents[i], $"session-{i}");

        var tasks = agents.Select((name, i) => Task.Run(() =>
        {
            var registry = new AgentRegistry(_testDir);
            var result = registry.GetCurrentAgent($"session-{i}");
            return (Expected: name, Actual: result);
        })).ToArray();

        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));

        foreach (var r in results)
        {
            Assert.NotNull(r.Actual);
            Assert.Equal(r.Expected, r.Actual.Name);
        }
    }

    [Fact]
    public async Task WhoamiDuringConcurrentFileWrites_NeverHangs()
    {
        var agents = new[] { "Adele", "Brian", "Charlie" };
        SetupConfig(agents);
        foreach (var name in agents)
            CreateAgent(name, $"session-{name}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Writer: continuously rewrite session files for Adele
        var writer = Task.Run(() =>
        {
            var workspace = Path.Combine(AgentsPath, "Adele");
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var json = $$"""{"Agent":"Adele","SessionId":"session-Adele","Claimed":"{{DateTime.UtcNow:o}}"}""";
                    File.WriteAllText(Path.Combine(workspace, ".session"), json);
                }
                catch (IOException) { }
            }
        });

        // Readers: repeatedly call GetCurrentAgent for Brian and Charlie
        var readers = new[] { "Brian", "Charlie" }.Select(name => Task.Run(() =>
        {
            var count = 0;
            while (!cts.Token.IsCancellationRequested && count < 100)
            {
                var registry = new AgentRegistry(_testDir);
                var result = registry.GetCurrentAgent($"session-{name}");
                if (result == null)
                    errors.Add($"GetCurrentAgent returned null for {name} on iteration {count}");
                else if (result.Name != name)
                    errors.Add($"GetCurrentAgent returned {result.Name} instead of {name}");
                count++;
            }
        })).ToArray();

        await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
        await writer.WaitAsync(TimeSpan.FromSeconds(2)).ContinueWith(_ => { }); // Ignore cancellation

        Assert.Empty(errors);
    }

    [Fact]
    public async Task FileContention_RetrySucceeds()
    {
        var agents = new[] { "Adele" };
        SetupConfig(agents);
        CreateAgent("Adele", "session-locked");

        var sessionPath = Path.Combine(AgentsPath, "Adele", ".session");

        // Hold an exclusive lock on the .session file, release after a short delay
        var lockStream = new FileStream(sessionPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var releaseTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            lockStream.Dispose();
        });

        var registry = new AgentRegistry(_testDir);
        var result = registry.GetCurrentAgent("session-locked");

        await releaseTask;

        // Should succeed after retry (lock released after 100ms, first retry at 50ms, second at 150ms)
        Assert.NotNull(result);
        Assert.Equal("Adele", result.Name);
    }

    [Fact]
    public void FileContention_AllRetriesExhausted_ReturnsNull()
    {
        var agents = new[] { "Adele" };
        SetupConfig(agents);
        CreateAgent("Adele", "session-locked");

        var sessionPath = Path.Combine(AgentsPath, "Adele", ".session");

        // Hold exclusive lock for the duration of the test
        using var lockStream = new FileStream(sessionPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var registry = new AgentRegistry(_testDir);
        var result = registry.GetCurrentAgent("session-locked");

        // All retries exhausted, should return null (not hang)
        Assert.Null(result);
    }

    [Fact]
    public void ShortCircuit_UsesHintFile()
    {
        var agents = new[] { "Adele", "Brian", "Charlie", "Dexter", "Emma" };
        SetupConfig(agents);

        // Only create session for Charlie
        CreateAgent("Charlie", "session-charlie");
        WriteAgentHint("Charlie");

        var registry = new AgentRegistry(_testDir);
        var result = registry.GetCurrentAgent("session-charlie");

        Assert.NotNull(result);
        Assert.Equal("Charlie", result.Name);
    }

    [Fact]
    public void ShortCircuit_FallsBackOnInvalidHint()
    {
        var agents = new[] { "Adele", "Brian" };
        SetupConfig(agents);

        CreateAgent("Brian", "session-brian");
        // Write a stale hint pointing to Adele (no session for Adele)
        WriteAgentHint("Adele");

        var registry = new AgentRegistry(_testDir);
        var result = registry.GetCurrentAgent("session-brian");

        // Should fall back to scan and find Brian
        Assert.NotNull(result);
        Assert.Equal("Brian", result.Name);
    }

    [Fact]
    public void ShortCircuit_FallsBackOnMissingHintFile()
    {
        var agents = new[] { "Adele" };
        SetupConfig(agents);
        CreateAgent("Adele", "session-adele");
        // No hint file written

        var registry = new AgentRegistry(_testDir);
        var result = registry.GetCurrentAgent("session-adele");

        Assert.NotNull(result);
        Assert.Equal("Adele", result.Name);

        // Verify hint file was written by the fallback scan
        var hintPath = Path.Combine(AgentsPath, ".session-agent");
        Assert.True(File.Exists(hintPath));
        Assert.Equal("Adele", File.ReadAllText(hintPath));
    }

    [Fact]
    public async Task StressTest_ConcurrentGetCurrentAgent()
    {
        var agents = new[] { "Adele", "Brian", "Charlie", "Dexter", "Emma" };
        SetupConfig(agents);
        foreach (var name in agents)
            CreateAgent(name, $"session-{name}");

        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
        var totalCalls = new System.Collections.Concurrent.ConcurrentBag<int>();

        // 10 concurrent tasks, each doing GetCurrentAgent in a loop for ~3 seconds
        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
        {
            var agent = agents[i % agents.Length];
            var sessionId = $"session-{agent}";
            var count = 0;
            var deadline = DateTime.UtcNow.AddSeconds(3);

            while (DateTime.UtcNow < deadline)
            {
                var registry = new AgentRegistry(_testDir);
                var result = registry.GetCurrentAgent(sessionId);
                if (result == null)
                    errors.Add($"Null result for {agent} on call {count}");
                else if (result.Name != agent)
                    errors.Add($"Wrong agent: expected {agent}, got {result.Name}");
                count++;
            }

            totalCalls.Add(count);
        })).ToArray();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Empty(errors);
        Assert.True(totalCalls.Sum() > 50, $"Expected many calls but only got {totalCalls.Sum()}");
    }
}
