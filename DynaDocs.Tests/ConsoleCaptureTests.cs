namespace DynaDocs.Tests;

public class ConsoleCaptureTests
{
    [Fact]
    public void Stderr_RestoresConsoleError_WhenActionThrows()
    {
        var preCallError = Console.Error;

        var ex = Assert.Throws<InvalidOperationException>(
            () => ConsoleCapture.Stderr(() => throw new InvalidOperationException("probe")));

        Assert.Equal("probe", ex.Message);
        Assert.Same(preCallError, Console.Error);
    }

    [Fact]
    public void Stderr_RestoresConsoleError_WhenActionSucceeds()
    {
        var preCallError = Console.Error;

        var output = ConsoleCapture.Stderr(() => Console.Error.Write("captured"));

        Assert.Equal("captured", output);
        Assert.Same(preCallError, Console.Error);
    }
}
