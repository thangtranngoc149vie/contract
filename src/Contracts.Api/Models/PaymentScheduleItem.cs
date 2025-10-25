using System.Text.Json.Serialization;

namespace Contracts.Api.Models;

public sealed class PaymentScheduleItem
{
    [JsonPropertyName("due_date")]
    public DateOnly DueDate { get; set; }

    [JsonPropertyName("amount_vnd")]
    public decimal AmountVnd { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
