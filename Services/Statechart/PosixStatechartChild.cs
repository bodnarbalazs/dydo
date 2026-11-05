namespace DynaDocs.Services.Statechart;

internal static class PosixStatechartChild
{
    public static int Run(string[] arguments)
    {
        if (arguments.Length < 2 || !int.TryParse(arguments[0], out var group) || group <= 0 ||
            !Path.IsPathFullyQualified(arguments[1])) throw new InvalidDataException("Invalid statechart checking-group launch.");
        PosixStatechartProcess.JoinGroup(group);
        throw PosixStatechartProcess.Handoff(arguments[1], arguments.Skip(2), PosixStatechartAnchor.EnvironmentRows());
    }
}
