namespace DynaDocs.Tests.Steps;

public record CliResult(int ExitCode, string Stdout, string Stderr)
{
    public void AssertSuccess() => Assert.True(ExitCode == 0,
        $"Expected exit 0, got {ExitCode}.\n{Stdout}\n{Stderr}");
}
