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
        File.WriteAllText(_profilePath, "# existing content\n");

        ShellCompletionInstaller.InstallToProfile("bash", _profilePath);

        var content = File.ReadAllText(_profilePath);
        Assert.Contains("# dydo shell completions", content);
        Assert.Contains("eval \"$(dydo completions bash)\"", content);
    }

    [Fact]
    public void Idempotent_SecondCallDoesNotDuplicate()
    {
        File.WriteAllText(_profilePath, "# existing content\n");

        ShellCompletionInstaller.InstallToProfile("bash", _profilePath);
        ShellCompletionInstaller.InstallToProfile("bash", _profilePath);

        var content = File.ReadAllText(_profilePath);
        var count = content.Split("# dydo shell completions").Length - 1;
        Assert.Equal(1, count);
    }

    [Fact]
    public void CreatesProfileFile_IfMissing()
    {
        var newProfile = Path.Combine(_testDir, "subdir", ".bashrc");

        ShellCompletionInstaller.InstallToProfile("bash", newProfile);

        Assert.True(File.Exists(newProfile));
        var content = File.ReadAllText(newProfile);
        Assert.Contains("# dydo shell completions", content);
    }

    [Fact]
    public void ZshProfile_ContainsZshSourcingLine()
    {
        var zshrc = Path.Combine(_testDir, ".zshrc");
        File.WriteAllText(zshrc, "");

        ShellCompletionInstaller.InstallToProfile("zsh", zshrc);

        var content = File.ReadAllText(zshrc);
        Assert.Contains("eval \"$(dydo completions zsh)\"", content);
    }

    [Fact]
    public void PowerShellProfile_ContainsInvokeExpression()
    {
        var psProfile = Path.Combine(_testDir, "profile.ps1");
        File.WriteAllText(psProfile, "");

        ShellCompletionInstaller.InstallToProfile("powershell", psProfile);

        var content = File.ReadAllText(psProfile);
        Assert.Contains("dydo completions powershell | Invoke-Expression", content);
    }

    [Fact]
    public void DetectShell_ReturnsShellAndProfile()
    {
        var (shell, profilePath) = ShellCompletionInstaller.DetectShell();

        // On any platform, should return either a known shell or null
        if (shell != null)
        {
            Assert.True(shell is "bash" or "zsh" or "powershell",
                $"Unexpected shell: {shell}");
            Assert.NotNull(profilePath);
            Assert.NotEmpty(profilePath);
        }
    }

    [Fact]
    public void DetectShell_WithZshEnv_ReturnsZsh()
    {
        var original = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", "/usr/bin/zsh");
            var (shell, profilePath) = ShellCompletionInstaller.DetectShell();

            Assert.Equal("zsh", shell);
            Assert.NotNull(profilePath);
            Assert.EndsWith(".zshrc", profilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL", original);
        }
    }

    [Fact]
    public void DetectShell_WithBashEnv_ReturnsBash()
    {
        var original = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", "/bin/bash");
            var (shell, profilePath) = ShellCompletionInstaller.DetectShell();

            Assert.Equal("bash", shell);
            Assert.NotNull(profilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL", original);
        }
    }

    [Fact]
    public void DetectShell_WithBackslashBash_ReturnsBash()
    {
        var original = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", "C:\\Git\\usr\\bin\\bash");
            var (shell, profilePath) = ShellCompletionInstaller.DetectShell();

            Assert.Equal("bash", shell);
            Assert.NotNull(profilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL", original);
        }
    }

    [Fact]
    public void DetectShell_WithUnknownShell_ReturnsNull()
    {
        var original = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", "/usr/bin/fish");
            var (shell, profilePath) = ShellCompletionInstaller.DetectShell();

            Assert.Null(shell);
            Assert.Null(profilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL", original);
        }
    }

    [Fact]
    public void DetectShell_NoShellEnvOnWindows_ReturnsPowerShell()
    {
        if (!OperatingSystem.IsWindows()) return;

        var original = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", null);
            var (shell, profilePath) = ShellCompletionInstaller.DetectShell();

            Assert.Equal("powershell", shell);
            Assert.NotNull(profilePath);
            Assert.EndsWith(".ps1", profilePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL", original);
        }
    }

    [Fact]
    public void Install_DoesNotThrow()
    {
        // Install() is best-effort and should never throw regardless of env
        var result = ShellCompletionInstaller.Install();

        // result can be null (already installed/skipped) or a success message
        if (result != null)
        {
            Assert.Contains("Shell completions installed", result);
        }
    }

    [Fact]
    public void Install_WithUnknownShell_ReturnsNull()
    {
        var original = Environment.GetEnvironmentVariable("SHELL");
        try
        {
            Environment.SetEnvironmentVariable("SHELL", "/usr/bin/fish");
            var result = ShellCompletionInstaller.Install();
            Assert.Null(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHELL", original);
        }
    }

    [Fact]
    public void Install_IsIdempotent()
    {
        // Calling Install twice should not duplicate the block
        ShellCompletionInstaller.Install();
        var secondResult = ShellCompletionInstaller.Install();

        // Second call should return null (already installed)
        Assert.Null(secondResult);
    }

    [Fact]
    public void InstallToProfile_WritesToNewFile()
    {
        var profile = Path.Combine(_testDir, "new-profile.sh");

        var result = ShellCompletionInstaller.InstallToProfile("bash", profile);

        Assert.NotNull(result);
        Assert.Contains("Shell completions installed", result);
        var content = File.ReadAllText(profile);
        Assert.Contains("# dydo shell completions", content);
        Assert.Contains("eval \"$(dydo completions bash)\"", content);
    }

    [Fact]
    public void InstallToProfile_Zsh_WritesCorrectLine()
    {
        var profile = Path.Combine(_testDir, ".zshrc");

        var result = ShellCompletionInstaller.InstallToProfile("zsh", profile);

        Assert.NotNull(result);
        var content = File.ReadAllText(profile);
        Assert.Contains("eval \"$(dydo completions zsh)\"", content);
    }

    [Fact]
    public void InstallToProfile_PowerShell_WritesCorrectLine()
    {
        var profile = Path.Combine(_testDir, "profile.ps1");

        var result = ShellCompletionInstaller.InstallToProfile("powershell", profile);

        Assert.NotNull(result);
        var content = File.ReadAllText(profile);
        Assert.Contains("dydo completions powershell | Invoke-Expression", content);
    }

    [Fact]
    public void InstallToProfile_SkipsIfMarkerExists()
    {
        var profile = Path.Combine(_testDir, ".bashrc");
        File.WriteAllText(profile, "# existing\n# dydo shell completions\neval stuff\n");

        var result = ShellCompletionInstaller.InstallToProfile("bash", profile);

        Assert.Null(result);
    }

    [Fact]
    public void InstallToProfile_AppendsToExistingProfile()
    {
        var profile = Path.Combine(_testDir, ".bashrc");
        File.WriteAllText(profile, "# existing content\n");

        var result = ShellCompletionInstaller.InstallToProfile("bash", profile);

        Assert.NotNull(result);
        var content = File.ReadAllText(profile);
        Assert.Contains("# existing content", content);
        Assert.Contains("# dydo shell completions", content);
    }

    [Fact]
    public void InstallToProfile_CreatesDirectory()
    {
        var profile = Path.Combine(_testDir, "subdir", "nested", ".bashrc");

        var result = ShellCompletionInstaller.InstallToProfile("bash", profile);

        Assert.NotNull(result);
        Assert.True(File.Exists(profile));
    }

    [Fact]
    public void InstallToProfile_UnknownShell_ReturnsNull()
    {
        var profile = Path.Combine(_testDir, ".fishrc");

        var result = ShellCompletionInstaller.InstallToProfile("fish", profile);

        Assert.Null(result);
    }

    [Fact]
    public void InstallToProfile_IsIdempotent()
    {
        var profile = Path.Combine(_testDir, ".bashrc");

        ShellCompletionInstaller.InstallToProfile("bash", profile);
        var secondResult = ShellCompletionInstaller.InstallToProfile("bash", profile);

        Assert.Null(secondResult);
        var content = File.ReadAllText(profile);
        var count = content.Split("# dydo shell completions").Length - 1;
        Assert.Equal(1, count);
    }

}
