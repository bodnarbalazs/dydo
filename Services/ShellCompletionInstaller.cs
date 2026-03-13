namespace DynaDocs.Services;

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
        // Check for marker (idempotent)
        if (File.Exists(profilePath))
        {
            var content = File.ReadAllText(profilePath);
            if (content.Contains(Marker))
                return null;
        }

        // Ensure directory exists
        var dir = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sourcingLine = shell switch
        {
            "bash" => "eval \"$(dydo completions bash)\"",
            "zsh" => "eval \"$(dydo completions zsh)\"",
            "powershell" => "dydo completions powershell | Invoke-Expression",
            _ => null
        };

        if (sourcingLine == null)
            return null;

        var block = $"\n{Marker}\n{sourcingLine}\n";
        File.AppendAllText(profilePath, block);

        return $"Shell completions installed ({shell} → {Path.GetFileName(profilePath)})";
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
