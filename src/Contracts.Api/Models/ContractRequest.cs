using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Contracts.Api.Models;

public sealed class ContractRequest
{
    [Required]
    [JsonPropertyName("scope_type")]
    public string ScopeType { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("project_id")]
    public Guid ProjectId { get; set; }

    [JsonPropertyName("package_id")]
    public Guid? PackageId { get; set; }

    [Required]
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("value_vnd")]
    public decimal ValueVnd { get; set; }

    [JsonPropertyName("warranty_value_vnd")]
    public decimal WarrantyValueVnd { get; set; }

    [Required]
    [JsonPropertyName("start_date")]
    public DateOnly StartDate { get; set; }

    [Required]
    [JsonPropertyName("end_date")]
    public DateOnly EndDate { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("payment_schedule")]
    public List<PaymentScheduleItem> PaymentSchedule { get; set; } = new();
}
