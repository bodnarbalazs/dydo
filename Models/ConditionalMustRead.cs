namespace DynaDocs.Models;

public class ConditionalMustRead
{
    public ConditionalMustReadCondition? When { get; init; }
    public required string Path { get; init; }
}
