namespace DynaDocs.Services;

using System.Text;

/// <summary>One finished git call. <see cref="Output"/> keeps the raw bytes, so a binary patch survives.</summary>
internal sealed record GitResult(int ExitCode, byte[] Output, string Error)
{
    public bool Ok => ExitCode == 0;

    public string Text => Encoding.UTF8.GetString(Output);

    /// <summary>The NUL-separated entries of a <c>-z</c> listing.</summary>
    public string[] Entries => Text.Split('\0', StringSplitOptions.RemoveEmptyEntries);
}
