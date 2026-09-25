namespace DynaDocs.Tests;

// Wrapped, grouped and shell-launched dangerous commands that both the analyzer and the guard hook must block.
public static class DangerousCommandCases
{
    public static TheoryData<string> WrappedFamilies => new()
    {
        "sudo dd if=/dev/zero of=/dev/sda",
        "sudo chmod -R 777 /",
        "sudo chown -R me /",
        "sudo mkfs.ext4 /dev/sda1",
        "sudo diskutil eraseDisk JHFS+ Blank disk2",
        "sudo gpg --export-secret-keys ABC",
        "sudo -u root rm file.txt",
        "doas dd if=x of=/dev/sda",
        "time dd if=x of=/dev/sda",
        "nohup git push --force origin main",
        "env FOO=1 git push --force",
        "(dd if=x of=/dev/sda)",
        "{ dd if=x of=/dev/sda; }",
        "echo $(dd if=x of=/dev/sda)",
        "echo `dd if=x of=/dev/sda`",
        "bash -c 'git push --force origin main'",
        "bash -lc 'git push --force'",
        "sh -c \"dd if=x of=/dev/sda\"",
        "zsh -c 'gh auth token'",
    };
}
