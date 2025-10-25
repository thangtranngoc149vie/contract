using System.Text.Json.Serialization;

namespace Contracts.Api.Models;

public sealed class ContractsMetaResponse
{
    [JsonPropertyName("scope_types")]
    public IReadOnlyCollection<string> ScopeTypes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("signature_statuses")]
    public IReadOnlyCollection<string> SignatureStatuses { get; init; } = Array.Empty<string>();

    [JsonPropertyName("state_initial")]
    public string StateInitial { get; init; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    [JsonPropertyName("constraints")]
    public ContractConstraints Constraints { get; init; } = new();
}

public sealed class ContractConstraints
{
    [JsonPropertyName("value_min")]
    public decimal ValueMin { get; init; }

    [JsonPropertyName("warranty_min")]
    public decimal WarrantyMin { get; init; }

    [JsonPropertyName("warranty_le_value")]
    public bool WarrantyLessOrEqualValue { get; init; }
}
