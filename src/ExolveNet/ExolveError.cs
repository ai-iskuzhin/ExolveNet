using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExolveNet;

/// <summary>Тело ошибки Exolve.</summary>
/// <remarks>
/// Платформа присылает ошибку вложенным объектом — <c>{"error":{"message":"…"}}</c>, — хотя в
/// документации встречается и плоская форма <c>{"error":"…"}</c>. Разбираются обе: на практике
/// приходит первая, и модель только под вторую означает, что текст ошибки всегда пустой.
/// </remarks>
public sealed record ExolveError
{
    /// <summary>Текст ошибки — например <c>hlr is disabled on the application</c>.</summary>
    [JsonPropertyName("error")]
    [JsonConverter(typeof(FlexibleErrorTextConverter))]
    public string? Error { get; init; }

    /// <summary>Текст, если платформа положила его в <c>message</c> верхнего уровня.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Код, если он пришёл.</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>Текст ошибки из того поля, в котором он фактически пришёл.</summary>
    public string? Text => string.IsNullOrWhiteSpace(Error) ? Message : Error;
}

/// <summary>
/// Читает <c>error</c> и строкой, и объектом с вложенным <c>message</c>.
/// </summary>
internal sealed class FlexibleErrorTextConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.StartObject:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    foreach (var name in new[] { "message", "error", "description", "detail" })
                    {
                        if (document.RootElement.TryGetProperty(name, out var value)
                            && value.ValueKind == JsonValueKind.String)
                        {
                            return value.GetString();
                        }
                    }
                }
                return null;

            case JsonTokenType.Null:
                return null;

            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
