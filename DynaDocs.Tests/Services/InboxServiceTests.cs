namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class InboxServiceTests
{
    [Fact]
    public void PrintInboxItem_TaskItem_IncludesFilePath()
    {
        var item = new InboxItem
        {
            Id = "abc123",
            From = "Adele",
            Role = "code-writer",
            Task = "my-task",
            Brief = "Do the thing",
            FilePath = Path.Combine(Directory.GetCurrentDirectory(), "dydo", "agents", "Brian", "inbox", "abc123-my-task.md")
        };

        var output = CaptureConsoleOutput(() => InboxService.PrintInboxItem(item));

        Assert.Contains("File: dydo/agents/Brian/inbox/abc123-my-task.md", output);
    }

    [Fact]
    public void PrintInboxItem_MessageItem_IncludesFilePath()
    {
        var item = new InboxItem
        {
            Id = "def456",
            From = "Adele",
            Role = "",
            Task = "",
            Brief = "",
            Type = "message",
            Subject = "hello",
            Body = "Hello Brian.",
            FilePath = Path.Combine(Directory.GetCurrentDirectory(), "dydo", "agents", "Brian", "inbox", "def456-msg-hello.md")
        };

        var output = CaptureConsoleOutput(() => InboxService.PrintInboxItem(item));

        Assert.Contains("File: dydo/agents/Brian/inbox/def456-msg-hello.md", output);
    }

    [Fact]
    public void PrintInboxItem_NullFilePath_NoFileLine()
    {
        var item = new InboxItem
        {
            Id = "abc123",
            From = "Adele",
            Role = "code-writer",
            Task = "my-task",
            Brief = "Do the thing"
        };

        var output = CaptureConsoleOutput(() => InboxService.PrintInboxItem(item));

        Assert.DoesNotContain("File:", output);
    }

    private static string CaptureConsoleOutput(Action action)
    {
        var original = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(sw));
        try
        {
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
