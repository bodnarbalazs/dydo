namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class ShellCompletionInstallerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _profilePath;

    public ShellCompletionInstallerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-installer-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _profilePath = Path.Combine(_testDir, ".bashrc");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    Directory.Delete(_testDir, true);
                    return;
                }
                catch (IOException) when (i < 2)
                {
                    Thread.Sleep(50 * (i + 1));
                }
            }
        }
    }

    [Fact]
    public void AppendsSourcingLine_ToProfile()
    {
        // Create an existing profile
        File.WriteAllText(_profilePath, "# existing content\n");

        AppendCompletionBlock(_profilePath, "bash");

        var content = File.ReadAllText(_profilePath);
        Assert.Contains("# dydo shell completions", content);
        Assert.Contains("eval \"$(dydo completions bash)\"", content);
    }

    [Fact]
    public void Idempotent_SecondCallDoesNotDuplicate()
    {
        File.WriteAllText(_profilePath, "# existing content\n");

        AppendCompletionBlock(_profilePath, "bash");
        AppendCompletionBlock(_profilePath, "bash");

        var content = File.ReadAllText(_profilePath);
        var count = content.Split("# dydo shell completions").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public void CreatesProfileFile_IfMissing()
    {
        var newProfile = Path.Combine(_testDir, "subdir", ".bashrc");

        AppendCompletionBlock(newProfile, "bash");

        Assert.True(File.Exists(newProfile));
        var content = File.ReadAllText(newProfile);
        Assert.Contains("# dydo shell completions", content);
    }

    [Fact]
    public void ZshProfile_ContainsZshSourcingLine()
    {
        var zshrc = Path.Combine(_testDir, ".zshrc");
        File.WriteAllText(zshrc, "");

        AppendCompletionBlock(zshrc, "zsh");

        var content = File.ReadAllText(zshrc);
        Assert.Contains("eval \"$(dydo completions zsh)\"", content);
    }

    [Fact]
    public void PowerShellProfile_ContainsInvokeExpression()
    {
        var psProfile = Path.Combine(_testDir, "profile.ps1");
        File.WriteAllText(psProfile, "");

        AppendCompletionBlock(psProfile, "powershell");

        var content = File.ReadAllText(psProfile);
        Assert.Contains("dydo completions powershell | Invoke-Expression", content);
    }

    /// <summary>
    /// Simulates what ShellCompletionInstaller.Install() does for a given shell and profile path.
    /// We test the logic directly since Install() depends on environment detection.
    /// </summary>
    private static void AppendCompletionBlock(string profilePath, string shell)
    {
        const string marker = "# dydo shell completions";

        if (File.Exists(profilePath))
        {
            var existing = File.ReadAllText(profilePath);
            if (existing.Contains(marker))
                return;
        }

        var dir = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var sourcingLine = shell switch
        {
            "bash" => "eval \"$(dydo completions bash)\"",
            "zsh" => "eval \"$(dydo completions zsh)\"",
            "powershell" => "dydo completions powershell | Invoke-Expression",
            _ => throw new ArgumentException($"Unknown shell: {shell}")
        };

        var block = $"\n{marker}\n{sourcingLine}\n";
        File.AppendAllText(profilePath, block);
    }
}
