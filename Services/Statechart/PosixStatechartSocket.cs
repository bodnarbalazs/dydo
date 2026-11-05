namespace DynaDocs.Services.Statechart;

internal static class PosixStatechartSocket
{
    // An absolute pipe name is the socket's own path; this one is where .NET puts a bare name.
    public static string For(string name, string temp) => Path.Combine(temp, "CoreFxPipe_" + name);
}
