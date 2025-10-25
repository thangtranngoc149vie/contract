using System.Text.Json.Serialization;

namespace Contracts.Api.Models;

public sealed class ErrorResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("field")]
    public string? Field { get; set; }
}
