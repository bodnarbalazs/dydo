namespace DynaDocs.Tests;

/// <summary>
/// Thread-safe console capture for parallel xUnit test execution.
/// Console.Out/Error are process-global — a static semaphore serializes all
/// redirect-execute-restore sequences so ToString() never races with writes.
/// </summary>
public static class ConsoleCapture
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string Stdout(Action action)
    {
        Gate.Wait();
        try
        {
            var original = Console.Out;
            var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                action();
                return writer.ToString();
            }
            finally
            {
                Console.SetOut(original);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public static string Stderr(Action action)
    {
        Gate.Wait();
        try
        {
            var original = Console.Error;
            var writer = new StringWriter();
            Console.SetError(writer);
            try
            {
                action();
                return writer.ToString();
            }
            finally
            {
                Console.SetError(original);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public static (int exitCode, string stdout, string stderr) All(Func<int> action)
    {
        Gate.Wait();
        try
        {
            var originalOut = Console.Out;
            var originalErr = Console.Error;
            var outWriter = new StringWriter();
            var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            try
            {
                var code = action();
                return (code, outWriter.ToString(), errWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<(int exitCode, string stdout, string stderr)> AllAsync(Func<Task<int>> action)
    {
        await Gate.WaitAsync();
        try
        {
            var originalOut = Console.Out;
            var originalErr = Console.Error;
            var outWriter = new StringWriter();
            var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            try
            {
                var code = await action();
                return (code, outWriter.ToString(), errWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<(int exitCode, string stdout, string stderr)> AllAsyncWithStdin(
        TextReader stdin, Func<Task<int>> action)
    {
        await Gate.WaitAsync();
        try
        {
            var originalOut = Console.Out;
            var originalErr = Console.Error;
            var originalIn = Console.In;
            var outWriter = new StringWriter();
            var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            Console.SetIn(stdin);
            try
            {
                var code = await action();
                return (code, outWriter.ToString(), errWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
                Console.SetIn(originalIn);
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
