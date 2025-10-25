using System.Text.Json.Serialization;

namespace Contracts.Api.Models;

public sealed class ContractValidationResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("errors")]
    public List<ValidationError> Errors { get; set; } = new();
}
