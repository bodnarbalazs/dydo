namespace DynaDocs.Services.Statechart;

using Microsoft.Win32.SafeHandles;

internal sealed class StatechartScratch : IDisposable
{
    public string Path { get; }
    public string Journal { get; }
    private readonly string _identity;
    private SafeFileHandle? _registered;

    private StatechartScratch(string path, string journal, string identity)
    {
        Path = path;
        Journal = journal;
        _identity = identity;
    }

    public static StatechartScratch Reserve(string parent, string state)
    {
        StatechartPaths.RequirePlainDirectory(parent);
        StatechartPaths.RequirePlainDirectory(state);
        var nonce = Guid.NewGuid().ToString("N");
        var path = System.IO.Path.Combine(parent, ".statechart-" + nonce);
        var journal = System.IO.Path.Combine(state, "scratch-" + nonce + ".journal");
        using (var stream = new FileStream(journal, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream)) writer.WriteLine("reserving\n" + path);
        if (OperatingSystem.IsWindows()) WindowsStatechartFiles.CreateExclusive(path);
        else PosixStatechartFiles.CreateExclusive(path);
        using var handle = Open(path);
        var identity = Identity(handle);
        File.AppendAllText(journal, "created-unregistered\n" + identity + "\n");
        return new StatechartScratch(path, journal, identity);
    }

    public void Register()
    {
        if (_registered is not null) throw new InvalidOperationException("Scratch is already registered.");
        var handle = Open(Path);
        try
        {
            if (Identity(handle) != _identity)
                throw new IOException("Statechart scratch was replaced before registration; retaining it.");
            File.AppendAllText(Journal, "registered\n");
            _registered = handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal bool CleanupAfterDrain()
    {
        if (_registered is null)
        {
            Retain("unregistered: no deletion authority");
            return false;
        }
        if (Identity(_registered) != _identity)
        {
            Retain("identity mismatch");
            return false;
        }
        if (!OperatingSystem.IsWindows())
        {
            if (!PosixStatechartFiles.DeleteRegistered(Path, _registered))
            {
                Retain("directory path identity changed; foreign replacement retained");
                return false;
            }
        }
        else DeleteWindows();
        _registered.Dispose();
        _registered = null;
        File.AppendAllText(Journal, "deleted-after-authoritative-drain\n");
        return true;
    }

    private void DeleteWindows()
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(Path))
        {
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                Directory.Delete(entry, recursive: true);
            else File.Delete(entry);
        }
        WindowsStatechartFiles.DeleteOnClose(_registered!);
    }

    private static SafeFileHandle Open(string path) => OperatingSystem.IsWindows()
        ? WindowsStatechartFiles.Open(path) : PosixStatechartFiles.Open(path);

    private static string Identity(SafeFileHandle handle) => OperatingSystem.IsWindows()
        ? WindowsStatechartFiles.Identity(handle) : PosixStatechartFiles.Identity(handle);

    public void Retain(string reason) => File.AppendAllText(Journal, "retained: " + reason + "\n");
    public void Dispose() => _registered?.Dispose();
}
