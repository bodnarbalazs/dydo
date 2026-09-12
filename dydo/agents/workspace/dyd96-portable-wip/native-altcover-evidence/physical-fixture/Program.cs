await Task.Yield();

Action<int> executedSetAction = value =>
{
    if (value > 0)
    {
        ProbeState.Record(value);
    }
    else
    {
        ProbeState.Record(-1);
    }
};

Action<int> unexecutedSetAction = value =>
{
    ProbeState.Record(value * 10);
};

executedSetAction(1);
GC.KeepAlive(unexecutedSetAction);

if (ProbeState.Seen != 1)
{
    throw new InvalidOperationException($"Lambda did not execute: {ProbeState.Seen}");
}

Console.WriteLine("ASSERT_EXECUTED=1");

internal static class ProbeState
{
    internal static int Seen { get; private set; }

    internal static void Record(int value)
    {
        Seen = value;
    }
}
