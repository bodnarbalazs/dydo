namespace DynaDocs.Services;

using System.Text;

public static class ShellCompletionInstaller
{
    private const string Marker = "# dydo shell completions";

    /// <summary>
    /// Detect shell, append completion sourcing line to profile.
    /// Returns a status message on success, or null if skipped/failed.
    /// </summary>
    public static string? Install()
    {
        try
        {
            var (shell, profilePath) = DetectShell();
            if (shell == null || profilePath == null)
                return null;

            return InstallToProfile(shell, profilePath);
        }
        catch
        {
            // Best-effort: never fail init
            return null;
        }
    }

    /// <summary>
    /// Writes completion block to the given profile path if not already present.
    /// Extracted for testability — Install() calls this after detecting the shell.
    /// </summary>
    public static string? InstallToProfile(string shell, string profilePath)
    {
        var sourcingLine = shell switch
        {
            "bash" => "eval \"$(dydo completions bash)\"",
            "zsh" => "eval \"$(dydo completions zsh)\"",
            "powershell" => "dydo completions powershell | Out-String | Invoke-Expression",
            _ => null
        };

        if (sourcingLine == null)
            return null;

        var block = $"\n{Marker}\n{sourcingLine}\n";
        if (shell == "powershell")
        {
            if (!InstallPowerShellProfile(profilePath, block))
                return null;
        }
        else
        {
            if (File.Exists(profilePath) && File.ReadAllText(profilePath).Contains(Marker))
                return null;

            EnsureProfileDirectory(profilePath);
            File.AppendAllText(profilePath, block);
        }

        return $"Shell completions installed ({shell} → {Path.GetFileName(profilePath)})";
    }

    private static bool InstallPowerShellProfile(string profilePath, string block)
    {
        var bytes = File.Exists(profilePath) ? File.ReadAllBytes(profilePath) : [];
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        reader.Peek();
        var encoding = reader.CurrentEncoding;
        var offsets = LegacyInsertionOffsets(bytes, encoding);
        if (offsets.Count > 0)
        {
            // Splice bytes instead of rewriting text: unrelated profile bytes may not decode cleanly.
            using var migrated = new MemoryStream();
            var start = 0;
            foreach (var offset in offsets.Order())
            {
                migrated.Write(bytes.AsSpan(start, offset - start));
                migrated.Write(encoding.GetBytes(" | Out-String"));
                start = offset;
            }
            migrated.Write(bytes.AsSpan(start));
            File.WriteAllBytes(profilePath, migrated.ToArray());
            return true;
        }

        if (bytes.AsSpan().IndexOf(encoding.GetBytes(Marker)) >= 0)
            return false;

        EnsureProfileDirectory(profilePath);
        using var profile = new FileStream(profilePath, FileMode.Append, FileAccess.Write);
        profile.Write(encoding.GetBytes(block));
        return true;
    }

    private static List<int> LegacyInsertionOffsets(byte[] bytes, Encoding encoding)
    {
        var offsets = new List<int>();
        var newline = encoding.GetBytes("\n");
        var crlf = encoding.GetBytes("\r\n");
        var preamble = encoding.GetPreamble();
        var contentStart = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        foreach (var ending in new[] { "\n", "\r\n" })
        {
            var prefix = encoding.GetBytes($"{Marker}{ending}dydo completions powershell");
            var legacy = encoding.GetBytes($"{Marker}{ending}dydo completions powershell | Invoke-Expression");
            var cursor = contentStart;
            while (cursor <= bytes.Length - legacy.Length)
            {
                var relative = bytes.AsSpan(cursor).IndexOf(legacy);
                if (relative < 0) break;
                var index = cursor + relative;
                var startsLine = index == contentStart || bytes.AsSpan(0, index).EndsWith(newline);
                var remainder = bytes.AsSpan(index + legacy.Length);
                var endsLine = remainder.IsEmpty || remainder.StartsWith(newline) || remainder.StartsWith(crlf);
                if (startsLine && endsLine && (index - contentStart) % newline.Length == 0)
                    offsets.Add(index + prefix.Length);
                cursor = index + legacy.Length;
            }
        }
        return offsets;
    }

    private static void EnsureProfileDirectory(string profilePath)
    {
        var dir = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public static (string? Shell, string? ProfilePath) DetectShell()
    {
        // Check $SHELL env var on all platforms first (handles Git Bash on Windows)
        var shellEnv = Environment.GetEnvironmentVariable("SHELL");

        if (!string.IsNullOrEmpty(shellEnv))
        {
            if (shellEnv.EndsWith("/zsh") || shellEnv.EndsWith("\\zsh"))
            {
                var zshrc = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".zshrc");
                return ("zsh", zshrc);
            }

            if (shellEnv.EndsWith("/bash") || shellEnv.EndsWith("\\bash"))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var bashrc = Path.Combine(home, ".bashrc");
                var bashProfile = Path.Combine(home, ".bash_profile");

                // Prefer .bashrc if it exists, otherwise .bash_profile
                var profile = File.Exists(bashrc) ? bashrc : bashProfile;
                return ("bash", profile);
            }
        }

        // If $SHELL is unset and we're on Windows → PowerShell
        if (string.IsNullOrEmpty(shellEnv) && OperatingSystem.IsWindows())
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var profilePath = Path.Combine(docs, "PowerShell", "Microsoft.PowerShell_profile.ps1");
            return ("powershell", profilePath);
        }

        return (null, null);
    }
}
