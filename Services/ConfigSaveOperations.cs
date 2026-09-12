namespace DynaDocs.Services;

internal sealed record ConfigSaveOperations(
    Func<string, string> ChooseTemporaryPath,
    Func<string, FileStream> CreateNew,
    Action<FileStream, byte[]> WriteAll,
    Action<FileStream> DurableFlush,
    Action<FileStream> Close,
    Action<string, string> Replace)
{
    public static ConfigSaveOperations Default { get; } = new(
        ConfigService.TemporarySiblingPath,
        ConfigService.CreateNewSibling,
        (stream, bytes) => stream.Write(bytes),
        stream => stream.Flush(flushToDisk: true),
        stream => stream.Dispose(),
        (temporary, target) => File.Move(temporary, target, overwrite: true));
}
