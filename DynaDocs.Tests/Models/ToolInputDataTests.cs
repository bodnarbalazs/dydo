namespace DynaDocs.Tests.Models;

using System.Text.Json;
using DynaDocs.Models;

public class ToolInputDataTests
{
    [Fact]
    public void DefaultProperties_AreNull()
    {
        var data = new ToolInputData();

        Assert.Null(data.FilePath);
        Assert.Null(data.Content);
        Assert.Null(data.OldString);
        Assert.Null(data.NewString);
        Assert.Null(data.Command);
        Assert.Null(data.Path);
        Assert.Null(data.RunInBackground);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var data = new ToolInputData
        {
            FilePath = "/src/file.cs",
            Content = "new content",
            OldString = "old",
            NewString = "new",
            Command = "dotnet build",
            Path = "/src",
            RunInBackground = true
        };

        Assert.Equal("/src/file.cs", data.FilePath);
        Assert.Equal("new content", data.Content);
        Assert.Equal("old", data.OldString);
        Assert.Equal("new", data.NewString);
        Assert.Equal("dotnet build", data.Command);
        Assert.Equal("/src", data.Path);
        Assert.True(data.RunInBackground);
    }

    [Fact]
    public void Deserialize_MapsJsonPropertyNames()
    {
        var json = """
        {
            "file_path": "/a.cs",
            "content": "hello",
            "old_string": "foo",
            "new_string": "bar",
            "command": "ls",
            "path": "/tmp",
            "run_in_background": false
        }
        """;

        var data = JsonSerializer.Deserialize<ToolInputData>(json);

        Assert.NotNull(data);
        Assert.Equal("/a.cs", data.FilePath);
        Assert.Equal("hello", data.Content);
        Assert.Equal("foo", data.OldString);
        Assert.Equal("bar", data.NewString);
        Assert.Equal("ls", data.Command);
        Assert.Equal("/tmp", data.Path);
        Assert.False(data.RunInBackground);
    }

    [Fact]
    public void Serialize_UsesJsonPropertyNames()
    {
        var data = new ToolInputData
        {
            FilePath = "/test.cs",
            RunInBackground = true
        };

        var json = JsonSerializer.Serialize(data);

        Assert.Contains("\"file_path\"", json);
        Assert.Contains("\"run_in_background\"", json);
        Assert.DoesNotContain("\"FilePath\"", json);
        Assert.DoesNotContain("\"RunInBackground\"", json);
    }

    [Fact]
    public void RunInBackground_NullableBoolean_SerializesCorrectly()
    {
        var withNull = new ToolInputData();
        var withTrue = new ToolInputData { RunInBackground = true };
        var withFalse = new ToolInputData { RunInBackground = false };

        var jsonNull = JsonSerializer.Serialize(withNull);
        var jsonTrue = JsonSerializer.Serialize(withTrue);
        var jsonFalse = JsonSerializer.Serialize(withFalse);

        Assert.Contains("\"run_in_background\":null", jsonNull.Replace(" ", ""));
        Assert.Contains("\"run_in_background\":true", jsonTrue.Replace(" ", ""));
        Assert.Contains("\"run_in_background\":false", jsonFalse.Replace(" ", ""));
    }
}
