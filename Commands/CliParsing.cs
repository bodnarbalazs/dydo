namespace DynaDocs.Commands;

using System.CommandLine;

/// <summary>
/// The one way dydo parses its command line. System.CommandLine reads an argument that starts
/// with '@' as a response-file reference, so a guard <c>--command</c> text such as a PowerShell
/// here-string (<c>@'...'@</c>) exited 1 with "Response file not found" instead of getting a
/// verdict, and a hook host may treat exit 1 as a non-blocking error. No dydo command documents
/// response files, so a leading '@' is text everywhere (DYD-675).
/// </summary>
public static class CliParsing
{
    public static ParseResult Parse(Command command, string[] args) =>
        command.Parse(args, new ParserConfiguration { ResponseFileTokenReplacer = null });
}
