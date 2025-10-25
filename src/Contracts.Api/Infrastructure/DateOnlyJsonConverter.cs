using System.Text.Json;
using System.Text.Json.Serialization;

namespace Contracts.Api.Infrastructure;

public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } value)
        {
            if (DateOnly.TryParse(value, out var result))
            {
                return result;
            }
        }

        throw new JsonException($"Unable to convert \"{reader.GetString()}\" to DateOnly");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format));
}
