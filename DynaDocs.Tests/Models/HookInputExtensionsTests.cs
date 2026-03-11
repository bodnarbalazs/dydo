namespace DynaDocs.Tests.Models;

using DynaDocs.Models;

public class HookInputExtensionsTests
{
    private static HookInput MakeInput(string? toolName, string? filePath = null, string? command = null, string? path = null)
    {
        return new HookInput
        {
            ToolName = toolName,
            ToolInput = new ToolInputData
            {
                FilePath = filePath,
                Command = command,
                Path = path,
            }
        };
    }

    // ─── GetAction ───

    [Theory]
    [InlineData("write", "write")]
    [InlineData("edit", "edit")]
    [InlineData("bash", "execute")]
    [InlineData("read", "read")]
    [InlineData("glob", "read")]
    [InlineData("grep", "read")]
    [InlineData("Write", "write")]
    [InlineData("BASH", "execute")]
    [InlineData("Glob", "read")]
    public void GetAction_MapsToolNameToAction(string toolName, string expected)
    {
        var input = MakeInput(toolName);
        Assert.Equal(expected, input.GetAction());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("agent")]
    [InlineData("unknown-tool")]
    public void GetAction_ReturnsUnknown_ForUnmappedTools(string? toolName)
    {
        var input = MakeInput(toolName);
        Assert.Equal("unknown", input.GetAction());
    }

    // ─── GetFilePath ───

    [Fact]
    public void GetFilePath_ReturnsFilePath()
    {
        var input = MakeInput("edit", filePath: "/some/file.cs");
        Assert.Equal("/some/file.cs", input.GetFilePath());
    }

    [Fact]
    public void GetFilePath_ReturnsNull_WhenNoToolInput()
    {
        var input = new HookInput { ToolName = "edit" };
        Assert.Null(input.GetFilePath());
    }

    [Fact]
    public void GetFilePath_ReturnsNull_WhenFilePathNull()
    {
        var input = MakeInput("edit");
        Assert.Null(input.GetFilePath());
    }

    // ─── GetSearchPath ───

    [Fact]
    public void GetSearchPath_ReturnsPath()
    {
        var input = MakeInput("glob", path: "/search/dir");
        Assert.Equal("/search/dir", input.GetSearchPath());
    }

    [Fact]
    public void GetSearchPath_ReturnsNull_WhenNoToolInput()
    {
        var input = new HookInput { ToolName = "glob" };
        Assert.Null(input.GetSearchPath());
    }

    // ─── IsWriteOperation ───

    [Theory]
    [InlineData("write", true)]
    [InlineData("edit", true)]
    [InlineData("Write", true)]
    [InlineData("EDIT", true)]
    [InlineData("read", false)]
    [InlineData("bash", false)]
    [InlineData("glob", false)]
    [InlineData(null, false)]
    public void IsWriteOperation_IdentifiesWriteTools(string? toolName, bool expected)
    {
        var input = MakeInput(toolName);
        Assert.Equal(expected, input.IsWriteOperation());
    }

    // ─── IsReadOperation ───

    [Theory]
    [InlineData("read", true)]
    [InlineData("glob", true)]
    [InlineData("grep", true)]
    [InlineData("Read", true)]
    [InlineData("GLOB", true)]
    [InlineData("write", false)]
    [InlineData("bash", false)]
    [InlineData("edit", false)]
    [InlineData(null, false)]
    public void IsReadOperation_IdentifiesReadTools(string? toolName, bool expected)
    {
        var input = MakeInput(toolName);
        Assert.Equal(expected, input.IsReadOperation());
    }

    // ─── IsBashTool ───

    [Theory]
    [InlineData("bash", true)]
    [InlineData("Bash", true)]
    [InlineData("BASH", true)]
    [InlineData("read", false)]
    [InlineData("write", false)]
    [InlineData(null, false)]
    public void IsBashTool_IdentifiesBashTool(string? toolName, bool expected)
    {
        var input = MakeInput(toolName);
        Assert.Equal(expected, input.IsBashTool());
    }

    // ─── GetCommand ───

    [Fact]
    public void GetCommand_ReturnsCommand()
    {
        var input = MakeInput("bash", command: "ls -la");
        Assert.Equal("ls -la", input.GetCommand());
    }

    [Fact]
    public void GetCommand_ReturnsNull_WhenNoToolInput()
    {
        var input = new HookInput { ToolName = "bash" };
        Assert.Null(input.GetCommand());
    }

    // ─── IsFileOperation ───

    [Theory]
    [InlineData("edit", true)]
    [InlineData("write", true)]
    [InlineData("read", true)]
    [InlineData("bash", true)]
    [InlineData("glob", true)]
    [InlineData("grep", true)]
    [InlineData("EDIT", true)]
    [InlineData("agent", false)]
    [InlineData("unknown", false)]
    [InlineData(null, false)]
    public void IsFileOperation_IdentifiesFileTools(string? toolName, bool expected)
    {
        var input = MakeInput(toolName);
        Assert.Equal(expected, input.IsFileOperation());
    }
}
