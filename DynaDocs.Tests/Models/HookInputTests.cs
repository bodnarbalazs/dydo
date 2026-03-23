namespace DynaDocs.Tests.Models;

using System.Text.Json;
using DynaDocs.Models;

public class HookInputTests
{
    [Fact]
    public void DefaultProperties_AreNull()
    {
        var input = new HookInput();

        Assert.Null(input.SessionId);
        Assert.Null(input.TranscriptPath);
        Assert.Null(input.Cwd);
        Assert.Null(input.PermissionMode);
        Assert.Null(input.HookEventName);
        Assert.Null(input.ToolName);
        Assert.Null(input.ToolInput);
        Assert.Null(input.ToolUseId);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var toolInput = new ToolInputData { FilePath = "/test.cs" };
        var input = new HookInput
        {
            SessionId = "session-1",
            TranscriptPath = "/transcripts/1.json",
            Cwd = "/home/user",
            PermissionMode = "default",
            HookEventName = "PreToolUse",
            ToolName = "edit",
            ToolInput = toolInput,
            ToolUseId = "tool-123"
        };

        Assert.Equal("session-1", input.SessionId);
        Assert.Equal("/transcripts/1.json", input.TranscriptPath);
        Assert.Equal("/home/user", input.Cwd);
        Assert.Equal("default", input.PermissionMode);
        Assert.Equal("PreToolUse", input.HookEventName);
        Assert.Equal("edit", input.ToolName);
        Assert.Same(toolInput, input.ToolInput);
        Assert.Equal("tool-123", input.ToolUseId);
    }

    [Fact]
    public void Deserialize_MapsJsonPropertyNames()
    {
        var json = """
        {
            "session_id": "s1",
            "transcript_path": "/t.json",
            "cwd": "/cwd",
            "permission_mode": "plan",
            "hook_event_name": "PreToolUse",
            "tool_name": "write",
            "tool_input": { "file_path": "/a.cs" },
            "tool_use_id": "tu-1"
        }
        """;

        var input = JsonSerializer.Deserialize<HookInput>(json);

        Assert.NotNull(input);
        Assert.Equal("s1", input.SessionId);
        Assert.Equal("/t.json", input.TranscriptPath);
        Assert.Equal("/cwd", input.Cwd);
        Assert.Equal("plan", input.PermissionMode);
        Assert.Equal("PreToolUse", input.HookEventName);
        Assert.Equal("write", input.ToolName);
        Assert.NotNull(input.ToolInput);
        Assert.Equal("/a.cs", input.ToolInput.FilePath);
        Assert.Equal("tu-1", input.ToolUseId);
    }

    [Fact]
    public void Serialize_UsesJsonPropertyNames()
    {
        var input = new HookInput
        {
            SessionId = "s1",
            ToolName = "read",
            ToolUseId = "tu-2"
        };

        var json = JsonSerializer.Serialize(input);

        Assert.Contains("\"session_id\"", json);
        Assert.Contains("\"tool_name\"", json);
        Assert.Contains("\"tool_use_id\"", json);
        Assert.DoesNotContain("\"SessionId\"", json);
        Assert.DoesNotContain("\"ToolName\"", json);
    }

    [Fact]
    public void Deserialize_EmptyJson_AllPropertiesNull()
    {
        var input = JsonSerializer.Deserialize<HookInput>("{}");

        Assert.NotNull(input);
        Assert.Null(input.SessionId);
        Assert.Null(input.ToolName);
        Assert.Null(input.ToolInput);
    }
}
