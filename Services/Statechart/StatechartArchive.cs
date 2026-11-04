namespace DynaDocs.Services.Statechart;

using System.Formats.Tar;
using System.IO.Compression;

internal sealed class StatechartArchive
{
    private readonly string _root;
    private readonly long _limit;
    private readonly int _entryLimit;
    private readonly CancellationToken _token;
    private readonly Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _directories = new(StringComparer.Ordinal);
    private readonly HashSet<string> _members = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Target, bool Hard)> _links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnixFileMode> _modes = new(StringComparer.Ordinal);
    private long _expanded;

    private StatechartArchive(string root, long limit, int entryLimit, CancellationToken token)
    {
        _root = root;
        _limit = limit;
        _entryLimit = entryLimit;
        _token = token;
    }

    public static void Extract(string archive, string destination, CancellationToken token,
        long expandedLimit = 1024L * 1024 * 1024, int entryLimit = 20000)
    {
        token.ThrowIfCancellationRequested();
        if (expandedLimit is <= 0 or > 1024L * 1024 * 1024 || entryLimit is <= 0 or > 20000)
            throw new ArgumentOutOfRangeException(nameof(expandedLimit), "Extraction bounds may only be reduced from the pinned maximum.");
        var root = Path.GetFullPath(destination);
        StatechartPaths.RequirePlainDirectory(Path.GetDirectoryName(root)!);
        if (Directory.Exists(root) || File.Exists(root) || new DirectoryInfo(root).LinkTarget is not null)
            throw new IOException("Statechart archive staging already exists; foreign content retained.");
        Directory.CreateDirectory(root);
        var extraction = new StatechartArchive(root, expandedLimit, entryLimit, token);
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) extraction.Zip(archive);
        else if (archive.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || archive.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            extraction.Tar(archive);
        else throw new IOException("Unsupported locked statechart archive format.");
        extraction.Links();
        extraction.DirectoryModes();
    }

    private void Zip(string archive)
    {
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            var directory = entry.FullName.EndsWith('/');
            var kind = (entry.ExternalAttributes >> 16) & 0xf000;
            if (kind != 0 && kind != (directory ? 0x4000 : 0x8000))
                throw new IOException("Unsupported statechart ZIP link/device entry.");
            var name = Reserve(entry.FullName, directory);
            var mode = (UnixFileMode)((entry.ExternalAttributes >> 16) & 0x1ff);
            if (directory) Directory.CreateDirectory(Full(name));
            else
            {
                using var stream = entry.Open();
                Write(name, stream, entry.Length, mode);
            }
            if (directory && mode != 0) _modes[name] = mode;
        }
    }

    private void Tar(string archive)
    {
        using var file = File.OpenRead(archive);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            var directory = entry.EntryType == TarEntryType.Directory;
            var name = Reserve(entry.Name, directory);
            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(Full(name));
                    _modes[name] = entry.Mode & (UnixFileMode)0x1ff;
                    break;
                case TarEntryType.RegularFile:
                case TarEntryType.V7RegularFile:
                    using (var data = entry.DataStream ?? Stream.Null)
                        Write(name, data, entry.Length, entry.Mode);
                    break;
                case TarEntryType.HardLink:
                case TarEntryType.SymbolicLink:
                    _links.Add(name, (entry.LinkName, entry.EntryType == TarEntryType.HardLink));
                    break;
                default:
                    throw new IOException("Unsupported statechart tar device/special entry: " + entry.EntryType);
            }
        }
    }

    private string Reserve(string name, bool directory)
    {
        _token.ThrowIfCancellationRequested();
        if (directory && name.EndsWith('/')) name = name[..^1];
        ValidateName(name);
        if (_members.Count >= _entryLimit) throw new IOException("Statechart archive entry bound exceeded.");
        if (!_members.Add(name)) throw new IOException("Duplicate/case-colliding statechart archive entry: " + name);
        var parts = name.Split('/');
        for (var index = 0; index < parts.Length; index++)
        {
            var part = string.Join('/', parts.Take(index + 1));
            var isDirectory = index < parts.Length - 1 || directory;
            if (_names.TryGetValue(part, out var existing))
            {
                if (existing != part || _directories[existing] != isDirectory)
                    throw new IOException("Statechart archive path/ancestor collision: " + name);
            }
            else
            {
                _names.Add(part, part);
                _directories.Add(part, isDirectory);
            }
            if (isDirectory) Directory.CreateDirectory(Full(part));
        }
        return name;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.StartsWith('/') || name.Contains('\\') || name.Contains(':'))
            throw new IOException("Unsafe statechart archive path: " + name);
        foreach (var part in name.Split('/'))
        {
            var device = part.Split('.')[0];
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.Any(character => character < 32 || "<>\"|?*".Contains(character)) ||
                new[] { "CON", "PRN", "AUX", "NUL" }.Contains(device, StringComparer.OrdinalIgnoreCase) ||
                device.Length == 4 && (device.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                    device.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && device[3] is >= '1' and <= '9')
                throw new IOException("Unsafe statechart archive path component: " + part);
        }
    }

    private void Write(string name, Stream input, long length, UnixFileMode mode)
    {
        if (length < 0 || length > _limit - _expanded) throw new IOException("Statechart expanded archive byte bound exceeded.");
        StatechartPaths.RequirePlainDirectory(Path.GetDirectoryName(Full(name))!);
        using var output = new FileStream(Full(name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[65536];
        long written = 0;
        int count;
        while ((count = input.Read(buffer)) != 0)
        {
            _token.ThrowIfCancellationRequested();
            if (count > length - written || count > _limit - _expanded)
                throw new IOException("Statechart expanded archive byte bound exceeded.");
            output.Write(buffer, 0, count);
            written += count;
            _expanded += count;
        }
        if (written != length) throw new IOException("Truncated statechart archive member.");
        _modes[name] = mode & (UnixFileMode)0x1ff;
        if (!OperatingSystem.IsWindows() && _modes[name] != 0) File.SetUnixFileMode(Full(name), _modes[name]);
    }

    // Tar names a hard link's target from the archive root and a symbolic link's from the link's directory.
    // The walk expands symbolic links; a hard link it reaches is followed to the file it copies.
    private string Resolve(string name, (string Target, bool Hard) link)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            _token.ThrowIfCancellationRequested();
            if (!seen.Add(name)) throw new IOException("Cyclic statechart archive link.");
            var target = StatechartLinks.Resolve(link.Hard ? "" : StatechartLinks.Parent(name), link.Target,
                candidate => _links.TryGetValue(candidate, out var row) && !row.Hard ? row.Target : null,
                candidate => _directories.TryGetValue(candidate, out var directory) ? directory : null);
            if (!_links.TryGetValue(target, out link)) return target;
            name = target;
        }
    }

    // The empty name is the extraction root, which a link may reach.
    private bool IsDirectory(string name) => name.Length == 0 || _directories[name];

    // Every link resolves before any is created, so a rejected archive leaves no link behind.
    private void Links()
    {
        var resolved = _links.ToDictionary(row => row.Key, row => Resolve(row.Key, row.Value));
        foreach (var name in _links.Where(row => row.Value.Hard).Select(row => row.Key))
        {
            var target = resolved[name];
            if (IsDirectory(target)) throw new IOException("Statechart archive hardlink points to a directory.");
            using var source = File.OpenRead(Full(target));
            Write(name, source, source.Length, _modes[target]);
        }
        foreach (var (name, link) in _links.Where(row => !row.Value.Hard))
        {
            _token.ThrowIfCancellationRequested();
            StatechartPaths.RequirePlainDirectory(Path.GetDirectoryName(Full(name))!);
            var target = link.Target.Replace('/', Path.DirectorySeparatorChar);
            if (IsDirectory(resolved[name])) Directory.CreateSymbolicLink(Full(name), target);
            else File.CreateSymbolicLink(Full(name), target);
        }
    }

    private void DirectoryModes()
    {
        if (OperatingSystem.IsWindows()) return;
        foreach (var (name, mode) in _modes.Where(row => _directories[row.Key]).OrderByDescending(row => row.Key.Length))
            File.SetUnixFileMode(Full(name), mode);
    }

    private string Full(string name) => Path.Combine(_root, name.Replace('/', Path.DirectorySeparatorChar));
}
