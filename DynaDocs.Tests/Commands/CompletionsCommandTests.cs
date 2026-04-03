namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

[Collection("ConsoleOutput")]
public class CompletionsCommandTests
{
    [Fact]
    public void Bash_ContainsCompleteDirective()
    {
        var output = CaptureOutput("bash");

        Assert.Contains("complete -F _dydo_completions dydo", output);
        Assert.Contains("_dydo_completions()", output);
        Assert.Contains("COMPREPLY", output);
    }

    [Fact]
    public void Zsh_ContainsCompdefDirective()
    {
        var output = CaptureOutput("zsh");

        Assert.Contains("compdef _dydo_completions dydo", output);
        Assert.Contains("compadd", output);
    }

    [Fact]
    public void PowerShell_ContainsRegisterArgumentCompleter()
    {
        var output = CaptureOutput("powershell");

        Assert.Contains("Register-ArgumentCompleter", output);
        Assert.Contains("-CommandName dydo", output);
        Assert.Contains("CompletionResult", output);
    }

    [Fact]
    public void UnknownShell_ReturnsError()
    {
        var command = CompletionsCommand.Create();
        var result = command.Parse("fish").Invoke();

        Assert.Equal(2, result);
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("zsh")]
    [InlineData("powershell")]
    public void KnownShell_ReturnsSuccess(string shell)
    {
        var command = CompletionsCommand.Create();
        var result = command.Parse(shell).Invoke();

        Assert.Equal(0, result);
    }

    private static string CaptureOutput(string shell)
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(sw));
        try
        {
            var command = CompletionsCommand.Create();
            command.Parse(shell).Invoke();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return sw.ToString();
    }
}
