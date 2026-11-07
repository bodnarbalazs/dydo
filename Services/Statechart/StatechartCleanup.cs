namespace DynaDocs.Services.Statechart;

internal static class StatechartCleanup
{
    public static async Task<int> RunAsync(string[] arguments, TextReader input, TextWriter output)
    {
        if (arguments.Length != 4 || !Guid.TryParseExact(arguments[0], "N", out _))
            throw new InvalidDataException("Invalid statechart cleanup channel.");
        var nonce = arguments[0];
        await Say(output, "ready:1:" + nonce);
        if (await Read(input) != "reserve:" + nonce) return 2;
        using var stage = StatechartScratch.Reserve(arguments[1], arguments[2]);
        using var temp = StatechartScratch.Reserve(arguments[3], arguments[2]);
        await Say(output, "created:" + Encode(stage.Path) + ":" + Encode(temp.Path));
        if (await Read(input) != "register:" + nonce)
        {
            Retain(stage, temp, "owner loss before registration; drain uncertain");
            return 2;
        }
        stage.Register();
        temp.Register();
        await Say(output, "registered:" + nonce);
        if (await Read(input) != "drained:" + nonce)
        {
            Retain(stage, temp, "owner loss or invalid drain provenance; drain uncertain");
            return 2;
        }
        // Only the native owner writes this channel, after sealing and querying its sole job handle.
        if (!stage.CleanupAfterDrain() || !temp.CleanupAfterDrain()) return 2;
        await Say(output, "cleaned:" + nonce);
        return 0;
    }

    public static async Task<string?> Read(TextReader input)
    {
        var buffer = new char[1];
        var message = new System.Text.StringBuilder();
        while (await input.ReadAsync(buffer.AsMemory()) != 0)
        {
            if (buffer[0] == '\n') return message.ToString().TrimEnd('\r');
            if (message.Length == 16384) throw new InvalidDataException("Oversize statechart control frame.");
            message.Append(buffer[0]);
        }
        return null;
    }

    // The helper's created:<stage>:<temp> frame names the scratch it reserved; anything else, EOF included, is the given error.
    public static (string Stage, string Temp) Created(string? frame, string error) =>
        frame?.Split(':') is ["created", var stage, var temp] ? (Decode(stage), Decode(temp)) : throw new InvalidDataException(error);

    public static string Encode(string path) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(path));
    public static string Decode(string path) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(path));
    public static async Task Say(TextWriter output, string message)
    {
        await output.WriteLineAsync(message);
        await output.FlushAsync();
    }

    private static void Retain(StatechartScratch stage, StatechartScratch temp, string reason)
    {
        stage.Retain(reason);
        temp.Retain(reason);
    }
}
