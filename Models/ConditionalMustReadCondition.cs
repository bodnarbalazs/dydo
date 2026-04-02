namespace DynaDocs.Models;

public class ConditionalMustReadCondition
{
    public string? MarkerExists { get; init; }
    public string? TaskNameMatches { get; init; }
    public string? DispatchedByRole { get; init; }
}
