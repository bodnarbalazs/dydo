namespace DynaDocs.Tests.Services;

using DynaDocs.Services;
using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

public class ShellCompletionInstallerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _profilePath;
    private readonly ITestOutputHelper _output;
    private bool _retainScratch;

    public ShellCompletionInstallerTests(ITestOutputHelper output)
    {
        _output = output;
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-installer-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _profilePath = Path.Combine(_testDir, ".bashrc");
    }

    public void Dispose()
    {
        if (_retainScratch)
        {
            _output.WriteLine($"Cleanup was not confirmed; retained scratch directory: {_testDir}");
            return;
        }
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
        Assert.Contains("dydo completions powershell | Out-String | Invoke-Expression", content);
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
        Assert.Contains("dydo completions powershell | Out-String | Invoke-Expression", content);
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

    public static TheoryData<string, string> ProfileEncodings => new()
    {
        { "utf8", "" }, { "utf8", "\n" },
        { "utf8-bom", "" }, { "utf8-bom", "\n" },
        { "utf16-le", "" }, { "utf16-le", "\n" },
        { "utf16-be", "" }, { "utf16-be", "\n" },
        { "utf32-le", "" }, { "utf32-le", "\n" },
        { "utf32-be", "" }, { "utf32-be", "\n" }
    };

    [Theory]
    [MemberData(nameof(ProfileEncodings))]
    public void PowerShellAppend_PreservesOriginalBytesAndEncoding(string encodingName, string ending)
    {
        var encoding = ProfileEncoding(encodingName);
        var original = EncodeProfile(encoding, "# árvíztűrő 日本語" + ending);
        File.WriteAllBytes(_profilePath, original);

        var result = ShellCompletionInstaller.InstallToProfile("powershell", _profilePath);

        Assert.Contains("Shell completions installed", result);
        var expected = original.Concat(encoding.GetBytes(
            "\n# dydo shell completions\ndydo completions powershell | Out-String | Invoke-Expression\n")).ToArray();
        Assert.Equal(expected, File.ReadAllBytes(_profilePath));
        Assert.Null(ShellCompletionInstaller.InstallToProfile("powershell", _profilePath));
        Assert.Equal(expected, File.ReadAllBytes(_profilePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PowerShellMissingOrEmptyProfile_UsesUtf8WithoutBom(bool exists)
    {
        if (exists) File.WriteAllBytes(_profilePath, []);

        ShellCompletionInstaller.InstallToProfile("powershell", _profilePath);

        Assert.Equal(Encoding.UTF8.GetBytes(
            "\n# dydo shell completions\ndydo completions powershell | Out-String | Invoke-Expression\n"),
            File.ReadAllBytes(_profilePath));
    }

    [Theory]
    [MemberData(nameof(ProfileEncodings))]
    public void PowerShellMigration_PreservesEncodingAndOtherBytes(string encodingName, string ending)
    {
        var encoding = ProfileEncoding(encodingName);
        const string before = "# árvíztűrő\r\n# dydo shell completions\n";
        const string legacy = "dydo completions powershell | Invoke-Expression";
        const string corrected = "dydo completions powershell | Out-String | Invoke-Expression";
        var original = EncodeProfile(encoding, before + legacy + ending);
        File.WriteAllBytes(_profilePath, original);

        Assert.NotNull(ShellCompletionInstaller.InstallToProfile("powershell", _profilePath));

        var expected = EncodeProfile(encoding, before + corrected + ending);
        Assert.Equal(expected, File.ReadAllBytes(_profilePath));
        Assert.Null(ShellCompletionInstaller.InstallToProfile("powershell", _profilePath));
        Assert.Equal(expected, File.ReadAllBytes(_profilePath));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void PowerShellMigration_RepairsEveryExactBlockPreservingUnrelatedInvalidBytes(string newline)
    {
        var legacy = Encoding.UTF8.GetBytes(
            $"# dydo shell completions{newline}dydo completions powershell | Invoke-Expression{newline}");
        var corrected = Encoding.UTF8.GetBytes(
            $"# dydo shell completions{newline}dydo completions powershell | Out-String | Invoke-Expression{newline}");
        byte[] unrelated = [0xff, 0xfe, 0x80, (byte)'\n'];
        var original = legacy.Concat(unrelated).Concat(legacy).Concat(unrelated).ToArray();
        File.WriteAllBytes(_profilePath, original);

        ShellCompletionInstaller.InstallToProfile("powershell", _profilePath);

        Assert.Equal(corrected.Concat(unrelated).Concat(corrected).Concat(unrelated).ToArray(),
            File.ReadAllBytes(_profilePath));
    }

    [Theory]
    [InlineData("# dydo shell completions\ndydo completions powershell | Out-String | Invoke-Expression\n")]
    [InlineData("# dydo shell completions\n dydo completions powershell | Invoke-Expression\n")]
    [InlineData("# dydo shell completions\ndydo completions powershell | Invoke-Expression \n")]
    [InlineData("# dydo shell completions\ndydo completions powershell | Invoke-Expression # custom\n")]
    [InlineData("prefix # dydo shell completions\ndydo completions powershell | Invoke-Expression\n")]
    [InlineData("# dydo shell completions suffix\ndydo completions powershell | Invoke-Expression\n")]
    [InlineData("# dydo shell completions\n\ndydo completions powershell | Invoke-Expression\n")]
    [InlineData("# dydo shell completions\nWrite-Output 'custom'\ndydo completions powershell | Invoke-Expression\n")]
    public void PowerShellCustomizedBlock_IsByteIdentical(string content)
    {
        var original = EncodeProfile(new UnicodeEncoding(false, true), content);
        File.WriteAllBytes(_profilePath, original);

        Assert.Null(ShellCompletionInstaller.InstallToProfile("powershell", _profilePath));

        Assert.Equal(original, File.ReadAllBytes(_profilePath));
    }

    [Fact]
    public void PowerShellUnmarkedLegacyLine_IsPreservedWhenAppending()
    {
        const string original = "dydo completions powershell | Invoke-Expression";
        File.WriteAllText(_profilePath, original);

        ShellCompletionInstaller.InstallToProfile("powershell", _profilePath);

        Assert.Equal(original + "\n# dydo shell completions\ndydo completions powershell | Out-String | Invoke-Expression\n",
            File.ReadAllText(_profilePath));
    }

    [Theory]
    [MemberData(nameof(ProfileEncodings))]
    public void PowerShellMigration_MixedExactAndCustomizedBlocks_PreservesEveryOtherByte(string encodingName, string ending)
    {
        var encoding = ProfileEncoding(encodingName);
        const string legacy = "dydo completions powershell | Invoke-Expression";
        const string corrected = "dydo completions powershell | Out-String | Invoke-Expression";
        const string marker = "# dydo shell completions";
        var custom = $"\n# 日本語\r\n{marker}\n {legacy}\n{marker} suffix\n{legacy}\n";
        var original = EncodeProfile(encoding, $"{marker}\r\n{legacy}{custom}{marker}\n{legacy}{ending}");
        var expected = EncodeProfile(encoding, $"{marker}\r\n{corrected}{custom}{marker}\n{corrected}{ending}");
        File.WriteAllBytes(_profilePath, original);

        Assert.NotNull(ShellCompletionInstaller.InstallToProfile("powershell", _profilePath));
        Assert.Equal(expected, File.ReadAllBytes(_profilePath));
        Assert.Null(ShellCompletionInstaller.InstallToProfile("powershell", _profilePath));
        Assert.Equal(expected, File.ReadAllBytes(_profilePath));
    }

    [Theory]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public void PowerShellMigration_UnalignedLegacyBytes_AreUnchanged(string encodingName)
    {
        var encoding = ProfileEncoding(encodingName);
        var original = EncodeProfile(encoding, "# dydo shell completions\nWrite-Output 'custom'\n")
            .Concat(new byte[] { 0xff })
            .Concat(encoding.GetBytes("\n# dydo shell completions\ndydo completions powershell | Invoke-Expression"))
            .ToArray();
        File.WriteAllBytes(_profilePath, original);

        Assert.Null(ShellCompletionInstaller.InstallToProfile("powershell", _profilePath));
        Assert.Equal(original, File.ReadAllBytes(_profilePath));
    }

    private static Encoding ProfileEncoding(string name) => name switch
    {
        "utf8" => new UTF8Encoding(false),
        "utf8-bom" => new UTF8Encoding(true),
        "utf16-le" => new UnicodeEncoding(false, true),
        "utf16-be" => new UnicodeEncoding(true, true),
        "utf32-le" => new UTF32Encoding(false, true),
        "utf32-be" => new UTF32Encoding(true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static byte[] EncodeProfile(Encoding encoding, string content) =>
        encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray();

    public static IEnumerable<object[]> NativeProfiles()
    {
        yield return ["utf8", "missing", ""];
        yield return ["utf8", "empty", ""];
        foreach (var name in new[] { "utf8", "utf8-bom", "utf16-le", "utf16-be", "utf32-le", "utf32-be" })
        foreach (var ending in new[] { "", "\n" })
        foreach (var kind in new[] { "append", "legacy" })
            yield return [name, kind, ending];
    }

    [Theory]
    [MemberData(nameof(NativeProfiles))]
    public async Task PowerShellNativeCandidate_SourcesInstalledProfile(string encodingName, string kind, string ending)
    {
        if (!OperatingSystem.IsWindows()) return;
        var profilePath = Path.Combine(_testDir, "native-profile.ps1");
        var content = kind == "legacy"
            ? "# árvíztűrő 日本語\r\n# dydo shell completions\r\ndydo completions powershell | Invoke-Expression" + ending
            : "# árvíztűrő 日本語" + ending;
        if (kind != "missing")
            File.WriteAllBytes(profilePath, kind == "empty" ? [] : EncodeProfile(ProfileEncoding(encodingName), content));
        Assert.NotNull(ShellCompletionInstaller.InstallToProfile("powershell", profilePath));
        var installed = File.ReadAllBytes(profilePath);
        Assert.Null(ShellCompletionInstaller.InstallToProfile("powershell", profilePath));
        Assert.Equal(installed, File.ReadAllBytes(profilePath));

        var candidate = CandidateApphost();
        var result = await RunPowerShellAsync("""
            $ErrorActionPreference = 'Stop'
            $env:PATH = [IO.Path]::GetDirectoryName($env:COMPLETION_CANDIDATE) + [IO.Path]::PathSeparator + $env:PATH
            $actual = (Get-Command dydo -CommandType Application | Select-Object -First 1).Source
            if ($actual -ne $env:COMPLETION_CANDIDATE) { throw "Wrong native executable: $actual" }
            Write-Output "Candidate: $actual"
            Write-Output "Version: $(& $actual --version)"
            . $env:COMPLETION_PROFILE
            $matches = [System.Management.Automation.CommandCompletion]::CompleteInput('dydo ch', 7, $null).CompletionMatches
            if (@($matches | Where-Object CompletionText -eq 'check').Count -eq 0) { throw 'Expected check completion was absent' }
            Write-Output 'PASS: check completion'
            """, new Dictionary<string, string>
            {
                ["COMPLETION_CANDIDATE"] = candidate,
                ["COMPLETION_PROFILE"] = profilePath
            });

        _output.WriteLine(result.Stdout);
        _output.WriteLine($"PROFILE:{encodingName}:{kind}:{ending.Length}:{Convert.ToBase64String(installed)}");
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Stderr);
        Assert.Contains("PASS: check completion", result.Stdout);
    }

    [Fact]
    public async Task NativeCapture_DrainsBothPipesBeyondBufferCapacity()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunPowerShellAsync("""
            1..10000 | ForEach-Object { [Console]::Out.WriteLine(('o' * 128)) }
            1..10000 | ForEach-Object { [Console]::Error.WriteLine(('e' * 128)) }
            """);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(10000, result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Equal(10000, result.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        _output.WriteLine($"Captured stdout={result.Stdout.Length}, stderr={result.Stderr.Length} characters concurrently.");
    }

    [Fact]
    public async Task NativeTimeout_KillsDescendantAndDrainsBeforeScratchDeletion()
    {
        if (!OperatingSystem.IsWindows()) return;
        var elapsed = Stopwatch.StartNew();
        var failure = await Assert.ThrowsAsync<TimeoutException>(() => RunPowerShellAsync("""
            $child = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList '-NoProfile', '-NonInteractive', '-Command', 'Start-Sleep -Seconds 120' -PassThru -WindowStyle Hidden
            [Console]::Out.WriteLine("PARENT:$PID")
            [Console]::Out.WriteLine("CHILD:$($child.Id)")
            [Console]::Out.Flush()
            [Console]::Error.WriteLine('stalling')
            [Console]::Error.Flush()
            Start-Sleep -Seconds 120
            """, executionTimeout: TimeSpan.FromSeconds(3)));

        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15), failure.Message);
        Assert.Equal(true, failure.Data["CleanupConfirmed"]);
        Assert.False(_retainScratch);
        var stdout = Assert.IsType<string>(failure.Data["Stdout"]);
        Assert.Contains("stalling", Assert.IsType<string>(failure.Data["Stderr"]));
        foreach (var prefix in new[] { "PARENT:", "CHILD:" })
        {
            var line = Assert.Single(stdout.Split('\n'), line => line.StartsWith(prefix, StringComparison.Ordinal));
            var pid = int.Parse(line[prefix.Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                using var remaining = Process.GetProcessById(pid);
                Assert.True(remaining.HasExited, $"Native process {pid} is still running.");
            }
            catch (ArgumentException) { }
        }
        _output.WriteLine(failure.Message);
        _output.WriteLine(stdout);
    }

    [Fact]
    public async Task NativeFailure_RetainsBothStreamsAndExitCodeAfterCleanup()
    {
        if (!OperatingSystem.IsWindows()) return;
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => RunPowerShellAsync("""
            [Console]::Out.WriteLine('before failure')
            [Console]::Error.WriteLine('failure detail')
            exit 23
            """));

        Assert.Equal(true, failure.Data["CleanupConfirmed"]);
        Assert.Contains("before failure", Assert.IsType<string>(failure.Data["Stdout"]));
        Assert.Contains("failure detail", Assert.IsType<string>(failure.Data["Stderr"]));
        Assert.Contains("exit=23", failure.Message);
        Assert.Contains("before failure", failure.Message);
        Assert.Contains("failure detail", failure.Message);
        Assert.False(_retainScratch);
    }

    private static string CandidateApphost()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "DynaDocs.csproj")))
            root = root.Parent;
        Assert.NotNull(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var candidate = Path.Combine(root.FullName, "bin", configuration, "net10.0", "dydo.exe");
        Assert.True(File.Exists(candidate), $"Candidate apphost is missing: {candidate}");
        return candidate;
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunPowerShellAsync(
        string command, Dictionary<string, string>? environment = null, TimeSpan? executionTimeout = null)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _testDir
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", command })
            startInfo.ArgumentList.Add(argument);
        startInfo.Environment["SHELL"] = "dydo-test-no-shell";
        foreach (var entry in environment ?? [])
            startInfo.Environment[entry.Key] = entry.Value;
        using var process = new Process { StartInfo = startInfo };
        var elapsed = Stopwatch.StartNew();
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Exception? failure = null;
        try
        {
            await process.WaitForExitAsync().WaitAsync(executionTimeout ?? TimeSpan.FromSeconds(30));
        }
        catch (Exception error)
        {
            failure = error;
        }

        using var teardown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var cleanupErrors = new List<string>();
        if (failure != null && !process.HasExited)
        {
            try
            {
                await Task.Run(() => process.Kill(entireProcessTree: true)).WaitAsync(teardown.Token);
            }
            catch (Exception error)
            {
                if (!process.HasExited) cleanupErrors.Add($"Termination: {error.Message}");
            }
        }
        try
        {
            await process.WaitForExitAsync().WaitAsync(teardown.Token);
            await Task.WhenAll(stdout, stderr).WaitAsync(teardown.Token);
        }
        catch (Exception error)
        {
            cleanupErrors.Add($"Exit/drain: {error.Message}");
        }

        var capturedOut = stdout.IsCompletedSuccessfully ? await stdout : "<stdout capture incomplete>";
        var capturedErr = stderr.IsCompletedSuccessfully ? await stderr : "<stderr capture incomplete>";
        var exitCode = process.HasExited ? process.ExitCode : (int?)null;
        var cleanupConfirmed = process.HasExited && stdout.IsCompleted && stderr.IsCompleted && cleanupErrors.Count == 0;
        if (!cleanupConfirmed)
        {
            _retainScratch = true;
            ObserveCaptureFailure(stdout);
            ObserveCaptureFailure(stderr);
        }
        if (failure != null || !cleanupConfirmed || exitCode != 0)
        {
            var diagnostic = $"Native process failed after {elapsed.Elapsed}; exit={exitCode}; cleanup={cleanupConfirmed}; " +
                $"scratch={_testDir}; {string.Join("; ", cleanupErrors)}\nstdout:\n{capturedOut}\nstderr:\n{capturedErr}";
            Exception reported = failure is TimeoutException
                ? new TimeoutException(diagnostic, failure)
                : new InvalidOperationException(diagnostic, failure);
            reported.Data["CleanupConfirmed"] = cleanupConfirmed;
            reported.Data["Stdout"] = capturedOut;
            reported.Data["Stderr"] = capturedErr;
            throw reported;
        }
        _output.WriteLine($"Native exit={exitCode}; elapsed={elapsed.Elapsed}; cleanup confirmed.");
        return (exitCode!.Value, capturedOut, capturedErr);
    }

    private static void ObserveCaptureFailure(Task task) =>
        _ = task.ContinueWith(completed => _ = completed.Exception,
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
