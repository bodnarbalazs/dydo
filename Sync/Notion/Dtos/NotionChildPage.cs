namespace DynaDocs.Sync.Notion.Dtos;

/// <summary>A child page discovered by enumerating a parent page's <c>child_page</c> blocks (DR 033 §3):
/// the sub-page's id and title. Not a wire DTO — it is projected from <see cref="NotionBlock"/>.</summary>
public sealed class NotionChildPage
{
    public required string Id { get; init; }
    public required string Title { get; init; }
}
