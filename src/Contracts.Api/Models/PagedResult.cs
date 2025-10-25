using System.Text.Json.Serialization;

namespace Contracts.Api.Models;

public sealed class PagedResult<T>
{
    [JsonPropertyName("items")]
    public IReadOnlyCollection<T> Items { get; init; } = Array.Empty<T>();

    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; init; }

    [JsonPropertyName("total")]
    public long Total { get; init; }
}
