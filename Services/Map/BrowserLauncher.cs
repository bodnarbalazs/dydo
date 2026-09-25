namespace DynaDocs.Services.Map;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>Opens a URL in the default browser: the shell on Windows, `open` on macOS, `xdg-open` elsewhere.</summary>
internal static class BrowserLauncher
{
    /// <param name="start">Starts the opener; production passes <see cref="Process.Start(ProcessStartInfo)"/>.</param>
    public static void Open(Uri url, Func<ProcessStartInfo, Process?> start)
    {
        try
        {
            using var process = start(StartInfo(url, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()));
        }
        catch (Win32Exception)
        {
            // A headless machine may have no opener; the URL is already printed.
            Console.Error.WriteLine("Could not open a browser; open the URL above yourself.");
        }
    }

    internal static ProcessStartInfo StartInfo(Uri url, bool windows, bool macOS)
    {
        // .NET does not shell-execute by default, and only Windows can shell-execute a URL.
        if (windows)
            return new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true };

        var startInfo = new ProcessStartInfo(macOS ? "open" : "xdg-open");
        startInfo.ArgumentList.Add(url.AbsoluteUri);
        return startInfo;
    }
}
