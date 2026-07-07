namespace DynaDocs.Tests;

/// <summary>Makes <see cref="File.Delete(string)"/> fail for the given file for this object's lifetime, on any
/// OS. On Windows an exclusive share-lock blocks deletion; on Unix — where an open handle does NOT block unlink
/// (POSIX allows deleting an open file) — the parent directory is made non-writable instead, since unlink
/// permission is governed by the directory, not the file. Restores the original mode on <see cref="Dispose"/>.
/// Lets a delete-failure test exercise the same abort path on both the dev (Windows) and CI (Linux) OSes.</summary>
public sealed class UndeletableFile : IDisposable
{
    private readonly FileStream? _lock;
    private readonly string? _dir;
    private readonly UnixFileMode _originalMode;

    public UndeletableFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            _lock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return;
        }

        _dir = Path.GetDirectoryName(path)!;
        _originalMode = File.GetUnixFileMode(_dir);
        File.SetUnixFileMode(_dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
    }

    public void Dispose()
    {
        _lock?.Dispose();
        if (!OperatingSystem.IsWindows() && _dir != null)
            File.SetUnixFileMode(_dir, _originalMode);
    }
}
